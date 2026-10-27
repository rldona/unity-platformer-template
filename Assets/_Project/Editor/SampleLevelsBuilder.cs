using UnityEditor;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Builds the two sample levels. Level01 walks through every piece (spikes, checkpoints,
    /// one-way platforms, a pit crossed on a moving platform, goal); Level02 adds a vertical
    /// lift and loops back to Level01. Coordinates are in tiles; the floor surface is at y = 3.
    /// With the default tuning a full jump reaches 3.5 tiles up and about 4 tiles across.
    /// </summary>
    public static class SampleLevelsBuilder
    {
        public const string Level01 = "Level01";
        public const string Level02 = "Level02";
        private const string ScenesFolder = "Assets/_Project/Scenes/";
        private const int FloorTop = 3;

        [MenuItem("Platformer/Build Sample Levels")]
        public static void BuildAll()
        {
            BuildLevel02();
            BuildLevel01();
        }

        [MenuItem("Platformer/Build Level 01")]
        public static void BuildLevel01()
        {
            const int width = 64;
            const int height = 18;
            var level = LevelBuilder.Begin(ScenesFolder + Level01 + ".unity");
            if (level == null)
                return;

            level.Shell(width, height, FloorTop);
            level.PlacePlayer(new Vector2(3.5f, FloorTop));

            // Three spikes: a short hop clears them.
            level.AddSpikes(9, 11, FloorTop);
            level.AddCheckpoint(new Vector2(15.5f, FloorTop));

            // Stair of one-way platforms, each 3 tiles above the previous one.
            level.AddOneWay(18, 21, FloorTop + 2);
            level.AddOneWay(23, 26, FloorTop + 5);
            level.AddOneWay(28, 31, FloorTop + 8);

            // 8-tile bottomless pit, too wide to jump: ride the moving platform across.
            level.Clear(36, 0, 43, FloorTop - 1);
            level.AddKillZone(new Rect(36f, 0f, 8f, 1.5f));
            level.AddMovingPlatform(new Vector2(37.5f, FloorTop - 0.5f), new Vector2(5f, 0f), 2.5f);

            level.AddCheckpoint(new Vector2(47.5f, FloorTop));

            // Spikes too long to jump, with a one-way bridge over them (down + jump drops onto the spikes).
            level.AddSpikes(51, 55, FloorTop);
            level.AddOneWay(50, 56, FloorTop + 2);

            level.AddGoal(new Vector2(61.5f, FloorTop), Level02);
            level.Finish(firstInBuild: true);
        }

        [MenuItem("Platformer/Build Level 02")]
        public static void BuildLevel02()
        {
            const int width = 48;
            const int height = 24;
            const int ledgeTop = 15;
            var level = LevelBuilder.Begin(ScenesFolder + Level02 + ".unity");
            if (level == null)
                return;

            level.Shell(width, height, FloorTop);
            level.PlacePlayer(new Vector2(3.5f, FloorTop));

            // Warm-up: two stacked one-way platforms.
            level.AddOneWay(5, 8, FloorTop + 2);
            level.AddOneWay(10, 13, FloorTop + 5);

            // A strip of spikes crossed on a horizontal platform; its top is 3 tiles up, so
            // board it with a full jump from the floor.
            level.AddSpikes(18, 27, FloorTop);
            level.AddMovingPlatform(new Vector2(19.5f, FloorTop + 2.5f), new Vector2(8f, 0f), 2.5f);

            level.AddCheckpoint(new Vector2(29.5f, FloorTop));

            // Lift flush against the high ledge: it rises from one tile above the floor to the ledge top.
            level.Fill(34, FloorTop, width - 2, ledgeTop - 1);
            level.AddMovingPlatform(new Vector2(32.5f, FloorTop + 0.5f), new Vector2(0f, ledgeTop - FloorTop - 1), 2.5f);

            level.AddGoal(new Vector2(44.5f, ledgeTop), Level01);
            level.Finish();
        }
    }
}
