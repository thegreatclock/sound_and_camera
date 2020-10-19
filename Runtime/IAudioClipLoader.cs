using System;
using UnityEngine;

namespace GreatClock.Common.Sound {

	public interface IAudioClipLoader {

		void LoadAudio(string folder, string audio, Action<AudioClip> onLoaded);
		void ReleaseAudio(AudioClip clip);

	}

}
