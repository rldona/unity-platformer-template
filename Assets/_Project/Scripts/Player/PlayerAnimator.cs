using UnityEngine;

namespace Platformer.Player
{
    /// <summary>
    /// Plays the Animator state named after the controller's state (Idle, Run, Jump, Fall)
    /// whenever it changes. The Animator needs one state per name; no transitions or parameters.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    public class PlayerAnimator : MonoBehaviour
    {
        [Tooltip("Animator on the sprite object, with states named Idle, Run, Jump and Fall.")]
        [SerializeField] private Animator animator;

        private static readonly int[] StateHashes =
        {
            Animator.StringToHash(nameof(PlayerController.State.Idle)),
            Animator.StringToHash(nameof(PlayerController.State.Run)),
            Animator.StringToHash(nameof(PlayerController.State.Jump)),
            Animator.StringToHash(nameof(PlayerController.State.Fall)),
        };

        private PlayerController controller;

        private void Reset() => animator = GetComponentInChildren<Animator>();

        private void Awake() => controller = GetComponent<PlayerController>();

        private void OnEnable()
        {
            controller.OnStateChanged.AddListener(Play);
            Play(controller.CurrentState);
        }

        private void OnDisable() => controller.OnStateChanged.RemoveListener(Play);

        private void Play(PlayerController.State state)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.Play(StateHashes[(int)state], 0, 0f);
        }
    }
}
