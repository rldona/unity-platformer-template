using UnityEngine;

namespace Platformer.Cameras
{
    /// <summary>
    /// Renders full screen at the display's own resolution. The player otherwise reuses the
    /// last windowed size (saved between runs) when going full screen, so a 1280x720 window
    /// becomes a 16:9 image with black bars on a 16:10 display. Created automatically at startup;
    /// the Pixel Perfect Camera then fills the extra space with more of the level.
    /// </summary>
    public sealed class FullscreenNativeResolution : MonoBehaviour
    {
        private bool wasFullScreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Application.isEditor)
                return;
            var go = new GameObject(nameof(FullscreenNativeResolution)) { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<FullscreenNativeResolution>();
        }

        private void Start() => Apply();

        private void Update()
        {
            // Only on entering full screen, so a mode the display refuses can't loop.
            if (Screen.fullScreen != wasFullScreen)
                Apply();
        }

        private void Apply()
        {
            wasFullScreen = Screen.fullScreen;
            if (!Screen.fullScreen)
                return;
            var native = Screen.mainWindowDisplayInfo;
            if (native.width > 0 && (Screen.width != native.width || Screen.height != native.height))
                Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
        }
    }
}
