using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 플레이어 리깅 모델(Blender animate_player.py가 내보낸 Player_Rigged.fbx)을 가져오기 설정하고 애니메이터를 만들어요.
    // 동작: Idle·Run(반복), Dash·Attack1~3(한 번). 애니메이터 변수: Speed(float, 이동 속도), Combo(int, 몇 타), Attack·Dash(trigger)
    public static partial class PrototypeSceneBuilder
    {
        const string PlayerRiggedModel = PlayerPartsFolder + "/Player_Rigged.fbx";
        const string PlayerControllerPath = "Assets/_Game/Art/Characters/Player/Player.controller";
        // 평타는 에셋 스토어 Free Sword Animation(EEJANAI,TEAM)의 Humanoid 동작을 옮겨 입혀요. 없으면 직접 만든 Attack1~3을 써요
        const string SwordAnimFolder = "Assets/EEJANAI_Team/FreeSwordAnimations/Animations";
        const string PlayerAnimFolder = "Assets/_Game/Art/Characters/Player/Anims";
        // 에셋 동작에서 게임 평타(1타 세로 · 2타 가로 · 3타 넓은 가로)에 맞는 구간만 잘라 원래 속도로 써요 (초).
        // 구간은 Blender에서 칼끝 궤적을 재서 골랐고, 칼이 게임 판정 시간 안에 지나가요
        static readonly (string src, float from, float to)[] SwordCombo =
        {
            ("slash3", 0f, 0.30f),      // 1타 세로베기
            ("slash1", 0.03f, 0.28f),   // 2타 가로베기
            ("slash4", 0.40f, 0.75f),   // 3타 넓은 가로베기
        };

        // 모델이 없으면 null (그러면 맨몸 파츠로 세워요)
        static RuntimeAnimatorController SetupPlayerRig()
        {
            if (!(AssetImporter.GetAtPath(PlayerRiggedModel) is ModelImporter importer)) return null;

            // Humanoid: 뼈 이름이 Unity 사람형 규칙(Hips, LeftUpperArm, LeftIndexProximal…)이라 자동으로 짝지어지고, 남의 사람형 동작을 옮겨 입힐 수 있어요.
            // Blender 축(Z 위) → Unity 축(Y 위)은 뼈대에 구워 넣어요. 기본 자세는 Blender에서 T자로 맞춰 내보냈어요
            if (importer.animationType != ModelImporterAnimationType.Human || !importer.bakeAxisConversion || !importer.importAnimation)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }

            // Blender 동작 이름은 "Player_Rig|Idle"처럼 들어와서 끝 이름만 써요. Idle·Run은 반복
            var clips = importer.defaultClipAnimations;
            bool changed = importer.clipAnimations.Length != clips.Length;
            foreach (var clip in clips)
            {
                clip.name = clip.takeName.Split('|').Last();
                clip.loopTime = clip.name == "Idle" || clip.name == "Run" || clip.name == "SkillVortex";   // 탁류 소용돌이는 2초 동안 반복
                // 몸통 위치·방향은 동작에 구워 넣어 캐릭터가 제자리에서 움직여요 (이동은 걷기 스크립트가 해요)
                clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
                var current = importer.clipAnimations.FirstOrDefault(c => c.takeName == clip.takeName);
                if (current == null || current.name != clip.name || current.loopTime != clip.loopTime || !current.lockRootPositionXZ) changed = true;
            }
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            var all = AssetDatabase.LoadAllAssetsAtPath(PlayerRiggedModel).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            if (AssetDatabase.LoadAllAssetsAtPath(PlayerRiggedModel).OfType<Avatar>().All(a => !a.isHuman))
                Debug.LogWarning($"{PlayerRiggedModel}의 Humanoid 아바타가 만들어지지 않았어요. 모델 가져오기 설정의 Rig 탭을 확인해 주세요");
            // 칼 평타: 에셋이 있으면 잘라 만든 구간으로 Attack1~3 자리를 바꿔 끼워요
            for (int i = 0; i < SwordCombo.Length; i++)
            {
                var (src, from, to) = SwordCombo[i];
                var cut = CutSwordClip(src, new[] { from, to }, new[] { 0f, to - from }, $"Sword_Attack{i + 1}");
                if (cut != null) all[$"Attack{i + 1}"] = cut;
            }
            // IK로 만든 베기가 있으면 그걸로 (Blender ik_attack.py): 몸은 정면(공격 범위 쪽)을 보고 오른팔이 칼 궤적을 따라가요.
            // 1타 세로 = 에셋 slash3의 칼 궤적, 2타 가로 · 3타 넓은 가로 = 직접 그린 궤적
            for (int i = 1; i <= 3; i++)
                if (all.TryGetValue($"IKAttack{i}", out var ik)) all[$"Attack{i}"] = ik;
            string[] needed = { "Idle", "Run", "Dash", "Attack1", "Attack2", "Attack3" };
            var missing = needed.Where(n => !all.ContainsKey(n)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogWarning($"{PlayerRiggedModel}에 동작이 없어요: {string.Join(", ", missing)} (있는 것: {string.Join(", ", all.Keys)})");
                return null;
            }

            // 애니메이터는 매번 새로 만들어요 (설정은 이 코드가 기준)
            AssetDatabase.DeleteAsset(PlayerControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Combo", AnimatorControllerParameterType.Int);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Skill", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("SkillQuake", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("SkillFlash", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("SkillPalm", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("SkillWave", AnimatorControllerParameterType.Trigger);
            var sm = controller.layers[0].stateMachine;

            // 동작 길이를 게임 시간(초)에 맞춰 재생 속도를 정해요
            AnimatorState State(string name, float seconds)
            {
                var st = sm.AddState(name);
                st.motion = all[name];
                if (seconds > 0f) st.speed = all[name].length / seconds;
                return st;
            }
            var idle = State("Idle", 0f);
            var run = State("Run", 0f);
            run.speed = 1.2f;   // 걷는 속도 6m/s에 다리 박자를 맞춰요
            var dash = State("Dash", 0.18f);
            // 회선참: 칼을 옆으로 뻗고 있는 동안 게임이 몸을 한 바퀴 돌려요 (시전 0.35초). 동작이 없으면 대기 자세로 돌아요
            AnimatorState skill = null;
            if (all.ContainsKey("SkillSpin"))
            {
                skill = State("SkillSpin", 0.35f);
                var toSkill = sm.AddAnyStateTransition(skill);
                toSkill.hasExitTime = false; toSkill.duration = 0.03f; toSkill.canTransitionToSelf = true;
                toSkill.AddCondition(AnimatorConditionMode.If, 0f, "Skill");
            }
            // 붕산권(비급): 왼 주먹을 치켜들었다가 무릎을 굽혀 땅을 내려쳐요. 내려치는 4프레임째가 판정(0.12초)에 오게 0.42초로 재생
            AnimatorState quake = null;
            if (all.ContainsKey("SkillQuake"))
            {
                quake = State("SkillQuake", 0.42f);
                var toQuake = sm.AddAnyStateTransition(quake);
                toQuake.hasExitTime = false; toQuake.duration = 0.03f; toQuake.canTransitionToSelf = true;
                toQuake.AddCondition(AnimatorConditionMode.If, 0f, "SkillQuake");
            }
            // 죽림섬(비급): 0.25초 간격 순간이동 베기 3번에 맞춰 좌우로 번갈아 베어 넘겨요 (25프레임을 그대로, 30fps = 0.83초)
            AnimatorState flash = null;
            if (all.ContainsKey("SkillFlash"))
            {
                flash = State("SkillFlash", 25f / 30f);
                var toFlash = sm.AddAnyStateTransition(flash);
                toFlash.hasExitTime = false; toFlash.duration = 0.03f; toFlash.canTransitionToSelf = true;
                toFlash.AddCondition(AnimatorConditionMode.If, 0f, "SkillFlash");
            }
            // 금강장(비급): 왼손바닥을 허리춤에서 앞으로 내뻗어요. 내뻗는 4프레임째가 판정(0.12초)에 오게 0.42초로 재생
            AnimatorState palm = null;
            if (all.ContainsKey("SkillPalm"))
            {
                palm = State("SkillPalm", 0.42f);
                var toPalm = sm.AddAnyStateTransition(palm);
                toPalm.hasExitTime = false; toPalm.duration = 0.03f; toPalm.canTransitionToSelf = true;
                toPalm.AddCondition(AnimatorConditionMode.If, 0f, "SkillPalm");
            }
            // 탁랑참(비급): 칼을 머리 위 뒤로 치켜들었다가 앞으로 길게 내베어 파도 검기를 보내요. 회선참과 같은 시전 0.35초 (5프레임째가 내베기)
            AnimatorState wave = null;
            if (all.ContainsKey("SkillWave"))
            {
                wave = State("SkillWave", 0.35f);
                var toWave = sm.AddAnyStateTransition(wave);
                toWave.hasExitTime = false; toWave.duration = 0.03f; toWave.canTransitionToSelf = true;
                toWave.AddCondition(AnimatorConditionMode.If, 0f, "SkillWave");
            }
            // 평타는 원래 속도로 재생해요. 끝나면 대기로 넘어가고, 다음 타가 오면 이어 베요
            var attacks = new[] { State("Attack1", 0f), State("Attack2", 0f), State("Attack3", 0f) };
            sm.defaultState = idle;

            var toRun = idle.AddTransition(run);
            toRun.hasExitTime = false; toRun.duration = 0.1f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var toIdle = run.AddTransition(idle);
            toIdle.hasExitTime = false; toIdle.duration = 0.12f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var toDash = sm.AddAnyStateTransition(dash);
            toDash.hasExitTime = false; toDash.duration = 0.03f; toDash.canTransitionToSelf = true;
            toDash.AddCondition(AnimatorConditionMode.If, 0f, "Dash");
            for (int i = 0; i < attacks.Length; i++)
            {
                var t = sm.AddAnyStateTransition(attacks[i]);
                t.hasExitTime = false; t.duration = 0.04f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                t.AddCondition(AnimatorConditionMode.Equals, i + 1, "Combo");
            }
            // 대시·공격이 끝나면: 걷기 스크립트는 공격·대시 중엔 Speed를 0으로 주다가 끝나면 움직임을 풀어요.
            // 그래서 Speed가 오르면 바로 달리기로, 아니면 동작이 끝까지 돈 뒤 대기로
            foreach (var st in attacks.Append(dash).Append(skill).Append(quake).Append(flash).Append(palm).Append(wave).Where(s => s != null))
            {
                var r = st.AddTransition(run);
                r.hasExitTime = false; r.duration = 0.1f;
                r.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                var b = st.AddTransition(idle);
                b.hasExitTime = true; b.exitTime = 1f; b.duration = 0.12f;
            }

            // 탁류 소용돌이(비도 + 탁랑참): 윗몸만 덮어쓰는 층. 비도가 소용돌이치는 동안 InkPlayerAnimator가 무게를 1로 올려요
            //   (다리는 아래 층의 걷기·대기를 그대로 따라요)
            if (all.TryGetValue("SkillVortex", out var vortexClip))
            {
                var mask = new AvatarMask { name = "Player_UpperBody" };
                for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                    mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                        || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                        || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers);
                AssetDatabase.AddObjectToAsset(mask, controller);
                controller.AddLayer("Vortex");
                var layers = controller.layers;
                layers[1].avatarMask = mask;
                layers[1].defaultWeight = 0f;
                layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
                controller.layers = layers;
                var vs = layers[1].stateMachine.AddState("SkillVortex");
                vs.motion = vortexClip;
                layers[1].stateMachine.defaultState = vs;
            }

            AssetDatabase.SaveAssets();
            return controller;
        }

        // 에셋의 Humanoid 동작에서 구간을 잘라 시간을 다시 배치한 새 동작 파일을 만들어요.
        // srcTimes[i] 순간의 자세가 dstTimes[i]에 오도록 그 사이를 고르게 늘이거나 줄여요 (60프레임으로 다시 찍음).
        // 방향은 "몸 방향 기준"으로 구워요: 에셋 동작은 녹화 때 캐릭터가 저마다 다른 쪽을 보고 있어서,
        // 원래 방향을 따르면 오른쪽을 보고 눌러도 엉뚱한 쪽을 베요
        static AnimationClip CutSwordClip(string srcName, float[] srcTimes, float[] dstTimes, string outName)
        {
            var src = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{SwordAnimFolder}/{srcName}.anim");
            if (src == null || !src.humanMotion) return null;
            EnsureFolder(PlayerAnimFolder);
            string path = $"{PlayerAnimFolder}/{outName}.anim";
            AssetDatabase.DeleteAsset(path);
            const float fps = 60f;
            float total = dstTimes[dstTimes.Length - 1];
            int count = Mathf.CeilToInt(total * fps) + 1;
            float SourceTime(float t)
            {
                for (int k = 1; k < dstTimes.Length; k++)
                    if (t <= dstTimes[k] || k == dstTimes.Length - 1)
                        return Mathf.Lerp(srcTimes[k - 1], srcTimes[k], Mathf.InverseLerp(dstTimes[k - 1], dstTimes[k], t));
                return srcTimes[srcTimes.Length - 1];
            }
            var dst = new AnimationClip { frameRate = fps, name = outName };
            foreach (var binding in AnimationUtility.GetCurveBindings(src))
            {
                var curve = AnimationUtility.GetEditorCurve(src, binding);
                var times = new float[count];
                var values = new float[count];
                for (int i = 0; i < count; i++)
                {
                    times[i] = Mathf.Min(total, i / fps);
                    values[i] = curve.Evaluate(SourceTime(times[i]));
                }
                var keys = new Keyframe[count];
                for (int i = 0; i < count; i++)
                {
                    int p = Mathf.Max(0, i - 1), n = Mathf.Min(count - 1, i + 1);
                    float slope = n > p ? (values[n] - values[p]) / Mathf.Max(1e-4f, times[n] - times[p]) : 0f;
                    keys[i] = new Keyframe(times[i], values[i], slope, slope);
                }
                AnimationUtility.SetEditorCurve(dst, binding, new AnimationCurve(keys));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(src);
            settings.startTime = 0f;
            settings.stopTime = total;
            settings.loopTime = false;
            settings.loopBlendOrientation = true;       // 몸 회전은 자세에 구워요 (루트는 안 돌아요)
            settings.keepOriginalOrientation = false;   // 몸 방향 기준
            settings.orientationOffsetY = 0f;
            settings.loopBlendPositionY = true;
            settings.keepOriginalPositionY = true;
            settings.loopBlendPositionXZ = true;        // 앞으로 나가는 건 걷기 스크립트가 해요
            settings.keepOriginalPositionXZ = false;
            AnimationUtility.SetAnimationClipSettings(dst, settings);
            AssetDatabase.CreateAsset(dst, path);
            return dst;
        }
    }
}
