using GreatClock.Common.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace GreatClock.Common.Sound {

	public struct SfxData {

		public string folder;
		public string sfx;
		public float volume;
		public float delay;
		public float fadein;
		public bool loop;

		public static SfxData GetSfxData(string folder, string sfx) {
			SfxData data = new SfxData();
			data.folder = folder;
			data.sfx = sfx;
			data.volume = 1f;
			data.delay = 0f;
			data.fadein = 0f;
			data.loop = false;
			return data;
		}

	}

	/// <summary>
	/// 音乐音效管理器
	/// </summary>
	public abstract class SoundManagerBase {

		private const string VOLUME_SOUND = "Volume_Sound";
		private const string VOLUME_MUSIC = "Volume_Music";
		private const string FX_PITCH = "Fx_Pitch";

		private const string KEY_MUSIC_VOLUME = "Setting_MusicVolume";
		private const string KEY_SOUND_VOLUME = "Setting_SoundVolume";

		private const float MUSIC_FADE_DURATION = 1f;

		private GameObject mSoundRoot = null;

		private AudioMixer mMasterMixer = null;
		private Audio3DSettings m3DSettings = null;

		private AudioMixerGroup mGroupMusic;
		private AudioMixerGroup mGroupSound;
		private AudioMixerGroup mGroupPitch;
		private AudioMixerGroup mGroupVoice;

		private int mIdGen = 0;

		private float mVolumeMusic = 1f;
		private float mVolumeSound = 1f;

		private float mVolumeMusicLimit = 1f;
		private float mVolumeMusicLimitTimer = float.NaN;
		private float mVolumeMusicLimitFrom;
		private float mVolumeMusicLimitTo;
		private float mVolumeMusicLimitDuration;

		private Queue<SfxPlayingData> mCacheList = new Queue<SfxPlayingData>();
		private Dictionary<int, SfxPlayingData> mLoadingList = new Dictionary<int, SfxPlayingData>();
		private Dictionary<int, SfxPlayingData> mPlayingList = new Dictionary<int, SfxPlayingData>();
		private List<SfxPlayingData> mFadingList = new List<SfxPlayingData>();
		private List<SfxPlayingData> mVoiceList = new List<SfxPlayingData>();

		private string mCurMusicFolder = null;
		private string mCurMusicSfx = null;
		private int mCurMusicId = -1;

		public void Init(Audio3DSettings settings, IAudioClipLoader loader) {
			m3DSettings = settings;
			SfxPlayingData.loader = loader;
		}

		public float VolumeMusic {
			get {
				return mVolumeMusic;
			}
			set {
				if (mVolumeMusic != value) {
					mVolumeMusic = value;
					PlayerPrefs.SetFloat(KEY_MUSIC_VOLUME, mVolumeMusic);
					ApplyMusicVolume(mVolumeMusic);
				}
			}
		}
		public float VolumeSound {
			get {
				return mVolumeSound;
			}
			set {
				if (mVolumeSound != value) {
					mVolumeSound = value;
					PlayerPrefs.SetFloat(KEY_SOUND_VOLUME, mVolumeSound);
					ApplySoundVolume(mVolumeSound);
				}
			}
		}

		public float MaxDistance { get { return m3DSettings == null ? 1000f : m3DSettings.MaxDistance; } }

		/// <summary>
		/// 暂停背景音乐播放
		/// </summary>
		public void PauseMusic(bool fade) {
			PauseAudioInternal(mCurMusicId, fade);
		}
		/// <summary>
		/// 恢复背景音乐播放
		/// </summary>
		public void ResumeMusic(bool fade) {
			ResumeAudioInternal(mCurMusicId, fade);
		}
		/// <summary>
		/// 停止背景音乐播放
		/// </summary>
		public void StopMusic() {
			FadeOutStopInternal(mCurMusicId, MUSIC_FADE_DURATION);
			mCurMusicId = -1;
			mCurMusicFolder = null;
			mCurMusicSfx = null;
		}

		public bool Stop(int id) {
			return FadeOutStopInternal(id, 0f);
		}

		public bool FadeOutStop(int id, float fadeout) {
			return FadeOutStopInternal(id, fadeout);
		}

		public bool IsPlaying(int id) {
			return mPlayingList.ContainsKey(id) || mLoadingList.ContainsKey(id);
		}

		protected SoundManagerBase() {
			mSoundRoot = new GameObject("SoundRoot");
			GameObject.DontDestroyOnLoad(mSoundRoot);

			mMasterMixer = Resources.Load<AudioMixer>("Sound/MasterMixer");

			mGroupMusic = FindAudioMixerGroup("Music");
			mGroupSound = FindAudioMixerGroup("Sound");
			mGroupPitch = FindAudioMixerGroup("Sound/Pitch");
			mGroupVoice = FindAudioMixerGroup("Sound/Voice");

			mVolumeMusic = PlayerPrefs.GetFloat(KEY_MUSIC_VOLUME, 1f);
			mVolumeSound = PlayerPrefs.GetFloat(KEY_SOUND_VOLUME, 1f);
			ApplyMusicVolume(mVolumeMusic);
			ApplySoundVolume(mVolumeSound);

			GameUpdater.updater.AddUnScaled(OnUpdate);
		}

		protected int PlayAudioInternal(SfxData sfx, Action<int> callback) {
			if (mVolumeSound <= 0f && !sfx.loop) { return -1; }
			return PlaySoundInternal(sfx, false, Vector3.zero, false, mGroupSound, callback);
		}

		protected int Play3DAudioInternal(SfxData sfx, Vector3 pos, Action<int> callback) {
			if (mVolumeSound <= 0f && !sfx.loop) { return -1; }
			return PlaySoundInternal(sfx, true, pos, false, mGroupSound, callback);
		}

		protected int PlayAudioPitchInternal(SfxData sfx, float pitch, Action<int> callback) {
			if (mVolumeSound <= 0f && !sfx.loop) { return -1; }
			mMasterMixer.SetFloat(FX_PITCH, pitch);
			return PlaySoundInternal(sfx, false, Vector3.zero, false, mGroupPitch, callback);
		}
		protected int PlayVoiceInternal(SfxData sfx, Action<int> callback) {
			if (mVolumeSound <= 0f && !sfx.loop) { return -1; }
			return PlaySoundInternal(sfx, false, Vector3.zero, true, mGroupVoice, callback);
		}

		protected void PlayMusicInternal(SfxData sfx) {
			if (mCurMusicFolder == sfx.folder && mCurMusicSfx == sfx.sfx) { return; }
			if (FadeOutStopInternal(mCurMusicId, MUSIC_FADE_DURATION)) {
				sfx.delay += MUSIC_FADE_DURATION;
			}
			mCurMusicFolder = sfx.folder;
			mCurMusicSfx = sfx.sfx;
			sfx.loop = true;
			mCurMusicId = PlaySoundInternal(sfx, false, Vector3.zero, false, mGroupMusic, null);
		}

		private void ApplyMusicVolume(float volume) {
			mMasterMixer.SetFloat(VOLUME_MUSIC, Mathf.Max(-80f, 20f * Mathf.Log10(volume * mVolumeMusicLimit)));
		}

		private void ApplySoundVolume(float volume) {
			mMasterMixer.SetFloat(VOLUME_SOUND, Mathf.Max(-80f, 20f * Mathf.Log10(volume)));
		}

		private int PlaySoundInternal(SfxData sfx, bool is3D, Vector3 pos, bool isVoice, AudioMixerGroup group, Action<int> callback) {
			if (string.IsNullOrEmpty(sfx.sfx)) {
				return -1;
			}
			//Log.dtf("zw", "Play Sound sfxId '{0}' name '{1}' !", sfxId, sfx.sfx);
			SfxPlayingData data = GetAudioData();
			int id = ++mIdGen;
			data.Init(id, sfx.folder, sfx.sfx, sfx.volume, isVoice, sfx.fadein, Time.unscaledTime + sfx.delay, callback);
			data.source.transform.position = pos;
			data.source.spatialBlend = is3D ? 1f : 0f;
			data.source.volume = sfx.volume;
			data.source.loop = sfx.loop;
			data.source.outputAudioMixerGroup = group;
			if (is3D) {
				if (m3DSettings != null) {
					data.source.minDistance = m3DSettings.MinDistance;
					data.source.maxDistance = m3DSettings.MaxDistance;
					data.source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, m3DSettings.RollOffCurve);
					data.source.rolloffMode = AudioRolloffMode.Custom;
				} else {
					data.source.minDistance = 1f;
					data.source.maxDistance = 1000f;
					data.source.rolloffMode = AudioRolloffMode.Logarithmic;
				}
			}
			if (isVoice) {
				mVoiceList.Add(data);
				if (mVoiceList.Count == 1) {
					mVolumeMusicLimitFrom = mVolumeMusicLimit;
					mVolumeMusicLimitTo = 0.2f;
					mVolumeMusicLimitDuration = 1f / 0.5f;
					mVolumeMusicLimitTimer = 0f;
				}
			}
			mLoadingList.Add(data.id, data);
			data.Load();
			return data.id;
		}

		private Func<SfxPlayingData, AudioClip, bool> mOnAudioLoaded;
		private bool OnAudioLoaded(SfxPlayingData data, AudioClip clip) {
			if (!mLoadingList.Remove(data.id)) { return false; }
			if (clip == null) {
				Debug.LogErrorFormat("[SoundManager] Fail to load sfx '{0}{1}' !", data.folder, data.sfx);
				StopAudio(data, true);
				return false;
			}
			mPlayingList.Add(data.id, data);
			return true;
		}

		private Action<SfxPlayingData> mOnBeginFading;
		private void OnBeginFading(SfxPlayingData data) {
			mFadingList.Add(data);
		}

		private Action<SfxPlayingData, bool> mOnAudioFinished;
		private void OnAudioFinished(SfxPlayingData data, bool manuallyStop) {
			mPlayingList.Remove(data.id);
			StopAudio(data, manuallyStop);
		}

		private void StopAudio(SfxPlayingData data, bool manuallyStop) {
			data.StopAudio(manuallyStop);
			if (data.voice && mVoiceList.Remove(data)) {
				if (mVoiceList.Count <= 0) {
					mVolumeMusicLimitFrom = mVolumeMusicLimit;
					mVolumeMusicLimitTo = 1f;
					mVolumeMusicLimitDuration = 1f / 0.5f;
					mVolumeMusicLimitTimer = 0f;
				}
			}
			CacheAudioData(data);
		}

		private bool FadeOutStopInternal(int id, float fadeout) {
			SfxPlayingData data;
			if (mPlayingList.TryGetValue(id, out data)) {
				data.FadeOutStop(fadeout);
				return true;
			}
			if (mLoadingList.TryGetValue(id, out data)) {
				mLoadingList.Remove(id);
				StopAudio(data, true);
				return true;
			}
			return false;
		}
		private void PauseAudioInternal(int id, bool fade) {
			SfxPlayingData data;
			if (mPlayingList.TryGetValue(id, out data) || mLoadingList.TryGetValue(id, out data)) {
				data.Pause(fade);
			}
		}

		private void ResumeAudioInternal(int id, bool fade) {
			SfxPlayingData data;
			if (mPlayingList.TryGetValue(id, out data) || mLoadingList.TryGetValue(id, out data)) {
				data.Resume(fade);
			}
		}

		private SfxPlayingData GetAudioData() {
			if (mCacheList.Count > 0) {
				SfxPlayingData data = mCacheList.Dequeue();
				data.source.gameObject.SetActive(true);
				return data;
			} else {
				GameObject o = new GameObject();
				o.name = "Audio";
				o.transform.parent = mSoundRoot.transform;
				SfxPlayingData data = new SfxPlayingData(o.AddComponent<AudioSource>());
				data.source.playOnAwake = true;
				data.source.loop = false;
				data.source.rolloffMode = AudioRolloffMode.Logarithmic;
				data.source.minDistance = 1f;
				data.source.maxDistance = 500f;
				if (mOnAudioLoaded == null) { mOnAudioLoaded = OnAudioLoaded; }
				data.onLoaded = mOnAudioLoaded;
				if (mOnBeginFading == null) { mOnBeginFading = OnBeginFading; }
				data.onBeginFading = mOnBeginFading;
				if (mOnAudioFinished == null) { mOnAudioFinished = OnAudioFinished; }
				data.onAudioFinished = OnAudioFinished;
				return data;
			}
		}

		private void CacheAudioData(SfxPlayingData data) {
			/*if (mCacheList.Contains(data)) {
				Log.errt("SoundManager", "SoundManager.CacheAudioSource can't cache again.");
				return;
			}*/
			data.Clear();
			mCacheList.Enqueue(data);
			data.source.gameObject.SetActive(false);
		}

		public virtual void ReleaseUnused() {
			foreach (var data in mCacheList) {
				if (data.source != null) {
					Object.Destroy(data.source.gameObject);
				}
			}
			mCacheList.Clear();
		}

		private AudioMixerGroup FindAudioMixerGroup(string path) {
			AudioMixerGroup[] groups = mMasterMixer.FindMatchingGroups(path);
			for (int i = 0, imax = groups != null ? groups.Length : 0; i < imax; i++) {
				AudioMixerGroup group = groups[i];
				if (path.EndsWith(group.name)) {
					return group;
				}
			}
			return null;
		}

		private void OnUpdate(float delta) {
			for (int i = mFadingList.Count - 1; i >= 0; i--) {
				SfxPlayingData data = mFadingList[i];
				if (data.fadingStatus == eFadingStatus.None) {
					mFadingList.RemoveAt(i);
					continue;
				}
				if (data.UpdateFade(delta)) {
					mFadingList.RemoveAt(i);
				}
			}
			if (!float.IsNaN(mVolumeMusicLimitTimer)) {
				mVolumeMusicLimitTimer += delta;
				float t = mVolumeMusicLimitTimer * mVolumeMusicLimitDuration;
				if (t >= 1f) { mVolumeMusicLimitTimer = float.NaN; }
				t = Mathf.Sin(Mathf.PI * (Mathf.Clamp01(t) - 0.5f)) * 0.5f + 0.5f;
				mVolumeMusicLimit = Mathf.LerpUnclamped(mVolumeMusicLimitFrom, mVolumeMusicLimitTo, t);
				ApplyMusicVolume(mVolumeMusic);
			}
		}

		private enum eFadingStatus { None, Fading, FadingPause, FadingStop }

		private class SfxPlayingData {
			public static IAudioClipLoader loader;
			public readonly AudioSource source;
			public int id { get; private set; }
			public string folder { get; private set; }
			public string sfx { get; private set; }
			private float mVolume;
			public bool voice { get; private set; }
			private float mFadeIn;
			private float mStartTime;
			private Action<int> mCallback;

			public void Init(int id, string folder, string sfx, float volume, bool voice, float fadein, float starttime, Action<int> callback) {
				this.id = id;
				this.folder = folder;
				this.sfx = sfx;
				mVolume = volume;
				this.voice = voice;
				mFadeIn = fadein;
				mStartTime = starttime;
				mCallback = callback;
			}
			public eFadingStatus fadingStatus { get; private set; }
			public float timer;
			public float duration;
			public float volumeFrom;
			public float volumeTo;

			public Func<SfxPlayingData, AudioClip, bool> onLoaded;
			public Action<SfxPlayingData> onBeginFading;
			public Action<SfxPlayingData, bool> onAudioFinished;

			private ulong mTimerId;
			private Action<AudioClip> mOnAudioLoaded;
			private Timer.TimerDelegate mOnLoopAudioTimer;
			private Timer.TimerDelegate mOnAudioFinished;

			public SfxPlayingData(AudioSource source) {
				this.source = source;
				mOnAudioLoaded = OnAudioLoaded;
				mOnLoopAudioTimer = OnLoopAudioTimer;
				mOnAudioFinished = OnAudioFinished;
			}
			public void Load() {
				loader.LoadAudio(folder, sfx, mOnAudioLoaded);
			}
			private void OnAudioLoaded(AudioClip clip) {
				if (!onLoaded(this, clip)) {
					loader.ReleaseAudio(clip);
					return;
				}
#if UNITY_EDITOR
				source.name = clip.name;
#endif
				float delay = mStartTime - Time.unscaledTime;
				source.clip = clip;
				if (delay > 0f) {
					source.PlayDelayed(delay);//.Play((ulong)Mathf.RoundToInt(delay * 44100));
				} else {
					source.Play();
					delay = 0f;
				}
				if (source.loop) {
					if (mCallback != null) {
						mTimerId = Timer.Register(clip.length + delay, mOnLoopAudioTimer);
					}
				} else {
					mTimerId = Timer.Register(clip.length + delay, mOnAudioFinished);
				}
				if (mFadeIn > 0f) {
					source.volume = 0f;
					fadingStatus = eFadingStatus.Fading;
					timer = -delay;
					duration = 1f / mFadeIn;
					volumeFrom = 0f;
					volumeTo = mVolume;
					onBeginFading(this);
				}
			}

			public void StopAudio(bool manuallyStop) {
				source.Stop();
				if (manuallyStop) {
					Timer.Unregister(mTimerId);
				} else {
					Action<int> func = mCallback;
					if (func != null) {
						try { func(id); } catch (Exception e) { Debug.LogException(e); }
					}
				}
			}

			public void Pause(bool fade) {
				if (!fade) {
					source.Pause();
					return;
				}
				fadingStatus = eFadingStatus.FadingPause;
				timer = 0f;
				duration = 1f / MUSIC_FADE_DURATION;
				volumeFrom = source.volume;
				volumeTo = 0f;
				Timer.Unregister(mTimerId);
				onBeginFading(this);
			}

			public void Resume(bool fade) {
				source.UnPause();
				if (fade) {
					fadingStatus = eFadingStatus.Fading;
					timer = 0f;
					duration = 1f / MUSIC_FADE_DURATION;
					volumeFrom = source.volume;
					volumeTo = mVolume;
					onBeginFading(this);
				} else {
					source.volume = mVolume;
				}
				if (source.clip != null) {
					if (source.loop) {
						if (mCallback != null) {
							mTimerId = Timer.Register(source.clip.length - source.time, mOnLoopAudioTimer);
						}
					} else {
						mTimerId = Timer.Register(source.clip.length - source.time, mOnAudioFinished);
					}
				}
			}

			public void FadeOutStop(float fadeout) {
				if (fadeout <= 0f) {
					onAudioFinished(this, true);
					return;
				}
				fadingStatus = eFadingStatus.FadingStop;
				timer = 0f;
				duration = 1f / fadeout;
				volumeFrom = source.volume;
				volumeTo = 0f;
				onBeginFading(this);
			}

			public bool UpdateFade(float delta) {
				timer += delta;
				float t = timer * duration;
				bool done = t >= 1f;
				t = Mathf.Sin(Mathf.PI * (Mathf.Clamp01(t) - 0.5f)) * 0.5f + 0.5f;
				source.volume = Mathf.LerpUnclamped(volumeFrom, volumeTo, t);
				if (done) {
					switch (fadingStatus) {
						case eFadingStatus.FadingPause:
							source.Pause();
							break;
						case eFadingStatus.FadingStop:
							onAudioFinished(this, true);
							break;
					}
					fadingStatus = eFadingStatus.None;
					return true;
				}
				return false;
			}

			public void Clear() {
				AudioClip clip = source.clip;
				if (clip != null) {
					source.clip = null;
					loader.ReleaseAudio(clip);
				}
				id = -1;
				mCallback = null;
				fadingStatus = eFadingStatus.None;
				folder = null;
				sfx = null;
			}

			private void OnLoopAudioTimer() {
				mTimerId = Timer.Register(source.clip.length - source.time, mOnLoopAudioTimer);
				Action<int> func = mCallback;
				if (func != null) {
					try { func(id); } catch (Exception e) { Debug.LogException(e); }
				}
			}

			private void OnAudioFinished() {
				onAudioFinished(this, false);
			}

		}

	}

}