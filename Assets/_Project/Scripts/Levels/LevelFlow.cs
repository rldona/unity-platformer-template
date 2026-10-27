using System.Collections;
using Platformer.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Platformer.Levels
{
    /// <summary>
    /// Screen fades for level changes and respawns. Loading a level fades to black, loads the
    /// scene (each level has its own player at its spawn) and fades back in. A respawn fades
    /// out quickly, moves the player to its checkpoint and fades in. Installs itself once and
    /// survives scene loads.
    /// </summary>
    public sealed class LevelFlow : MonoBehaviour
    {
        private const float LevelFadeOutTime = 0.5f;
        private const float LevelBlackHoldTime = 0.2f;
        private const float LevelFadeInTime = 0.6f;
        private const float RespawnFadeOutTime = 0.25f;
        private const float RespawnBlackHoldTime = 0.1f;
        private const float RespawnFadeInTime = 0.3f;
        private const int FadeSortingOrder = 32000;

        private CanvasGroup fade;

        public static LevelFlow Instance { get; private set; }
        public bool IsBusy { get; private set; }
        public float FadeAlpha => fade.alpha;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("[Level Flow]");
            DontDestroyOnLoad(go);
            go.AddComponent<LevelFlow>();
        }

        private void Awake()
        {
            Instance = this;
            fade = CreateFadeOverlay();
        }

        /// <summary>Fades out, loads the scene (it must be in the build scene list) and fades in.</summary>
        public void LoadLevel(string scene, PlayerController player)
        {
            if (!IsBusy)
                StartCoroutine(RunLoadLevel(scene, player));
        }

        /// <summary>Fades out, moves the player to the position and fades in.</summary>
        public void Respawn(PlayerController player, Vector2 position)
        {
            if (!IsBusy)
                StartCoroutine(RunRespawn(player, position));
        }

        private IEnumerator RunLoadLevel(string scene, PlayerController player)
        {
            IsBusy = true;
            if (player != null)
                player.LockControl(0f);
            yield return Fade(1f, LevelFadeOutTime);

            var load = SceneManager.LoadSceneAsync(scene);
            if (load == null)
            {
                Debug.LogError($"Platformer: scene '{scene}' is not in the build scene list.");
                if (player != null)
                    player.UnlockControl();
            }
            else
            {
                while (!load.isDone)
                    yield return null;
            }

            yield return new WaitForSecondsRealtime(LevelBlackHoldTime);
            yield return Fade(0f, LevelFadeInTime);
            IsBusy = false;
        }

        private IEnumerator RunRespawn(PlayerController player, Vector2 position)
        {
            IsBusy = true;
            player.LockControl(0f);
            yield return Fade(1f, RespawnFadeOutTime);

            player.Respawn(position);
            // Cut the camera straight to the player instead of panning back across the level.
            foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude))
                cam.PreviousStateIsValid = false;

            yield return new WaitForSecondsRealtime(RespawnBlackHoldTime);
            player.UnlockControl();
            yield return Fade(0f, RespawnFadeInTime);
            IsBusy = false;
        }

        private IEnumerator Fade(float target, float duration)
        {
            var start = fade.alpha;
            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                fade.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            fade.alpha = target;
        }

        private CanvasGroup CreateFadeOverlay()
        {
            var canvasObject = new GameObject("Fade Canvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = FadeSortingOrder;

            var group = canvasObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var imageObject = new GameObject("Black");
            imageObject.transform.SetParent(canvasObject.transform, false);
            var image = imageObject.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return group;
        }
    }
}
