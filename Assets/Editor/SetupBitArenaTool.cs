#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using KernelKnight.Player;

namespace KernelKnight.Editor
{
    [InitializeOnLoad]
    public static class SetupBitArenaTool
    {
        private const string PrefKey = "BitArena_Setup_V48";

        // Exact cut boundaries at the empty transparent gaps between characters (trimmed to used frames only)
        private static readonly Dictionary<string, int[]> PreciseCuts = new Dictionary<string, int[]>
        {
            { "DASH.png", new int[] { 0, 241, 427, 630 } },
            { "DEATH.png", new int[] { 0, 239, 454, 672, 885, 1086, 1295, 1506, 1718, 1935, 2172 } },
            { "FALL.png", new int[] { 0, 211 } },
            { "HEAL.png", new int[] { 0, 181, 404, 627, 855, 1072, 1280, 1510, 1752, 1945, 2172 } },
            { "HURT.png", new int[] { 0, 164, 333, 512, 694, 874, 1052, 1244, 1435, 1608, 1774 } },
            { "IDLE.png", new int[] { 0, 248, 462, 675, 891, 1105, 1288, 1508, 1717, 1932, 2172 } },
            { "JUMP.png", new int[] { 0, 202, 413, 623, 835 } },
            { "LAND.png", new int[] { 0, 236, 452, 680, 899, 1099, 1307, 1530, 1731, 1941, 2172 } },
            { "LOOKUP.png", new int[] { 0, 256, 488, 707, 919, 1139, 1348, 1556, 1767, 1964, 2172 } },
            { "PARRY.png", new int[] { 0, 269, 618, 903, 1284 } },
            { "POGO.png", new int[] { 0, 238, 595, 949, 1245 } },
            { "RUN.png", new int[] { 0, 248, 468, 683, 896, 1114, 1328, 1547, 1755, 1951, 2172 } },
            { "SLASH.png", new int[] { 0, 179, 426, 628, 778 } },
            { "SLASHSTART.png", new int[] { 0, 892 } },
            { "SLASH_2.png", new int[] { 0, 797 } },
            { "SLASH 1.png", new int[] { 0, 2085 } },
            { "SLASH_4.png", new int[] { 0, 1184 } },
            { "SLASHEND.png", new int[] { 0, 2280 } },
            { "SLASH_6.png", new int[] { 0, 694 } },
            { "UPSLASH.png", new int[] { 0, 278, 598, 965, 1260, 1519 } },
            { "WALK.png", new int[] { 0, 190, 373, 532, 711, 877, 1057, 1220, 1394, 1569, 1774 } }
        };

        // Exact feet pivots
        private static readonly Dictionary<string, float> FeetPivotY = new Dictionary<string, float>
        {
            { "DASH.png", 0.39233f }, // Anchored at DASH_5 apex
            { "DEATH.png", 0.35359f },
            { "FALL.png", 0.32873f }, // Anchored at FALL_5 apex
            { "HEAL.png", 0.29834f },
            { "HURT.png", 0.38331f },
            { "IDLE.png", 0.36464f },
            { "JUMP.png", 0.54972f }, // Anchored at JUMP_5 apex
            { "LAND.png", 0.25829f },
            { "LOOKUP.png", 0.33425f },
            { "POGO.png", 0.26719f }, // Anchored at POGO_5 plunge
            { "RUN.png", 0.35773f },
            { "SLASH.png", 0.38444f },
            { "SLASHSTART.png", 0.02132f },
            { "SLASH_2.png", 0.03462f },
            { "SLASH 1.png", 0.02520f },
            { "SLASH_4.png", 0.04132f },
            { "SLASHEND.png", 0.02517f },
            { "SLASH_6.png", 0.03918f },
            { "WALK.png", 0.40135f }
        };

