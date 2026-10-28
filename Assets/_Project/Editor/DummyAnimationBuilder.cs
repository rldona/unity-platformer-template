using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Builds the player's animations from the Dummy character in Assets/Characters/Dummy:
    /// sets up the Aseprite importer (PPU 16, pivot bottom-center on the canvas, Point, no compression),
    /// cuts one clip per PlayerController state out of the imported frames and saves an Animator Controller
    /// with a state per clip. Frame durations come from the .aseprite files.
    /// Airborne poses drawn mid-air on the canvas are moved down onto the feet, because physics
    /// already moves the body.
    /// </summary>
    public static class DummyAnimationBuilder
    {
        public const string Folder = "Assets/Characters/Dummy";
        public const string ClipsFolder = Folder + "/Animations";
        public const string ControllerPath = Folder + "/Dummy.controller";

        private const float PixelsPerUnit = 16f;
        private const float ClipFrameRate = 60f;

        private readonly struct ClipSpec
        {
            public readonly string State;
            public readonly string File;
            public readonly int First;
            public readonly int Last;
            public readonly bool Loop;
            public readonly bool SnapToFeet;

            public ClipSpec(string state, string file, int first, int last, bool loop, bool snapToFeet)
            {
                State = state;
                File = file;
                First = first;
                Last = last;
                Loop = loop;
                SnapToFeet = snapToFeet;
            }
        }

        // State names match PlayerController.State. Last = -1 means the file's last frame.
        // Air poses are drawn in place: physics moves the body. Jump and Fall play once and hold
        // their last pose; Dummy Fall ends with the landing, so only its two falling poses are used.
        private static readonly ClipSpec[] Clips =
        {
            new ClipSpec("Idle", "Dummy Idle", 0, -1, loop: true, snapToFeet: false),
            new ClipSpec("Run", "Dummy Walk", 0, -1, loop: true, snapToFeet: false),
            new ClipSpec("Jump", "Dummy Jump", 0, -1, loop: false, snapToFeet: false),
            new ClipSpec("Fall", "Dummy Fall", 0, 1, loop: false, snapToFeet: false),
        };

        [MenuItem("Platformer/Build Dummy Animations")]
        public static void BuildMenu()
        {
            var controller = Build();
            if (controller != null)
                EditorGUIUtility.PingObject(controller);
        }

        /// <summary>True when the Dummy .aseprite files are in the project.</summary>
        public static bool IsAvailable => Clips.All(c => File.Exists(AsepritePath(c.File)));

        public static RuntimeAnimatorController GetOrBuild()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            return controller != null ? controller : Build();
        }

        /// <summary>First idle frame, shown in the editor before the Animator runs.</summary>
        public static Sprite GetIdleSprite() => LoadFrame(AsepritePath(Clips[0].File), 0);

        public static AnimatorController Build()
        {
            if (!IsAvailable)
            {
                Debug.LogError($"Platformer: Dummy .aseprite files not found in {Folder}.");
                return null;
            }

            foreach (var file in Clips.Select(c => c.File).Distinct())
                ConfigureImporter(AsepritePath(file));

            if (!AssetDatabase.IsValidFolder(ClipsFolder))
                AssetDatabase.CreateFolder(Folder, "Animations");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            foreach (var child in stateMachine.states)
                stateMachine.RemoveState(child.state);

            for (var i = 0; i < Clips.Length; i++)
            {
                var clip = BuildClip(Clips[i]);
                if (clip == null)
                    return null;
                var state = stateMachine.AddState(Clips[i].State, new Vector3(300f, 60f * i));
                state.motion = clip;
                state.writeDefaultValues = true;
                if (i == 0)
                    stateMachine.defaultState = state;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static string AsepritePath(string file) => $"{Folder}/{file}.aseprite";

        private static void ConfigureImporter(string path)
        {
            var importer = (AsepriteImporter)AssetImporter.GetAtPath(path);
            importer.importMode = FileImportModes.AnimatedSprite;
            importer.layerImportMode = LayerImportModes.MergeFrame;
            importer.pivotSpace = PivotSpaces.Canvas;
            importer.pivotAlignment = SpriteAlignment.BottomCenter;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.generateModelPrefab = false;
            importer.generateAnimationClips = false;

            var platform = importer.GetImporterPlatformSettings(EditorUserBuildSettings.activeBuildTarget);
            platform.name = "DefaultTexturePlatform";
            platform.overridden = false;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetImporterPlatformSettings(platform);

            importer.SaveAndReimport();
        }

        private static Sprite LoadFrame(string path, int frame)
        {
            var name = $"Frame_{frame}";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s => s.name == name);
        }

        private static AnimationClip BuildClip(ClipSpec spec)
        {
            var path = AsepritePath(spec.File);
            using var file = AsepriteReader.ReadFile(path);
            var last = spec.Last < 0 ? file.noOfFrames - 1 : spec.Last;

            var spriteKeys = new ObjectReferenceKeyframe[last - spec.First + 2];
            var offsetCurve = new AnimationCurve();
            var time = 0f;
            for (var frame = spec.First; frame <= last; frame++)
            {
                var sprite = LoadFrame(path, frame);
                if (sprite == null)
                {
                    Debug.LogError($"Platformer: sprite Frame_{frame} missing in {path}.");
                    return null;
                }

                spriteKeys[frame - spec.First] = new ObjectReferenceKeyframe { time = time, value = sprite };
                // Pivot is the canvas bottom, so bounds.min.y is how far the art floats above the feet.
                var lift = spec.SnapToFeet ? Mathf.Round(sprite.bounds.min.y * PixelsPerUnit) / PixelsPerUnit : 0f;
                offsetCurve.AddKey(Constant(time, -lift));
                time += file.frameData[frame].frameDuration / 1000f;
            }

            // Repeat the last frame at the end so it is shown for its full duration.
            spriteKeys[^1] = new ObjectReferenceKeyframe { time = time, value = spriteKeys[^2].value };
            offsetCurve.AddKey(Constant(time, offsetCurve.keys[^1].value));

            var clip = new AnimationClip { name = "Dummy" + spec.State, frameRate = ClipFrameRate };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), spriteKeys);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), offsetCurve);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = spec.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            return SaveClip(clip, $"{ClipsFolder}/{clip.name}.anim");
        }

        private static Keyframe Constant(float time, float value) =>
            new Keyframe(time, value, float.PositiveInfinity, float.PositiveInfinity);

        /// <summary>Overwrites an existing clip in place so its GUID (and every reference to it) survives.</summary>
        private static AnimationClip SaveClip(AnimationClip clip, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(clip, path);
                return clip;
            }
            EditorUtility.CopySerialized(clip, existing);
            Object.DestroyImmediate(clip);
            return existing;
        }
    }
}
