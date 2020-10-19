using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GreatClock.Common.Sound {

	public static class Audio3DSettingsCreator {

		[MenuItem("GreatClock/Sound && Camera/Create Audio3D Settings")]
		static void CreateAudio3DSettings() {
			string path = EditorUtility.SaveFilePanelInProject("Audio3DSettings", "Audio3DSettings", "asset", "");
			if (string.IsNullOrEmpty(path)) { return; }
			if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) {
				EditorUtility.DisplayDialog("Audio3DSettings",
					string.Format("An asset file has already existed at '{0}' !", path), "OK");
				return;
			}
			Audio3DSettings settings = ScriptableObject.CreateInstance<Audio3DSettings>();
			AssetDatabase.CreateAsset(settings, path);
			AssetDatabase.SaveAssets();
			Debug.LogWarning(path);
		}

	}

}