        private static readonly Dictionary<string, float> FeetPivotX = new Dictionary<string, float>
        {
            { "SLASHSTART.png", 0.70631f },
            { "SLASH_2.png", 0.45044f },
            { "SLASH 1.png", 0.30474f },
            { "SLASH_4.png", 0.30828f },
            { "SLASHEND.png", 0.27837f },
            { "SLASH_6.png", 0.51441f }
        };

        private static readonly Dictionary<string, float[]> FeetPivotYPerFrame = new Dictionary<string, float[]>
        {
            { "JUMP.png", new float[] { 0.28039f, 0.38674f, 0.49586f, 0.54834f } },
            { "PARRY.png", new float[] { 0.01193f, 0.01193f, 0.00955f, 0.01193f } },
            { "POGO.png", new float[] { 0.45589f, 0.29738f, 0.30505f, 0.37925f } },
            { "UPSLASH.png", new float[] { 0.02692f, 0.02277f, 0.02070f, 0.02692f, 0.02277f } }
        };

        private static readonly Dictionary<string, float[]> FeetPivotXPerFrame = new Dictionary<string, float[]>
        {
            { "PARRY.png", new float[] { 0.56134f, 0.47135f, 0.53158f, 0.55512f } },
            { "POGO.png", new float[] { 0.64538f, 0.54902f, 0.53136f, 0.74932f } },
            { "UPSLASH.png", new float[] { 0.56475f, 0.48750f, 0.55586f, 0.68475f, 0.58301f } }
        };

        // Adaptive PPU to ensure Bit's world-space character size is 100% uniform across ALL actions
        private static readonly Dictionary<string, float> TexturePPU = new Dictionary<string, float>
        {
            { "IDLE.png", 100f },
            { "RUN.png", 88f }, // Scales 168px leaning run pose to ~1.91 units matching Idle
            { "DEATH.png", 100f },
            { "FALL.png", 100f },
            { "HEAL.png", 100f },
            { "JUMP.png", 100f },
            { "LAND.png", 100f },
            { "LOOKUP.png", 100f },
            { "SLASH.png", 68.7f }, // Scales 134px attack pose to match 195px Idle
            { "SLASHSTART.png", 478f },
            { "SLASH_2.png", 268f },
            { "SLASH 1.png", 477f },
            { "SLASH_4.png", 249f },
            { "SLASHEND.png", 477f },
            { "SLASH_6.png", 252f },
            { "UPSLASH.png", 138.27f },
            { "PARRY.png", 168.37f },
            { "DASH.png", 62f },
            { "HURT.png", 71.3f },
            { "POGO.png", 162.82f },
            { "WALK.png", 69.2f }
        };

        private static readonly string[] BitFiles = {
            "DASH.png", "DEATH.png", "FALL.png", "HEAL.png", "HURT.png",
            "IDLE.png", "JUMP.png", "JUMP1.png", "LAND.png", "LOOKUP.png",
            "PARRY.png", "POGO.png", "RUN.png", "SLASH.png", "SLASHSTART.png", "SLASH_2.png", "SLASH 1.png", "SLASH_4.png", "SLASHEND.png", "SLASH_6.png", "UPSLASH.png", "WALK.png"
        };

