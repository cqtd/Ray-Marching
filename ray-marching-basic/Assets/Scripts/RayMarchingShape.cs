using UnityEngine;

namespace CQ.RayMarching
{
	public class RayMarchingShape : MonoBehaviour
	{
		enum EShapeType
		{
			CUBE,
			SPHERE,
			TORUS,
			FLOOR_PLANE,
			BACKGROUND_PLANE,
		}

		enum EBlendType
		{
			NONE,
			BLEND,
			CUT,
			MASK,
		}

		[SerializeField] EShapeType m_shape = EShapeType.CUBE;
		[SerializeField] Vector2 m_radii = Vector2.one;
		[SerializeField] Color m_color = Color.white;
		[SerializeField] EBlendType m_blend = EBlendType.NONE;
		[SerializeField] [Range(0, 1)] float m_blendStrength = 0.1f;

		public int ShapeType
		{
			get
			{
				return (int) m_shape;
			}
		}

		public int BlendType {
			get
			{
				return (int) m_blend;
			}
		}

		public float BlendStrength {
			get
			{
				return m_blendStrength;
			}
		}

		public Vector3 Position {
			get
			{
				return transform.position;
			}
		}

		public Vector3 Scale {
			get
			{
				return transform.localScale * 0.5f;
			}
		}

		public Vector2 TorusR1R2 {
			get
			{
				return m_radii;
			}
		}

		public Color Color {
			get
			{
				return this.m_color;
			}
		}

		public Vector3 Rotation {
			get
			{
				return transform.localEulerAngles * Mathf.Deg2Rad;
			}
		}

		public Matrix4x4 TRMatrix {
			get
			{
				return Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one).inverse;
			}
		}
	}
}