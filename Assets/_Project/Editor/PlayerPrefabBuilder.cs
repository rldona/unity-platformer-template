using Platformer.Cameras;
using Platformer.Debugging;
using Platformer.Player;
using UnityEditor;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Builds Assets/_Project/Prefabs/Player.prefab from code so every reference is wired.
    /// Re-running overwrites the prefab in place (same GUID), so scene instances update.
    /// </summary>
    public static class PlayerPrefabBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        // 12x32 px body for the placeholder, 12x20 px for the Dummy character; 1 u = 16 px.
        private static readonly Vector2 PlaceholderBodySize = new Vector2(12f, 32f) / 16f;
        private static readonly Vector2 DummyBodySize = new Vector2(12f, 20f) / 16f;

        [MenuItem("Platformer/Build Player Prefab")]
        public static void BuildMenu()
        {
            var prefab = Build();
            if (prefab != null)
                EditorGUIUtility.PingObject(prefab);
        }

        public static GameObject GetOrBuild()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return prefab != null ? prefab : Build();
        }

        public static GameObject Build()
        {
            var playerLayer = LayerMask.NameToLayer("Player");
            var groundLayer = LayerMask.NameToLayer("Ground");
            if (playerLayer < 0 || groundLayer < 0)
            {
                Debug.LogError("Platformer: layers 'Ground' and 'Player' must exist (Project Settings > Tags and Layers).");
                return null;
            }

            // The Dummy character (Assets/Characters/Dummy) when it is in the project, else the placeholder.
            var animations = DummyAnimationBuilder.IsAvailable ? DummyAnimationBuilder.GetOrBuild() : null;
            var sprite = animations != null ? DummyAnimationBuilder.GetIdleSprite() : DummySpriteGenerator.GetOrGenerate();
            var bodySize = animations != null ? DummyBodySize : PlaceholderBodySize;

            var root = new GameObject("Player") { layer = playerLayer };
            try
            {
                var body = root.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                var bodyBox = root.AddComponent<BoxCollider2D>();

                // Visual is moved by PlayerPixelSnap; Sprite below it is free for the Animator.
                var visual = new GameObject("Visual") { layer = playerLayer };
                visual.transform.SetParent(root.transform, false);
                var spriteObject = new GameObject("Sprite") { layer = playerLayer };
                spriteObject.transform.SetParent(visual.transform, false);
                var spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.sortingOrder = 10;

                var controller = root.AddComponent<PlayerController>();
                // Box anchored to the feet: the transform origin is the bottom of the body.
                // Set after adding the controller, whose Reset() writes its own default size.
                bodyBox.size = bodySize;
                bodyBox.offset = new Vector2(0f, bodySize.y * 0.5f);
                var so = new SerializedObject(controller);
                so.FindProperty("groundMask").intValue = 1 << groundLayer;
                so.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (animations != null)
                {
                    var animator = spriteObject.AddComponent<Animator>();
                    animator.runtimeAnimatorController = animations;
                    var playerAnimator = root.AddComponent<PlayerAnimator>();
                    var animatorSo = new SerializedObject(playerAnimator);
                    animatorSo.FindProperty("animator").objectReferenceValue = animator;
                    animatorSo.ApplyModifiedPropertiesWithoutUndo();
                }

                var pixelSnap = root.AddComponent<PlayerPixelSnap>();
                var snapSo = new SerializedObject(pixelSnap);
                snapSo.FindProperty("visual").objectReferenceValue = visual.transform;
                snapSo.ApplyModifiedPropertiesWithoutUndo();

                var cameraTarget = new GameObject("CameraTarget");
                cameraTarget.transform.SetParent(root.transform, false);
                var cameraFollow = root.AddComponent<PlayerCameraTarget>();
                var cameraSo = new SerializedObject(cameraFollow);
                cameraSo.FindProperty("target").objectReferenceValue = cameraTarget.transform;
                cameraSo.ApplyModifiedPropertiesWithoutUndo();

                root.AddComponent<PlayerDebugOverlay>();
                root.AddComponent<PlayerRespawn>();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out var success);
                if (!success)
                    Debug.LogError($"Platformer: failed to save {PrefabPath}.");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
