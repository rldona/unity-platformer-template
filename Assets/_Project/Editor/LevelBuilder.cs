using System.Linq;
using Platformer.Levels;
using Platformer.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Shared scene construction for level builders: ground, one-way platform and hazard
    /// tilemaps, player, checkpoints, goal, moving platforms, kill zones, and a pixel-perfect
    /// Cinemachine camera confined to the level. Coordinates are in tiles (1 tile = 1 u).
    /// Usage: Begin, paint and place things, Finish.
    /// </summary>
    public sealed class LevelBuilder
    {
        // 320x180 reference resolution at 16 PPU.
        private const int ReferenceWidth = 320;
        private const int ReferenceHeight = 180;
        private const float OrthographicSize = ReferenceHeight / (2f * PlaceholderTextures.PixelsPerUnit);
        // Moving platforms are 3x1 tiles, matching their placeholder sprite.
        private static readonly Vector2 MovingPlatformSize = new Vector2(3f, 1f);

        private readonly string scenePath;
        private readonly Scene scene;
        private readonly GameObject playerPrefab;
        private readonly int groundLayer;
        private readonly Tilemap ground;
        private readonly Tilemap platforms;
        private readonly Tilemap hazards;
        private readonly TileBase groundTile;
        private readonly TileBase platformTile;
        private readonly TileBase spikeTile;
        private GameObject player;

        private LevelBuilder(string scenePath, Scene scene, GameObject playerPrefab, int groundLayer)
        {
            this.scenePath = scenePath;
            this.scene = scene;
            this.playerPrefab = playerPrefab;
            this.groundLayer = groundLayer;

            groundTile = PlaceholderTileGenerator.GetOrGenerate();
            platformTile = LevelArtGenerator.GetPlatformTile();
            spikeTile = LevelArtGenerator.GetSpikeTile();

            var grid = new GameObject("Grid").AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            ground = CreateSolidTilemap(grid, "Ground", oneWay: false);
            platforms = CreateSolidTilemap(grid, "OneWayPlatforms", oneWay: true);
            hazards = CreateHazardTilemap(grid);
        }

        /// <summary>Starts a new empty scene with light and tilemaps. Returns null if prerequisites are missing.</summary>
        public static LevelBuilder Begin(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return null;

            // Create the scene first: NewScene unloads unused assets, which would
            // invalidate tile/prefab references loaded before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var groundLayer = LayerMask.NameToLayer("Ground");
            var playerPrefab = PlayerPrefabBuilder.GetOrBuild();
            if (groundLayer < 0 || playerPrefab == null)
            {
                Debug.LogError($"Platformer: missing 'Ground' layer or player prefab; {scenePath} not built.");
                return null;
            }

            CreateGlobalLight();
            return new LevelBuilder(scenePath, scene, playerPrefab, groundLayer);
        }

        /// <summary>Fills an inclusive rectangle of solid ground tiles.</summary>
        public void Fill(int xMin, int yMin, int xMax, int yMax) => SetTiles(ground, xMin, yMin, xMax, yMax, groundTile);

        /// <summary>Clears an inclusive rectangle of solid ground tiles.</summary>
        public void Clear(int xMin, int yMin, int xMax, int yMax) => SetTiles(ground, xMin, yMin, xMax, yMax, null);

        /// <summary>Floor of the given thickness, side walls and ceiling.</summary>
        public void Shell(int width, int height, int floorTop)
        {
            Fill(0, 0, width - 1, floorTop - 1);
            Fill(0, 0, 0, height - 1);
            Fill(width - 1, 0, width - 1, height - 1);
            Fill(0, height - 1, width - 1, height - 1);
        }

        /// <summary>One-way platform tiles on row y: solid from above, passable from below (down + jump drops through).</summary>
        public void AddOneWay(int xMin, int xMax, int y) => SetTiles(platforms, xMin, y, xMax, y, platformTile);

        /// <summary>Spike tiles on row y (put them on the row above the floor).</summary>
        public void AddSpikes(int xMin, int xMax, int y) => SetTiles(hazards, xMin, y, xMax, y, spikeTile);

        /// <summary>Invisible hazard covering the area (min corner + size), e.g. the bottom of a pit.</summary>
        public void AddKillZone(Rect area)
        {
            var go = new GameObject("KillZone") { layer = LayerMask.NameToLayer("Ignore Raycast") };
            go.transform.position = area.center;
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = area.size;
            go.AddComponent<Hazard>();
        }

        /// <summary>Checkpoint flag standing at the given feet position; it becomes the respawn point when touched.</summary>
        public void AddCheckpoint(Vector2 feet)
        {
            var go = CreateFlag("Checkpoint", feet, LevelArtGenerator.GetCheckpoint());
            var checkpoint = go.AddComponent<Checkpoint>();
            checkpoint.Flag = go.GetComponent<SpriteRenderer>();
        }

        /// <summary>End-of-level flag; touching it loads nextScene.</summary>
        public void AddGoal(Vector2 feet, string nextScene)
        {
            var go = CreateFlag("Goal", feet, LevelArtGenerator.GetGoal());
            go.AddComponent<LevelGoal>().NextScene = nextScene;
        }

        /// <summary>3x1 moving platform centred at start, travelling to start + travel and back.</summary>
        public void AddMovingPlatform(Vector2 start, Vector2 travel, float speed)
        {
            var go = new GameObject("MovingPlatform") { layer = groundLayer };
            go.transform.position = start;
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = LevelArtGenerator.GetMovingPlatform();
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            go.AddComponent<BoxCollider2D>().size = MovingPlatformSize;
            var platform = go.AddComponent<MovingPlatform>();
            platform.Travel = travel;
            platform.Speed = speed;
        }

        /// <summary>Player instance plus the PlayerSpawn point used for respawns until a checkpoint is touched.</summary>
        public void PlacePlayer(Vector2 spawn)
        {
            var spawnPoint = new GameObject("PlayerSpawn").transform;
            spawnPoint.position = spawn;

            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = spawn;
            if (player.TryGetComponent<PlayerRespawn>(out var respawn))
            {
                respawn.SpawnPoint = spawnPoint;
                PrefabUtility.RecordPrefabInstancePropertyModifications(respawn);
            }
        }

        /// <summary>Bakes collision, adds camera and bounds, saves the scene and registers it for builds.</summary>
        public void Finish(bool firstInBuild = false)
        {
            // Composites cache their geometry in the scene and are not rebuilt on load,
            // so generate it now, after painting, before the scene is saved.
            foreach (var tilemap in new[] { ground, platforms })
            {
                tilemap.CompressBounds();
                tilemap.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
                tilemap.GetComponent<CompositeCollider2D>().GenerateGeometry();
            }
            hazards.CompressBounds();

            var bounds = CreateCameraBounds(ground);
            if (player != null)
                CreateCameras(player.transform, bounds);

            EditorSceneManager.SaveScene(scene, scenePath);
            AddToBuildSettings(scenePath, firstInBuild);
            Debug.Log($"Platformer: built {scenePath}.");
        }

        private static void SetTiles(Tilemap tilemap, int xMin, int yMin, int xMax, int yMax, TileBase value)
        {
            for (var x = xMin; x <= xMax; x++)
                for (var y = yMin; y <= yMax; y++)
                    tilemap.SetTile(new Vector3Int(x, y, 0), value);
        }

        private static void CreateGlobalLight()
        {
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;
        }

        /// <summary>
        /// Static tilemap whose tiles merge into one composite, so the player never catches on
        /// internal tile edges. One-way tilemaps add a PlatformEffector2D on the composite.
        /// </summary>
        private Tilemap CreateSolidTilemap(Grid grid, string name, bool oneWay)
        {
            var go = new GameObject(name) { layer = groundLayer };
            go.transform.SetParent(grid.transform, false);
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            var tilemapCollider = go.AddComponent<TilemapCollider2D>();
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

            if (oneWay)
            {
                composite.usedByEffector = true;
                var effector = go.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 160f;
                effector.useSideFriction = false;
                effector.useSideBounce = false;
            }
            return tilemap;
        }

        private static Tilemap CreateHazardTilemap(Grid grid)
        {
            var go = new GameObject("Hazards") { layer = LayerMask.NameToLayer("Ignore Raycast") };
            go.transform.SetParent(grid.transform, false);
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = 1;
            go.AddComponent<TilemapCollider2D>().isTrigger = true;
            go.AddComponent<Hazard>();
            return tilemap;
        }

        private static GameObject CreateFlag(string name, Vector2 feet, Sprite sprite)
        {
            var go = new GameObject(name) { layer = LayerMask.NameToLayer("Ignore Raycast") };
            go.transform.position = feet;
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 5;
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1f, 2f);
            trigger.offset = new Vector2(0f, 1f);
            return go;
        }

        /// <summary>Polygon matching the ground tilemap bounds, used by the Cinemachine confiner.</summary>
        private static PolygonCollider2D CreateCameraBounds(Tilemap tilemap)
        {
            var go = new GameObject("CameraBounds") { layer = LayerMask.NameToLayer("Ignore Raycast") };
            var polygon = go.AddComponent<PolygonCollider2D>();
            polygon.isTrigger = true;

            var b = tilemap.localBounds;
            var offset = (Vector2)tilemap.transform.position;
            polygon.SetPath(0, new[]
            {
                offset + new Vector2(b.min.x, b.min.y),
                offset + new Vector2(b.min.x, b.max.y),
                offset + new Vector2(b.max.x, b.max.y),
                offset + new Vector2(b.max.x, b.min.y),
            });
            return polygon;
        }

        private static void CreateCameras(Transform target, Collider2D bounds)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(target.position.x, target.position.y, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(24, 20, 28, 255);
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.AddComponent<CinemachineBrain>();

            var pixelPerfect = cameraObject.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = PlaceholderTextures.PixelsPerUnit;
            pixelPerfect.refResolutionX = ReferenceWidth;
            pixelPerfect.refResolutionY = ReferenceHeight;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            // Crop both axes so the 320x180 image is always scaled by an integer factor.
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;

            var vcamObject = new GameObject("CM Player Camera");
            vcamObject.transform.position = cameraObject.transform.position;
            var vcam = vcamObject.AddComponent<CinemachineCamera>();
            vcam.Follow = target;
            var lens = vcam.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = OrthographicSize;
            vcam.Lens = lens;

            var composer = vcamObject.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.TargetOffset = new Vector3(0f, 1f, 0f); // aim at the body, not the feet
            composer.Damping = new Vector3(0.4f, 0.6f, 0f);

            var confiner = vcamObject.AddComponent<CinemachineConfiner2D>();
            confiner.BoundingShape2D = bounds;

            vcamObject.AddComponent<CinemachinePixelPerfect>();
        }

        private static void AddToBuildSettings(string path, bool first)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            var entry = new EditorBuildSettingsScene(path, true);
            if (first)
                scenes.Insert(0, entry);
            else
                scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
