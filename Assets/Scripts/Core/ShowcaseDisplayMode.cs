using UnityEngine;

namespace MotoSquid.Core
{
    // The player reopens in whatever window mode it last used, which beats the Player Settings default, and the settings
    // menu that could change it is disabled. Remove this once that menu is back, or it will override the player's choice
    public static class ShowcaseDisplayMode
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.WindowsPlayer) return;

            var native = Screen.currentResolution;
            Screen.SetResolution(native.width, native.height, FullScreenMode.ExclusiveFullScreen, native.refreshRateRatio);
            Debug.Log($"[ShowcaseDisplayMode] Exclusive fullscreen at {native.width}x{native.height}.");
        }
    }
}