        static SetupBitArenaTool()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorPrefs.GetBool(PrefKey, false))
                {
                    ExecuteFullSetup();
                    EditorPrefs.SetBool(PrefKey, true);
                }
            };
        }

        [MenuItem("Tools/Kernel Knight/1. Setup Bit & Test Arena (Lengkap)", priority = 1)]
        public static void MenuFullSetup()
        {
            ExecuteFullSetup();
        }

        [MenuItem("Tools/Kernel Knight/2. Re-Slice 14 Animasi Bit", priority = 2)]
        public static void MenuSliceAll()
        {
            SliceAllBitTextures();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("<color=green><b>[Kernel Knight]</b> Seluruh 14 Sprite Sheet Bit berhasil dipotong ulang!</color>");
        }

        [MenuItem("Tools/Kernel Knight/3. Re-Build Animator Controller & Clips", priority = 3)]
        public static void MenuBuildClipsAndController()
        {
            var spriteDict = LoadAllBitSprites();
            var controller = BuildAnimatorAndClips(spriteDict);
            Debug.Log("<color=green><b>[Kernel Knight]</b> Seluruh 14 Animasi & Controller berhasil di-generate ulang!</color>");
        }

        [MenuItem("Tools/Kernel Knight/4. Spawn & Refresh Bit di Scene", priority = 4)]
        public static void MenuSpawnBitOnly()
        {
            var spriteDict = LoadAllBitSprites();
            string controllerPath = "Assets/Animation/Bit/Bit_Controller.controller";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            Sprite defaultSprite = (spriteDict.ContainsKey("IDLE") && spriteDict["IDLE"].Length > 0) ? spriteDict["IDLE"][0] : null;

            SetupSceneElements(defaultSprite, controller);
            Debug.Log("<color=green><b>[Kernel Knight]</b> Bit & Test Arena berhasil di-refresh di scene!</color>");
        }

        public static void ExecuteFullSetup()
        {
            Debug.Log("<color=cyan><b>[Kernel Knight]</b> Memulai proses integrasi penuh 14 animasi Bit & Test Arena...</color>");

            // 1. Slice all textures
            SliceAllBitTextures();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Load sprites
            var spriteDict = LoadAllBitSprites();

            // 3. Build animation clips & controller
            var controller = BuildAnimatorAndClips(spriteDict);

            // 4. Setup Scene Elements
            Sprite defaultSprite = (spriteDict.ContainsKey("IDLE") && spriteDict["IDLE"].Length > 0) ? spriteDict["IDLE"][0] : null;
            SetupSceneElements(defaultSprite, controller);

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green><b>[Kernel Knight]</b> SUKSES BESAR! Seluruh 14 Aksi Bit, Animator, Controller, dan Arena Uji Coba berhasil diintegrasikan tanpa error!</color>");
        }

        public static void SliceAllBitTextures()
        {
            foreach (var fileName in BitFiles)
            {
                string assetPath = $"Assets/Animation/Bit/{fileName}";
                if (File.Exists(assetPath))
                {
                    SliceTextureWithFeetPivot(assetPath);
                }
            }
        }

        public static Dictionary<string, Sprite[]> LoadAllBitSprites()
        {
            var spriteDict = new Dictionary<string, Sprite[]>();
            foreach (var fileName in BitFiles)
            {
                string assetPath = $"Assets/Animation/Bit/{fileName}";
                if (File.Exists(assetPath))
                {
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                        .OfType<Sprite>()
                        .OrderBy(s => GetSpriteIndex(s.name))
                        .ToArray();
                    string actionKey = Path.GetFileNameWithoutExtension(fileName).ToUpper();
                    spriteDict[actionKey] = sprites;
                }
            }
            return spriteDict;
        }

        public static RuntimeAnimatorController BuildAnimatorAndClips(Dictionary<string, Sprite[]> spriteDict)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Animation/Bit/Animations"))
            {
                AssetDatabase.CreateFolder("Assets/Animation/Bit", "Animations");
            }

            // Create all 14 Animation Clips with tuned frame rates
            var idleClip   = CreateAnimationClip("Bit_Idle", spriteDict.GetValueOrDefault("IDLE"), 10f, true);
            var runClip    = CreateAnimationClip("Bit_Run", spriteDict.GetValueOrDefault("RUN"), 14f, true);
            var walkClip   = CreateAnimationClip("Bit_Walk", spriteDict.GetValueOrDefault("WALK"), 12f, true);

            // Rising Jump (4 key frames: launch -> rise -> high -> apex) and Dash (3 key frames: start -> burst -> apex)
            var jumpClip   = CreateAnimationClip("Bit_Jump", spriteDict.GetValueOrDefault("JUMP"), 16f, false);
            var fallClip   = CreateAnimationClip("Bit_Fall", spriteDict.GetValueOrDefault("FALL"), 10f, true);
            var dashClip   = CreateAnimationClip("Bit_Dash", spriteDict.GetValueOrDefault("DASH"), 11f, false);

            var rawSlashStart = spriteDict.GetValueOrDefault("SLASHSTART");
            var rawSlash2 = spriteDict.GetValueOrDefault("SLASH_2");
            var rawSlash1 = spriteDict.GetValueOrDefault("SLASH 1");
            var rawSlash4 = spriteDict.GetValueOrDefault("SLASH_4");
            var rawSlashEnd = spriteDict.GetValueOrDefault("SLASHEND");
            var rawSlash6 = spriteDict.GetValueOrDefault("SLASH_6");
            var rawSlash = spriteDict.GetValueOrDefault("SLASH");

            var slashList = new List<Sprite>();
            if (rawSlashStart != null && rawSlashStart.Length > 0) slashList.Add(rawSlashStart[0]);
            if (rawSlash2 != null && rawSlash2.Length > 0) slashList.Add(rawSlash2[0]);
            if (rawSlash1 != null && rawSlash1.Length > 0) slashList.Add(rawSlash1[0]);
            if (rawSlash4 != null && rawSlash4.Length > 0) slashList.Add(rawSlash4[0]);
            if (rawSlashEnd != null && rawSlashEnd.Length > 0) slashList.Add(rawSlashEnd[0]);
            if (rawSlash6 != null && rawSlash6.Length > 0) slashList.Add(rawSlash6[0]);

            Sprite[] slashSprites = (slashList.Count > 0)
                ? slashList.ToArray()
                : ((rawSlash != null && rawSlash.Length >= 4) ? new Sprite[] { rawSlash[1], rawSlash[2], rawSlash[3] } : rawSlash);
            var slashClip  = CreateAnimationClip("Bit_Slash", slashSprites, 18f, false);
            var upSlashClip = CreateAnimationClip("Bit_UpSlash", spriteDict.GetValueOrDefault("UPSLASH"), 10f, false);

            var pogoClip   = CreateAnimationClip("Bit_Pogo", spriteDict.GetValueOrDefault("POGO"), 14f, false);

            // Defensive Parry guard stance (4-frame PARRY.png preferred, fallback to SLASH_0)
            var rawParry = spriteDict.GetValueOrDefault("PARRY");
            Sprite[] parrySprites = (rawParry != null && rawParry.Length > 0)
                ? rawParry
                : ((rawSlash != null && rawSlash.Length >= 1) ? new Sprite[] { rawSlash[0] } : rawSlash);
            var parryClip  = CreateAnimationClip("Bit_Parry", parrySprites, 15f, false);

            var jump1Clip  = CreateAnimationClip("Bit_Jump1", spriteDict.GetValueOrDefault("JUMP1"), 12f, false);
            var landClip   = CreateAnimationClip("Bit_Land", spriteDict.GetValueOrDefault("LAND"), 14f, false);
            var lookupClip = CreateAnimationClip("Bit_LookUp", spriteDict.GetValueOrDefault("LOOKUP"), 14f, false);
            var healClip   = CreateAnimationClip("Bit_Heal", spriteDict.GetValueOrDefault("HEAL"), 10f, true);
            var hurtClip   = CreateAnimationClip("Bit_Hurt", spriteDict.GetValueOrDefault("HURT"), 24f, false);
            var deathClip  = CreateAnimationClip("Bit_Death", spriteDict.GetValueOrDefault("DEATH"), 6f, false);

            // Create Animator Controller
            string controllerPath = "Assets/Animation/Bit/Bit_Controller.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDashing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsParrying", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsLookingUp", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsHealing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsHurting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Slash", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Pogo", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Parry", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            var idleState   = sm.AddState("Idle");   idleState.motion = idleClip; sm.defaultState = idleState;
            var runState    = sm.AddState("Run");    runState.motion = runClip;
            var jumpState   = sm.AddState("Jump");   jumpState.motion = jumpClip;
            var fallState   = sm.AddState("Fall");   fallState.motion = fallClip;
            var landState   = sm.AddState("Land");   landState.motion = landClip;
            var dashState   = sm.AddState("Dash");   dashState.motion = dashClip;
            var slashState  = sm.AddState("Slash");  slashState.motion = slashClip;
            var upSlashState = sm.AddState("UpSlash"); upSlashState.motion = upSlashClip;
            var pogoState   = sm.AddState("Pogo");   pogoState.motion = pogoClip;
            var parryState  = sm.AddState("Parry");  parryState.motion = parryClip;
            var healState   = sm.AddState("Heal");   healState.motion = healClip;
            var lookupState = sm.AddState("LookUp"); lookupState.motion = lookupClip;
            var hurtState   = sm.AddState("Hurt");   hurtState.motion = hurtClip;
            var deathState  = sm.AddState("Death");  deathState.motion = deathClip;

            // Idle <-> Run
            var i2r = idleState.AddTransition(runState);
            i2r.hasExitTime = false; i2r.duration = 0f;
            i2r.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var r2i = runState.AddTransition(idleState);
            r2i.hasExitTime = false; r2i.duration = 0f;
            r2i.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Jump / Fall (shielded from interrupting Attack, Dash, or Parry, and no self-reset!)
            var any2Jump = sm.AddAnyStateTransition(jumpState);
            any2Jump.hasExitTime = false; any2Jump.duration = 0f;
            any2Jump.canTransitionToSelf = false;
            any2Jump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
            any2Jump.AddCondition(AnimatorConditionMode.Greater, 0.1f, "VerticalVelocity");
            any2Jump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");
            any2Jump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            any2Jump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsParrying");
            any2Jump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHurting");
            any2Jump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var any2Fall = sm.AddAnyStateTransition(fallState);
            any2Fall.hasExitTime = false; any2Fall.duration = 0f;
            any2Fall.canTransitionToSelf = false;
            any2Fall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
            any2Fall.AddCondition(AnimatorConditionMode.Less, -0.1f, "VerticalVelocity");
            any2Fall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");
            any2Fall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            any2Fall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsParrying");
            any2Fall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHurting");
            any2Fall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var f2l = fallState.AddTransition(landState);
            f2l.hasExitTime = false; f2l.duration = 0f;
            f2l.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

            var l2i = landState.AddTransition(idleState);
            l2i.hasExitTime = true; l2i.exitTime = 0.6f; l2i.duration = 0.05f;

            // Dash (Packet-Hop) - canTransitionToSelf=false so all 3 dash effect frames play!
            var any2Dash = sm.AddAnyStateTransition(dashState);
            any2Dash.hasExitTime = false; any2Dash.duration = 0f;
            any2Dash.canTransitionToSelf = false;
            any2Dash.AddCondition(AnimatorConditionMode.If, 0, "IsDashing");

            var dash2Idle = dashState.AddTransition(idleState);
            dash2Idle.hasExitTime = false; dash2Idle.duration = 0.05f;
            dash2Idle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");

            // Slash (Cleanse-Needle) - Super responsive & spammable!
            var any2Slash = sm.AddAnyStateTransition(slashState);
            any2Slash.hasExitTime = false; any2Slash.duration = 0f;
            any2Slash.canTransitionToSelf = true;
            any2Slash.AddCondition(AnimatorConditionMode.If, 0, "Slash");

            var slash2Run = slashState.AddTransition(runState);
            slash2Run.hasExitTime = true; slash2Run.exitTime = 0.95f; slash2Run.duration = 0f;
            slash2Run.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var slash2Idle = slashState.AddTransition(idleState);
            slash2Idle.hasExitTime = true; slash2Idle.exitTime = 0.95f; slash2Idle.duration = 0f;
            slash2Idle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // UpSlash (Cleanse-Needle Upward - conditional transitions before Idle fallback)
            var upSlash2Run = upSlashState.AddTransition(runState);
            upSlash2Run.hasExitTime = true; upSlash2Run.exitTime = 0.95f; upSlash2Run.duration = 0f;
            upSlash2Run.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var upSlash2Lookup = upSlashState.AddTransition(lookupState);
            upSlash2Lookup.hasExitTime = true; upSlash2Lookup.exitTime = 0.95f; upSlash2Lookup.duration = 0f;
            upSlash2Lookup.AddCondition(AnimatorConditionMode.If, 0, "IsLookingUp");
            upSlash2Lookup.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var upSlash2Idle = upSlashState.AddTransition(idleState);
            upSlash2Idle.hasExitTime = true; upSlash2Idle.exitTime = 0.95f; upSlash2Idle.duration = 0f;
            upSlash2Idle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLookingUp");
            upSlash2Idle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Pogo Slash - Super responsive!
            var any2Pogo = sm.AddAnyStateTransition(pogoState);
            any2Pogo.hasExitTime = false; any2Pogo.duration = 0f;
            any2Pogo.canTransitionToSelf = true;
            any2Pogo.AddCondition(AnimatorConditionMode.If, 0, "Pogo");

            var pogo2Fall = pogoState.AddTransition(fallState);
            pogo2Fall.hasExitTime = true; pogo2Fall.exitTime = 0.95f; pogo2Fall.duration = 0f;

            // Parry (Dedicated instant guard)
            var any2Parry = sm.AddAnyStateTransition(parryState);
            any2Parry.hasExitTime = false; any2Parry.duration = 0f;
            any2Parry.canTransitionToSelf = true;
            any2Parry.AddCondition(AnimatorConditionMode.If, 0, "Parry");

            var parry2Idle = parryState.AddTransition(idleState);
            parry2Idle.hasExitTime = true; parry2Idle.exitTime = 0.95f; parry2Idle.duration = 0.02f;

            // Look Up
            var i2Lookup = idleState.AddTransition(lookupState);
            i2Lookup.hasExitTime = false; i2Lookup.duration = 0f;
            i2Lookup.AddCondition(AnimatorConditionMode.If, 0, "IsLookingUp");

            var lookup2I = lookupState.AddTransition(idleState);
            lookup2I.hasExitTime = false; lookup2I.duration = 0f;
            lookup2I.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLookingUp");

            var lookup2Run = lookupState.AddTransition(runState);
            lookup2Run.hasExitTime = false; lookup2Run.duration = 0f;
            lookup2Run.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            // Heal (Patching)
            var i2Heal = idleState.AddTransition(healState);
            i2Heal.hasExitTime = false; i2Heal.duration = 0f;
            i2Heal.AddCondition(AnimatorConditionMode.If, 0, "IsHealing");

            var heal2I = healState.AddTransition(idleState);
            heal2I.hasExitTime = false; heal2I.duration = 0.05f;
            heal2I.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHealing");

            // Hurt (blocked while dead, repeatable on consecutive hits)
            var any2Hurt = sm.AddAnyStateTransition(hurtState);
            any2Hurt.hasExitTime = false; any2Hurt.duration = 0f;
            any2Hurt.canTransitionToSelf = true;
            any2Hurt.AddCondition(AnimatorConditionMode.If, 0, "Hurt");
            any2Hurt.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var hurt2Idle = hurtState.AddTransition(idleState);
            hurt2Idle.hasExitTime = false; hurt2Idle.duration = 0.03f;
            hurt2Idle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHurting");

            // Death (slow, dramatic, stays on final frame with NO exit transition)
            var any2Death = sm.AddAnyStateTransition(deathState);
            any2Death.hasExitTime = false; any2Death.duration = 0f;
            any2Death.canTransitionToSelf = false;
            any2Death.AddCondition(AnimatorConditionMode.If, 0, "Death");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void SliceTextureWithFeetPivot(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            string fileName = Path.GetFileName(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width <= 0) width = (fileName == "DASH.png" || fileName == "HURT.png" || fileName == "POGO.png" || fileName == "SLASH.png" || fileName == "WALK.png") ? 1774 : 2172;
            if (height <= 0) height = (fileName == "DASH.png" || fileName == "HURT.png" || fileName == "POGO.png" || fileName == "SLASH.png" || fileName == "WALK.png") ? 887 : 724;

            importer.isReadable = true;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            float ppu = TexturePPU.ContainsKey(fileName) ? TexturePPU[fileName] : 100f;
            importer.spritePixelsToUnits = ppu;
            importer.alphaIsTransparency = true;

            int[] cuts = PreciseCuts.ContainsKey(fileName) ? PreciseCuts[fileName] : null;
            float defaultPivotX = FeetPivotX.ContainsKey(fileName) ? FeetPivotX[fileName] : 0.5f;
            float[] perFramePivotX = FeetPivotXPerFrame.ContainsKey(fileName) ? FeetPivotXPerFrame[fileName] : null;
            float defaultPivotY = FeetPivotY.ContainsKey(fileName) ? FeetPivotY[fileName] : 0.5f;
            float[] perFramePivotY = FeetPivotYPerFrame.ContainsKey(fileName) ? FeetPivotYPerFrame[fileName] : null;
            string baseName = Path.GetFileNameWithoutExtension(path);

            int sliceCount = (cuts != null) ? cuts.Length - 1 : 10;
            var metaList = new List<SpriteMetaData>();
            for (int i = 0; i < sliceCount; i++)
            {
                int x0 = (cuts != null) ? cuts[i] : Mathf.RoundToInt(i * (width / 10f));
                int x1 = (cuts != null) ? cuts[i + 1] : Mathf.RoundToInt((i + 1) * (width / 10f));
                int w = x1 - x0;
                float px = (perFramePivotX != null && i < perFramePivotX.Length) ? perFramePivotX[i] : defaultPivotX;
                float py = (perFramePivotY != null && i < perFramePivotY.Length) ? perFramePivotY[i] : defaultPivotY;

                var meta = new SpriteMetaData
                {
                    name = $"{baseName}_{i}",
                    rect = new Rect(x0, 0, w, height),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(px, py)
                };
                metaList.Add(meta);
            }

            importer.spritesheet = metaList.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static int GetSpriteIndex(string name)
        {
            int underscore = name.LastIndexOf('_');
            if (underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out int idx))
            {
                return idx;
            }
            return 0;
        }

        private static AnimationClip CreateAnimationClip(string name, Sprite[] sprites, float fps, bool loop)
        {
            string path = $"Assets/Animation/Bit/Animations/{name}.anim";
            AnimationClip clip = new AnimationClip { frameRate = fps };

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (sprites != null && sprites.Length > 0)
            {
                var binding = new EditorCurveBinding
                {
                    type = typeof(SpriteRenderer),
                    path = "",
                    propertyName = "m_Sprite"
                };

                var keyframes = new ObjectReferenceKeyframe[sprites.Length];
                for (int i = 0; i < sprites.Length; i++)
                {
                    keyframes[i] = new ObjectReferenceKeyframe
                    {
                        time = i / fps,
                        value = sprites[i]
                    };
                }

                AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            }

            AssetDatabase.CreateAsset(clip, path);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void SetupSceneElements(Sprite defaultBitSprite, RuntimeAnimatorController controller)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path))
            {
                activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            }

            string landPath = "Assets/Animation/land/platform.psd";
            var landSprites = AssetDatabase.LoadAllAssetsAtPath(landPath).OfType<Sprite>().ToArray();

            Sprite p0 = landSprites.FirstOrDefault(s => s.name == "platform_0") ?? landSprites.ElementAtOrDefault(0);
            Sprite p2 = landSprites.FirstOrDefault(s => s.name == "platform_2") ?? landSprites.ElementAtOrDefault(2);

            // Clean up arena, player, and any existing test targets/enemies completely
            while (GameObject.Find("TestArena") != null)
            {
                Object.DestroyImmediate(GameObject.Find("TestArena"));
            }
            while (GameObject.Find("Bit") != null)
            {
                Object.DestroyImmediate(GameObject.Find("Bit"));
            }
            while (GameObject.Find("Dummy_Bug") != null)
            {
                Object.DestroyImmediate(GameObject.Find("Dummy_Bug"));
            }
            while (GameObject.Find("Pogo_TestPad") != null)
            {
                Object.DestroyImmediate(GameObject.Find("Pogo_TestPad"));
            }
            while (GameObject.Find("DummyTarget") != null)
            {
                Object.DestroyImmediate(GameObject.Find("DummyTarget"));
            }
            while (GameObject.Find("AggressiveDummyTarget") != null)
            {
                Object.DestroyImmediate(GameObject.Find("AggressiveDummyTarget"));
            }

            foreach (var d in Object.FindObjectsByType<KernelKnight.Combat.DummyEnemy>(FindObjectsSortMode.None))
            {
                if (d != null) Object.DestroyImmediate(d.gameObject);
            }
            foreach (var p in Object.FindObjectsByType<KernelKnight.Combat.PogoObstacle>(FindObjectsSortMode.None))
            {
                if (p != null) Object.DestroyImmediate(p.gameObject);
            }

            var arena = new GameObject("TestArena");

            // Main floor at y = -3.5 (surface is at -2.22)
            if (p0 != null)
            {
                CreatePlatform(arena.transform, "Floor_Left", p0, new Vector3(-7.6f, -3.5f, 0));
                CreatePlatform(arena.transform, "Floor_Center", p0, new Vector3(0f, -3.5f, 0));
                CreatePlatform(arena.transform, "Floor_Right", p0, new Vector3(7.6f, -3.5f, 0));
            }

            // High Center Platform
            if (p2 != null)
            {
                CreatePlatform(arena.transform, "Platform_HighCenter", p2, new Vector3(0f, 0.2f, 0));
            }

            // Create Bit Character
            var bit = new GameObject("Bit");
            bit.tag = "Player";
            bit.transform.position = new Vector3(0f, -2.22f, 0f);

            var sr = bit.AddComponent<SpriteRenderer>();
            if (defaultBitSprite != null) sr.sprite = defaultBitSprite;
            sr.sortingOrder = 10;

            var anim = bit.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            var rb = bit.AddComponent<Rigidbody2D>();
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.freezeRotation = true;
            rb.gravityScale = 1f;

            var col = bit.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.2f, 1.95f);
            col.offset = new Vector2(0f, 0.975f);
            col.isTrigger = false;

            bit.AddComponent<PlayerInputHandler>();
            bit.AddComponent<PlayerController>();
            bit.AddComponent<PlayerCombatController>();
            bit.AddComponent<PlayerHealthController>();

            var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.transform.position = new Vector3(0, 0f, -10f);
                cam.orthographic = true;
                cam.orthographicSize = 6.5f;
                cam.tag = "MainCamera";
            }

            Selection.activeGameObject = bit;

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        private static GameObject CreatePlatform(Transform parent, string name, Sprite sprite, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 1;

            var col = go.AddComponent<BoxCollider2D>();
            if (sprite != null)
            {
                col.size = sprite.bounds.size;
                col.offset = sprite.bounds.center;
            }
            col.isTrigger = false;

            return go;
        }
    }
}
#endif
