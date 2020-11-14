using System;
using System.Collections.Generic;
using UnityEngine;

namespace CQ.RayMarching
{
	 [ExecuteInEditMode, ImageEffectAllowedInSceneView, RequireComponent(typeof(ComputeShader))]
	public class RayMarchingManager : MonoBehaviour
	{
		enum ELightClass
		{
			DIRECTIONAL,
			POINT,
			SPOT,
		}

		struct ShapeDataStruct
		{
			public int shapeType;
			public int blendType;
			public float blendStrength;
			public Vector2 radii;
			public Vector3 position;
			public Vector3 rotation;
			public Vector3 scale;
			public Vector4 color;

			public static int GetBytes()
			{
				return sizeof(int) * 2 + sizeof(float) * (1 + 2 + 3 * 3 + 4);
			}
		}

		struct LightStruct
		{
			public float range;
			public float angle;
			public float intensity;
			public Vector3 dir;
			public Vector3 pos;
			public Vector4 color;

			public static int GetBytes() { return sizeof(float)*(3 + 3*2 + 4); }
		}

		[Range(0f,1f)]
		[SerializeField] float m_ambientIntensity = .2f;
		[SerializeField] float m_softShadowCoef = 8f;
		[SerializeField] Color m_ambientColor = Color.white;
		[SerializeField] bool m_paintNormals = false;
		[SerializeField] ComputeShader m_raymarchingShader;

		Camera m_camera;
		RenderTexture m_tmpRenderTex;
		ComputeBuffer m_shapesBuffer, m_lightsBuffer;
		LightStruct[] m_lights;
		ShapeDataStruct[] m_shapesData;
		List<RayMarchingShape> shapes;

		void OnRenderImage(RenderTexture src, RenderTexture dest)
		{
			m_camera = Camera.current;
			CleanOrCreateRenderTexture();

			ProcessLights();
			
			// @TODO: 최적화 필요
			shapes = new List<RayMarchingShape>( FindObjectsOfType<RayMarchingShape>());
			if (shapes.Count > 0)
			{
				m_shapesData = new ShapeDataStruct[shapes.Count];
				m_shapesBuffer = new ComputeBuffer(m_shapesData.Length, ShapeDataStruct.GetBytes());

				ProcessShapes(ref shapes, ref m_shapesData, ref m_shapesBuffer);

				SetupComputeShader(src, m_tmpRenderTex);
				
				// launch kernel
				// get the proper grid size
				int gridSizeX = Mathf.CeilToInt(m_camera.pixelWidth / 8.0f);
				int gridSizeY = Mathf.CeilToInt(m_camera.pixelHeight / 8.0f);
				
				// run the compute shader
				m_raymarchingShader.Dispatch(0, gridSizeX, gridSizeY, 1);
				
				// copy the processed texture onto the output
				Graphics.Blit(m_tmpRenderTex, dest);
				
				// clean buffers
				m_shapesBuffer.Dispose();
				m_lightsBuffer.Dispose();
			}
			else
			{
				Graphics.Blit(src, dest);
			}
		}

		void SetupComputeShader(RenderTexture src, RenderTexture dest)
		{
			// pass the shapes buffer
			m_raymarchingShader.SetBuffer(0, "_shapes", m_shapesBuffer);
			m_raymarchingShader.SetInt( "_numShapes", m_shapesData.Length);
			
			// pass the lights buffer
			m_raymarchingShader.SetBuffer(0, "_lights", m_lightsBuffer);
			m_raymarchingShader.SetInt( "__numLights", m_lights.Length);
			
			// pass the needed matrices
			m_raymarchingShader.SetMatrix("_Camera2WorldMatrix", m_camera.cameraToWorldMatrix);
			m_raymarchingShader.SetMatrix("_InverseProjectionMatrix", m_camera.projectionMatrix.inverse);
			
			// pass the textures
			m_raymarchingShader.SetTexture(0, "_srcTex", src);
			m_raymarchingShader.SetTexture(0, "_outTex", dest);
			
			// pass the ambient light information
			m_raymarchingShader.SetFloat("_Ka", m_ambientIntensity);
			m_raymarchingShader.SetVector("_ambientColor", m_ambientColor);
			
			m_raymarchingShader.SetFloat("_Ksh", m_softShadowCoef);
			m_raymarchingShader.SetInt("_paintNormals", (m_paintNormals) ? 1 : 0);
		}

		void ProcessShapes(ref List<RayMarchingShape> mShapes, ref ShapeDataStruct[] shapeData, ref ComputeBuffer mShapesBuffer)
		{
			shapes.Sort((a,b) => a.BlendType.CompareTo(b.BlendType));

			for (int i = 0; i < shapes.Count; i++)
			{
				shapeData[i].shapeType = shapes[i].ShapeType;
				shapeData[i].blendType = shapes[i].BlendType;
				shapeData[i].blendStrength = shapes[i].BlendStrength;
				shapeData[i].radii = shapes[i].TorusR1R2;
				shapeData[i].position = shapes[i].Position;
				shapeData[i].rotation = shapes[i].Rotation;
				shapeData[i].scale = shapes[i].Scale;
				shapeData[i].color = shapes[i].Color;
			}
			
			mShapesBuffer.SetData(shapeData);
		}

		void ProcessLights()
		{
			Light[] lights = FindObjectsOfType<Light>();
			m_lights = new LightStruct[lights.Length];
			
			for (int i = 0; i < lights.Length; i++)
			{
				m_lights[i].dir = lights[i].transform.forward;
				m_lights[i].color = lights[i].color;
				m_lights[i].intensity = lights[i].intensity;

				switch (lights[i].type)
				{
					case LightType.Spot:
						m_lights[i].angle = lights[i].spotAngle;
						m_lights[i].range = lights[i].range;
						m_lights[i].pos = lights[i].transform.position;
						break;
					
					case LightType.Directional:
						m_lights[i].angle = 360f;
						m_lights[i].range = float.PositiveInfinity;
						m_lights[i].pos = Vector3.positiveInfinity;
						break;
					
					case LightType.Point:
						m_lights[i].angle = 360f;
						m_lights[i].range = lights[i].range;
						m_lights[i].pos = lights[i].transform.position;
						break;
					
					case LightType.Area:
						break;
					case LightType.Disc:
						break;
					
					default:
						m_lights[i].angle = 360f;
						m_lights[i].range = float.PositiveInfinity;
						m_lights[i].pos = Vector3.positiveInfinity;
						break;
				}
			}
			
			m_lightsBuffer = new ComputeBuffer(m_lights.Length, LightStruct.GetBytes());
			m_lightsBuffer.SetData(m_lights);
		}

		void CleanOrCreateRenderTexture()
		{
			if (m_tmpRenderTex == null ||
			    m_tmpRenderTex.width == m_camera.pixelHeight || m_tmpRenderTex.height == m_camera.pixelHeight)
			{
				if (m_tmpRenderTex != null)
				{
					m_tmpRenderTex.Release();
				}

				m_tmpRenderTex = new RenderTexture(m_camera.pixelWidth, m_camera.pixelHeight, 0,
					RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

				m_tmpRenderTex.enableRandomWrite = true;
				m_tmpRenderTex.Create();
			}
		}
	}
}