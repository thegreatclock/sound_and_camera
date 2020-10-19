using UnityEngine;

namespace GreatClock.Common.Sound {

	public class Audio3DSettings : ScriptableObject {

		[SerializeField]
		private float m_MinDistance = 1f;
		[SerializeField]
		private float m_MaxDistance = 20f;
		[SerializeField]
		private AnimationCurve m_RollOffCurve = new AnimationCurve(new Keyframe(0f, 1f, 0f, -3f), new Keyframe(1f, 0f));

		private bool mCurveCalculated = false;
		private AnimationCurve mCalculatedCurve;

		public float MinDistance { get { return m_MinDistance; } }

		public float MaxDistance { get { return m_MaxDistance; } }

		public AnimationCurve RollOffCurve {
			get {
				if (!mCurveCalculated) {
					mCurveCalculated = true;
					if (m_MinDistance <= 0f) {
						mCalculatedCurve = new AnimationCurve(m_RollOffCurve.keys);
					} else {
						float t0 = m_MinDistance / m_MaxDistance;
						float t1 = 1f - t0;
						float rt1 = 1f / t1;
						int len = m_RollOffCurve.length;
						Keyframe[] frames = new Keyframe[len + 1];
						frames[0] = new Keyframe(0f, 1f);
						int i = 0;
						while (i < len) {
							Keyframe frame = m_RollOffCurve[i];
							frame.time = frame.time * t1 + t0;
							frame.inTangent *= rt1;
							frame.outTangent *= rt1;
							i++;
							frames[i] = frame;
						}
						mCalculatedCurve = new AnimationCurve(frames);
					}
				}
				return mCalculatedCurve;
			}
		}

	}

}
