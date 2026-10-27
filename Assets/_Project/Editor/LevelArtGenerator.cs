using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Placeholder art for level pieces: one-way platform tile, spike tile, checkpoint and goal
    /// flags, and the moving platform. Each Get* loads the asset or generates it if missing.
    /// </summary>
    public static class LevelArtGenerator
    {
        private const string Folder = PlaceholderTextures.Folder;
        private const string PlatformSpritePath = Folder + "/PlatformTile.png";
        private const string PlatformTilePath = Folder + "/PlatformTile.asset";
        private const string SpikeSpritePath = Folder + "/SpikeTile.png";
        private const string SpikeTilePath = Folder + "/SpikeTile.asset";
        private const string CheckpointPath = Folder + "/Checkpoint.png";
        private const string GoalPath = Folder + "/Goal.png";
        private const string MovingPlatformPath = Folder + "/MovingPlatform.png";

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Outline = new Color32(30, 34, 46, 255);

        [MenuItem("Platformer/Placeholders/Generate Level Art")]
        public static void GenerateAll()
        {
            GeneratePlatformTile();
            GenerateSpikeTile();
            GenerateCheckpoint();
            GenerateGoal();
            GenerateMovingPlatform();
        }

        public static Tile GetPlatformTile() => Load<Tile>(PlatformTilePath) ?? GeneratePlatformTile();
        public static Tile GetSpikeTile() => Load<Tile>(SpikeTilePath) ?? GenerateSpikeTile();
        public static Sprite GetCheckpoint() => Load<Sprite>(CheckpointPath) ?? GenerateCheckpoint();
        public static Sprite GetGoal() => Load<Sprite>(GoalPath) ?? GenerateGoal();
        public static Sprite GetMovingPlatform() => Load<Sprite>(MovingPlatformPath) ?? GenerateMovingPlatform();

        /// <summary>16x16 tile whose top 5 px are a ledge; the collider is the full cell (one-way).</summary>
        private static Tile GeneratePlatformTile()
        {
            var pixels = PlaceholderTextures.Fill(16, 16, Clear);
            PlaceholderTextures.Rect(pixels, 16, 0, 11, 16, 16, new Color32(84, 148, 176, 255), Outline);
            var sprite = PlaceholderTextures.WriteSprite(PlatformSpritePath, 16, 16, pixels, new Vector2(0.5f, 0.5f));
            return PlaceholderTextures.WriteTile(PlatformTilePath, sprite, Tile.ColliderType.Grid);
        }

        /// <summary>Two 8 px spikes, 10 px tall. The tile uses the sprite's physics shape, so the hitbox follows the points.</summary>
        private static Tile GenerateSpikeTile()
        {
            var pixels = PlaceholderTextures.Fill(16, 16, Clear);
            var fill = new Color32(214, 72, 84, 255);
            for (var x = 0; x < 16; x++)
            {
                var local = x % 8;
                var height = Mathf.RoundToInt(10f - Mathf.Abs(local - 3.5f) * 2.5f);
                for (var y = 0; y < height; y++)
                {
                    // Outline only the slopes and tip; the base sits flush on the floor.
                    var edge = y == height - 1 || y >= Mathf.RoundToInt(10f - Mathf.Abs(local - 3.5f + Mathf.Sign(local - 3.5f)) * 2.5f);
                    pixels[y * 16 + x] = edge ? Outline : fill;
                }
            }
            var sprite = PlaceholderTextures.WriteSprite(SpikeSpritePath, 16, 16, pixels, new Vector2(0.5f, 0.5f));
            return PlaceholderTextures.WriteTile(SpikeTilePath, sprite, Tile.ColliderType.Sprite);
        }

        /// <summary>16x32 flag on a pole, drawn in light grey so the Checkpoint component can tint it.</summary>
        private static Sprite GenerateCheckpoint() =>
            Flag(CheckpointPath, new Color32(236, 240, 248, 255), new Color32(200, 204, 214, 255));

        private static Sprite GenerateGoal() =>
            Flag(GoalPath, new Color32(246, 204, 70, 255), new Color32(70, 74, 88, 255));

        private static Sprite Flag(string path, Color32 cloth, Color32 pole)
        {
            var pixels = PlaceholderTextures.Fill(16, 32, Clear);
            PlaceholderTextures.Rect(pixels, 16, 3, 0, 5, 30, pole, pole);
            PlaceholderTextures.Rect(pixels, 16, 5, 20, 14, 29, cloth, Outline);
            return PlaceholderTextures.WriteSprite(path, 16, 32, pixels, new Vector2(0.5f, 0f));
        }

        /// <summary>48x16 block (3 tiles wide), pivot in the centre.</summary>
        private static Sprite GenerateMovingPlatform()
        {
            var pixels = PlaceholderTextures.Fill(48, 16, Clear);
            PlaceholderTextures.Rect(pixels, 48, 0, 0, 48, 16, new Color32(150, 156, 176, 255), Outline);
            PlaceholderTextures.Rect(pixels, 48, 1, 12, 47, 15, new Color32(196, 202, 220, 255), new Color32(196, 202, 220, 255));
            return PlaceholderTextures.WriteSprite(MovingPlatformPath, 48, 16, pixels, new Vector2(0.5f, 0.5f));
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null ? asset : null;
        }
    }
}
