using Platformer.Player;
using UnityEngine;

namespace Platformer.Levels
{
    /// <summary>Base for triggers that react to the player's body entering them.</summary>
    public abstract class PlayerTrigger : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            // Only the solid body counts, never another trigger on the player.
            if (other.isTrigger || other.attachedRigidbody == null)
                return;
            if (other.attachedRigidbody.TryGetComponent<PlayerController>(out var player))
                OnPlayerEnter(player);
        }

        protected abstract void OnPlayerEnter(PlayerController player);
    }
}
