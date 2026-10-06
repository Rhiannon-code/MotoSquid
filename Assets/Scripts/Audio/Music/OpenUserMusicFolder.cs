using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;



namespace MotoSquid.Audio
{
    public class OpenUserMusicFolder : MonoBehaviour
    {
        public void OpenFolder()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "UserMusic");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);


#if UNITY_STANDALONE || UNITY_EDITOR
            Application.OpenURL("file://" + path);
#else
            Debug.Log($"[OpenUserMusicFolder] User music folder is at: {path} (opening a folder is unsupported on this platform).");
#endif
        }
    }
}
