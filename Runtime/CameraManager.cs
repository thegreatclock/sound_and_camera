using GreatClock.Common.Tweens;
using GreatClock.Common.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace GreatClock.Common.Camera3D {

	public sealed class CameraManager {

		#region singleton
		private static CameraManager s_instance;
		public static CameraManager instance {
			get {
				if (s_instance == null) {
					s_instance = new CameraManager();
				}
				return s_instance;
			}
		}
		private CameraManager() { }
		#endregion

		#region API

		public void Init(Camera camera) {
			mCamera = camera;
			if (mUpdate == null) {
				mUpdate = Update;
				FlushUpdater();
			}
		}

		public enum eUpdaterType { LateUpdate, FixedUpdate }

		public eUpdaterType updater {
			get {
				return mUpdater;
			}
			set {
				if (mUpdater == value) { return; }
				mUpdater = value;
				FlushUpdater();
			}
		}

		public void SetDefaultValues(float rotY, float lookDown, float distance, float fovOrSize,
			float followSpeedFactor, float followMinSpeed, float followMaxSpeed) {
			DefaultRotY = rotY;
			DefaultLookDown = lookDown;
			DefaultDistance = distance;
			DefaultCamFovOrSize = fovOrSize;
			if (!mDefaultInited) {
				mDefaultInited = true;
				mRotY.Value = rotY;
				mLookDown.Value = lookDown;
				mDistance.Value = distance;
				mCamFovOrSize.Value = fovOrSize;
			}
			mFollowSpeedFactor = followSpeedFactor;
			mFollowMinSpeed = followMinSpeed;
			mFollowMaxSpeed = followMaxSpeed;
		}

		public void SetFollowTarget(Transform target, Vector3 offset) {
			mTargetInited = true;
			FollowTarget = target;
			mTweenTargetOffset.Value = offset;
			mPrevPos = target != null ? target.position + offset : offset;
			mTargetPos.Value = mPrevPos;
			FlushCamera();
		}

		public void ChangeFollowTarget(Transform target, Vector3 offset, float duration) {
			ChangeFollowTarget(target, offset, duration, Ease.eEaseType.Linear);
		}

		public void ChangeFollowTarget(Transform target, Vector3 offset, float duration, Ease.eEaseType ease) {
			if (!mTargetInited) {
				SetFollowTarget(target, offset);
				return;
			}
			FollowTarget = target;
			mTweenTargetOffset.Value = offset;
			mPrevPos = target != null ? target.position + offset : offset;
			mTargetPos.AnimTo(mPrevPos, duration, ease);
			mPropTweening = true;
		}

		/// <summary>
		/// 按照指定最大速度移动摄像机至跟随目标
		/// </summary>
		/// <param name="target">跟随的目标，可为空，此时偏移坐标(offset)为目标点坐标</param>
		/// <param name="offset">计算用跟随的中心点相对目标的世界坐标系下的偏移</param>
		/// <param name="speed">摄像机最大移动速度</param>
		public void MoveToAndChangeFollowTarget(Transform target, Vector3 offset, float speed) {
			mTargetInited = true;
			FollowTarget = target;
			mTweenTargetOffset.Value = offset;
			mPrevPos = target != null ? target.position + offset : offset;
			mTargetPos.GoTo(mPrevPos, 4f, speed * 0.05f, speed);
			mPropTweening = true;
		}

		public void ChangeView(CameraParameter rotY, CameraParameter lookDown, CameraParameter distance, CameraParameter fovOrSize) {
			if (mViewInited) {
				bool b1 = AnimProperty(mRotY, rotY, true);
				bool b2 = AnimProperty(mLookDown, lookDown, true);
				bool b3 = AnimProperty(mDistance, distance, false);
				bool b4 = AnimProperty(mCamFovOrSize, fovOrSize, false);
				if (!b1 || !b2 || !b3 || !b4) {
					mPropTweening = true;
				}
			} else {
				mViewInited = true;
				mRotY.Value = rotY.value;
				mLookDown.Value = lookDown.value;
				mDistance.Value = distance.value;
				mCamFovOrSize.Value = fovOrSize.value;
			}
			FlushCamera();
		}

		public void ResetView(float duration, Ease.eEaseType ease) {
			if (mViewInited) {
				AnimProperty(mRotY, CameraParameter.Get(DefaultRotY, duration, ease), true);
				AnimProperty(mLookDown, CameraParameter.Get(DefaultLookDown, duration, ease), true);
				mDistance.AnimTo(DefaultDistance, duration, ease);
				mCamFovOrSize.AnimTo(DefaultCamFovOrSize, duration, ease);
				mPropTweening = true;
			} else {
				mViewInited = true;
				mRotY.Value = DefaultRotY;
				mLookDown.Value = DefaultLookDown;
				mDistance.Value = DefaultDistance;
				mCamFovOrSize.Value = DefaultCamFovOrSize;
				FlushCamera();
			}
		}

		public void ChangeOffset(Vector3 offset, float duration, Ease.eEaseType ease) {
			mTweenTargetOffset.AnimTo(offset, duration, ease);
		}

		public void EarthQuake(EarthQuakeData data) {
			if (mEarthQuakeTimer <= 0f) {
				mCaneraProjection = mCamera.projectionMatrix;
			}
			mEarthQuakeHardMode = data.hard;
			mEarthQuakeAmplitude = data.amplitude;
			mEarthQuakeDur = 0.5f / data.frequency;
			mEarthQuakeTimer = Mathf.Ceil(data.duration / mEarthQuakeDur) * mEarthQuakeDur;
			mEarthQuakeRandomDirection = float.IsNaN(data.direction);
			mEarthQuakeDirection = mEarthQuakeRandomDirection ? Random.Range(0f, 2f * Mathf.PI) : data.direction;
		}

		public Camera camera { get { return mCamera; } }

		public Transform FollowTarget { get; private set; }
		public Vector3 TargetOffset { get { return mTweenTargetOffset.Value; } }

		public float DefaultRotY { get; private set; }
		public float DefaultLookDown { get; private set; }
		public float DefaultDistance { get; private set; }
		public float DefaultCamFovOrSize { get; private set; }

		#endregion

		#region camera properties

		private Camera mCamera;
		private eUpdaterType mUpdater = eUpdaterType.LateUpdate;
		private GameUpdater.IUpdater mPrevUpdater;
		private GameUpdater.UpdateDelegate mUpdate;
		private bool mTargetInited = false;
		private Vector3Tween mTweenTargetOffset = new Vector3Tween();
		private float mFollowSpeedFactor;
		private float mFollowMinSpeed;
		private float mFollowMaxSpeed;

		private Vector3 mPrevPos;
		private bool mPropTweening = false;

		//目标位置，用于在切换摄像机目标或目标位置时，假定目标点移动数值及插值计算;
		private Vector3Tween mTargetPos = new Vector3Tween();
		private bool mViewInited = false;
		private FloatTween mRotY = new FloatTween();
		private FloatTween mLookDown = new FloatTween();
		private FloatTween mDistance = new FloatTween();
		private FloatTween mCamFovOrSize = new FloatTween();

		private bool mDefaultInited = false;

		public float RotationY { get { return mRotY.Value; } }
		public float LookDown { get { return mLookDown.Value; } }
		public float Distance { get { return mDistance.Value; } }
		public float FovOrSize { get { return mCamFovOrSize.Value; } }


		#endregion

		#region internal funcs

		private void FlushUpdater() {
			if (mUpdate == null) { return; }
			if (mPrevUpdater != null) {
				mPrevUpdater.Remove(mUpdate);
			}
			GameUpdater.IUpdater updater = null;
			switch (mUpdater) {
				case eUpdaterType.LateUpdate:
					updater = GameUpdater.late_updater;
					break;
				case eUpdaterType.FixedUpdate:
					updater = GameUpdater.fixed_updater;
					break;
			}
			mPrevUpdater = updater;
			if (updater != null) { updater.Add(mUpdate); }
		}

		private void Update(float deltaTime) {
			if (!mTargetInited) { return; }
			bool flag = false;
			mTweenTargetOffset.Update(deltaTime);
			Vector3 pos = mTweenTargetOffset.Value;
			if (FollowTarget != null) { pos += FollowTarget.position; }
			bool targetMoved = false;
			if ((mPrevPos - pos).sqrMagnitude > 1E-8f) {
				targetMoved = true;
				mPrevPos = pos;
			}
			if (targetMoved || (pos - mTargetPos.Value).sqrMagnitude > 1E-8f) {
				switch (mTargetPos.State) {
					case ePropTweenState.None:
						if (mFollowSpeedFactor <= 0f) {
							mTargetPos.Value = pos;
						} else {
							mTargetPos.GoTo(pos, mFollowSpeedFactor, mFollowMinSpeed, mFollowMaxSpeed);
							mPropTweening = true;
						}
						break;
					case ePropTweenState.SpeedControl:
						mTargetPos.To = pos;
						break;
				}
				flag = true;
			}
			if (mPropTweening) {
				mPropTweening = false;
				if (mTargetPos.Update(deltaTime) != eUpdateReturn.Finished) {
					mPropTweening = true;
				}
				if (mRotY.Update(deltaTime) != eUpdateReturn.Finished) {
					mPropTweening = true;
				}
				if (mLookDown.Update(deltaTime) != eUpdateReturn.Finished) {
					mPropTweening = true;
				}
				if (mDistance.Update(deltaTime) != eUpdateReturn.Finished) {
					mPropTweening = true;
				}
				if (mCamFovOrSize.Update(deltaTime) != eUpdateReturn.Finished) {
					mPropTweening = true;
				}
				flag = true;
			}
			if (flag) { FlushCamera(); }
			if (mEarthQuakeTimer > 0f) {
				float t0 = (mEarthQuakeTimer / mEarthQuakeDur) % 1f;
				mEarthQuakeTimer -= deltaTime;
				float t1 = mEarthQuakeTimer > 0f ? (mEarthQuakeTimer / mEarthQuakeDur) % 1f : 0f;
				bool flush = false;
				Vector2 flushOffset = Vector2.zero;
				if (mEarthQuakeTimer <= 0f) {
					mCamera.ResetProjectionMatrix();
				} else if (t0 < t1) {
					mEarthQuakeOffsetFrom = mEarthQuakeOffsetTo;
					if (mEarthQuakeTimer < mEarthQuakeDur) {
						mEarthQuakeOffsetTo = Vector2.zero;
					} else {
						if (mEarthQuakeRandomDirection) {
							float delta = Random.Range(2f, 4f) * Mathf.PI / 3f;
							if (mEarthQuakeDirection > 0f) {
								mEarthQuakeDirection -= delta;
							} else {
								mEarthQuakeDirection += delta;
							}
						} else {
							if (mEarthQuakeDirection > 0f) {
								mEarthQuakeDirection -= Mathf.PI;
							} else {
								mEarthQuakeDirection += Mathf.PI;
							}
						}
						mEarthQuakeOffsetTo = new Vector2(Mathf.Cos(mEarthQuakeDirection), Mathf.Sin(mEarthQuakeDirection)) * mEarthQuakeAmplitude;
					}
					if (mEarthQuakeHardMode) {
						flush = true;
						flushOffset = mEarthQuakeOffsetTo;
					}
				}
				if (!mEarthQuakeHardMode) {
					flush = true;
					flushOffset = Vector2.LerpUnclamped(mEarthQuakeOffsetTo, mEarthQuakeOffsetFrom, Mathf.Sin((t1 - 0.5f) * Mathf.PI) * 0.5f + 0.5f);
				}
				if (flush) {
					flushOffset *= Mathf.Min(Screen.width, Screen.height) / 100f;
					Vector3 screenOffset = new Vector3(flushOffset.x / Screen.width, flushOffset.y / Screen.height, 0f);
					mCamera.projectionMatrix = Matrix4x4.Translate(screenOffset) * mCaneraProjection;
				}
			}
		}

		private void FlushCamera() {
			if (mCamera == null) { return; }
			Quaternion rot = Quaternion.Euler(mLookDown.Value, mRotY.Value, 0f);
			Vector3 offset = rot * Vector3.forward * mDistance.Value;
			float fovOrSize = mCamFovOrSize.Value;
			if (mCamera.orthographic) {
				if (fovOrSize != mCamera.orthographicSize) {
					mCamera.orthographicSize = mCamFovOrSize.Value;
				}
			} else {
				if (fovOrSize != mCamera.fieldOfView) {
					mCamera.fieldOfView = mCamFovOrSize.Value;
				}
			}
			Transform cam = mCamera.transform;
			cam.position = mTargetPos.Value - offset;
			cam.rotation = rot;
		}

		private bool AnimProperty(FloatTween prop, CameraParameter para, bool chechAngle) {
			if (para == null || prop == null) { return false; }
			float value = para.value;
			if (chechAngle && para.isAngle) {
				float pt = prop.To;
				while (pt > 180f) { pt -= 360f; prop.Value -= 360f; }
				while (pt < -180f) { pt += 360f; prop.Value += 360f; }
				while (value - pt > 180f) { value -= 360f; }
				while (value - pt < -180f) { value += 360f; }
			}
			return para.curve != null ?
				prop.AnimTo(value, para.duration, para.curve) :
				prop.AnimTo(value, para.duration, para.ease);
		}

		#endregion

		#region earth quake

		private Matrix4x4 mCaneraProjection;

		private float mEarthQuakeTimer;
		private bool mEarthQuakeHardMode;
		private float mEarthQuakeAmplitude;
		private float mEarthQuakeDur;
		private bool mEarthQuakeRandomDirection;
		private float mEarthQuakeDirection;

		private Vector2 mEarthQuakeOffsetFrom;
		private Vector2 mEarthQuakeOffsetTo;

		#endregion
	}

	public class CameraParameter {
		public float value;
		public float duration;
		public Ease.eEaseType ease;
		public AnimationCurve curve;
		public bool isAngle;
		public CameraParameter SetIsAngle() { isAngle = true; return this; }
		private static Queue<CameraParameter> cached_instances = new Queue<CameraParameter>();
		public static CameraParameter Get(float value, float duration) {
			CameraParameter ins = cached_instances.Count > 0 ? cached_instances.Dequeue() : new CameraParameter();
			ins.value = value;
			ins.duration = duration;
			ins.ease = Ease.eEaseType.Linear;
			ins.curve = null;
			ins.isAngle = false;
			return ins;
		}
		public static CameraParameter Get(float value, float duration, Ease.eEaseType ease) {
			CameraParameter ins = cached_instances.Count > 0 ? cached_instances.Dequeue() : new CameraParameter();
			ins.value = value;
			ins.duration = duration;
			ins.ease = ease;
			ins.curve = null;
			ins.isAngle = false;
			return ins;
		}
		public static CameraParameter Get(float value, float duration, AnimationCurve curve) {
			CameraParameter ins = cached_instances.Count > 0 ? cached_instances.Dequeue() : new CameraParameter();
			ins.value = value;
			ins.duration = duration;
			ins.ease = Ease.eEaseType.Linear;
			ins.curve = curve;
			ins.isAngle = false;
			return ins;
		}
		public static void Cache(CameraParameter para) {
			if (para == null) { return; }
			para.curve = null;
			cached_instances.Enqueue(para);
		}
	}

}