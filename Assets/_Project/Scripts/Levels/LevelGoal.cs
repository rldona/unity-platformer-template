using Platformer.Player;
using UnityEngine;

namespace Platformer.Levels
{
    /// <summary>End of the level: touching it fades out and loads the next scene.</summary>
    public class LevelGoal : PlayerTrigger
    {
        [Tooltip("Scene to load next. It must be in the Build Profiles scene list.")]
        [SerializeField] private string nextScene;

        public string NextScene
        {
            get => nextScene;
            set => nextScene = value;
        }

        protected override void OnPlayerEnter(PlayerController player)
        {
            LevelFlow.Instance.LoadLevel(nextScene, player);
        }
    }
}
