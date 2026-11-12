using System.Collections.Generic;
using System.Linq;
using AThousandDawns.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Move = AThousandDawns.Art.InkEnemyModels.Move;
using MoveMode = AThousandDawns.Art.InkEnemyModels.MoveMode;

namespace AThousandDawns.EditorTools
{
    // 수묵 시험판 적 모델: VARCO로 만들고 자동 리깅한 사람형 적(Blender Enemies/Tools~/prepare_enemy.py가 정리)을 Humanoid로 가져와,
    // 플레이어 동작(대기·달리기·1타 내려찍기)을 옮겨 입혀요. 적은 실행 중에 생겨서 InkEnemyModels가 생길 때 갈아 끼워요.
    // 고수·보스는 패턴마다 다른 동작(플레이어 베기·회전베기·달리기, 모델 고유 동작)을 써요.
    public static partial class PrototypeSceneBuilder
    {
        const string CharactersFolder = "Assets/_Game/Art/Characters";

        // 사람형 적: 폴더 이름(= 파일 앞 이름), 게임 종류, 이동 속도(칸/초, 달리기 동작 빠르기 맞춤)
        static readonly (string name, EnemyKind kind, float moveSpeed)[] InkEnemies =
        {
            ("Grunt", EnemyKind.Grunt, 3.5f),
            ("Archer", EnemyKind.Archer, 3f),
            ("Shield", EnemyKind.Shield, 2.5f),
            ("Horn", EnemyKind.Horn, 3f),
            ("Bomber", EnemyKind.Bomber, 4.5f),
            ("Assassin", EnemyKind.Assassin, 5f),
            ("Nokrim", EnemyKind.Nokrim, 3f),
            ("Qingzhu", EnemyKind.Qingzhu, 5f),
            ("IronFist", EnemyKind.IronFist, 3.5f),
            ("Boar", EnemyKind.Boar, 2.5f),
            ("Hound", EnemyKind.Hound, 5f),
            ("Snake", EnemyKind.Snake, 4f),
            // 폐사찰
            ("Wraith", EnemyKind.Wraith, 2.5f),
            ("Arhat", EnemyKind.Arhat, 1.6f),
            ("Moktak", EnemyKind.Moktak, 2.5f),
            ("Monk", EnemyKind.Monk, 3f),
            ("Lantern", EnemyKind.Lantern, 4f),
            ("Hyegwang", EnemyKind.Hyegwang, 3.5f),   // 폐사찰 고수
            ("BloodBuddha", EnemyKind.BloodBuddha, 1.5f),   // 폐사찰 보스
            // 장강 수채
            ("Pirate", EnemyKind.Pirate, 3.5f),
            ("Harpooner", EnemyKind.Harpooner, 3f),
            ("Anchor", EnemyKind.Anchor, 1.8f),
            ("Rattan", EnemyKind.Rattan, 2.5f),
            ("WaterGhost", EnemyKind.WaterGhost, 2.5f),
            ("FlagBearer", EnemyKind.FlagBearer, 3f),
            ("Takrang", EnemyKind.Takrang, 3.8f),   // 장강 고수
            ("IronDragon", EnemyKind.IronDragon, 2f),   // 장강 보스
            // 천마총
            ("Cultist", EnemyKind.Cultist, 3.5f),
            ("BloodArcher", EnemyKind.BloodArcher, 3f),
            ("BloodShaman", EnemyKind.BloodShaman, 3f),
            ("DemonGuard", EnemyKind.DemonGuard, 2.5f),
            ("BloodBomber", EnemyKind.BloodBomber, 3f),
            ("BloodShadow", EnemyKind.BloodShadow, 4f),   // 천마총 고수
        };

        // 패턴 동작: attack(브레인 AttackName, null이면 다른 동작이 이어 부르는 상태) → state.
        // clips가 여럿이면 차례로 이어져요. seconds > 0이면 한 동작이 그 시간에 끝나도록 빠르기를 맞춰요 (연격 간격).
        // speed는 달리기·되풀이 빠르기 배수. after는 빈틈이 시작될 때 이어 재생할 상태
        struct MoveSpec
        {
            public string attack, state, after, ready;
            public string[] clips;
            public MoveMode mode;
            public float seconds, speed;
        }

        // 모델 고유 동작 중 반복하는 것
        static readonly HashSet<string> LoopingOwnClips = new HashSet<string> { "ChargeRun", "ShoulderRun", "Idle", "Run", "Chant", "Rain", "Drag", "Whirl" };

        // 고유 동작만 쓰는 적 (Generic으로 가져와요)
        static readonly HashSet<string> GenericRigs = new HashSet<string> { "Nokrim", "Qingzhu", "IronFist", "Boar", "Hound", "Snake", "Wraith", "Arhat", "Moktak", "Monk", "Lantern", "Hyegwang", "BloodBuddha", "Pirate", "Harpooner", "Anchor", "Rattan", "WaterGhost", "FlagBearer", "Takrang", "IronDragon", "Cultist", "BloodArcher", "BloodShaman", "DemonGuard", "BloodBomber", "BloodShadow" };   // 동물은 뼈를 직접 심어(prepare_animal.py) 사람 뼈대가 아니에요

        static readonly Dictionary<string, MoveSpec[]> InkEnemyMoves = new Dictionary<string, MoveSpec[]>
        {
            // 녹림왕 (고유 동작): 대도 휘두르기(오른쪽 뒤로 감았다 허리째 가로 베기) · 돌진(낮게 숙인 준비 → 대도를 끌며 달리기) → 끝에서 내려찍기 · 회전베기
            ["Nokrim"] = new[]
            {
                new MoveSpec { attack = "대도 휘두르기", state = "Sweep", clips = new[] { "Sweep" } },
                new MoveSpec { state = "ChargeReady", clips = new[] { "ChargeReady" }, mode = MoveMode.Run },
                new MoveSpec { attack = "돌진", state = "Charge", clips = new[] { "ChargeRun" }, mode = MoveMode.Run, speed = 1.4f, after = "Slam", ready = "ChargeReady" },
                new MoveSpec { state = "Slam", clips = new[] { "Slam" }, mode = MoveMode.Run },   // 양손 내려찍기, 끝 자세로 멈춰 있다가 빈틈이 끝나면 대기로
                // 회전베기(2페이즈): 두 팔을 벌리고 칼끝을 바깥으로 뻗은 자세(Whirl)로 판정 동안 모델째 빙글빙글 (힐윈드처럼)
                new MoveSpec { attack = "회전베기", state = "Spin", clips = new[] { "Whirl" }, mode = MoveMode.Whirl },
            },
            // 폐사찰 — 망령: 혼불은 기본 Attack(손을 당겼다 내밀기). 석상 나한: 합장한 채 낮게 숙였다가 들이받으며 돌진
            ["Arhat"] = new[]
            {
                new MoveSpec { state = "ShoulderReady", clips = new[] { "ShoulderReady" }, mode = MoveMode.Run },
                new MoveSpec { attack = "", state = "Charge", clips = new[] { "ShoulderRun" }, mode = MoveMode.Run, speed = 1.2f, ready = "ShoulderReady" },
                new MoveSpec { state = "Statue", clips = new[] { "Statue" }, mode = MoveMode.Run },   // 깨기 전 잿빛 석상 자세 (InkEnemyAnimator가 IsAwake를 보고)
                new MoveSpec { state = "Wake", clips = new[] { "Wake" } },                            // 깨어나기: 몸을 떨고 두 팔을 떨쳐 돌가루를 털어요
            },
            // 목탁승: 예고(1.5초) 동안 목탁을 다섯 번 두드려요 (그대로 재생)
            //   목탁 가호: 같은 목탁 예고 뒤 둘레 적에게 가호를 씌워요 (같은 동작)
            ["Moktak"] = new[]
            {
                new MoveSpec { attack = "", state = "Knock", clips = new[] { "Knock" }, mode = MoveMode.Play },
                new MoveSpec { attack = "목탁 가호", state = "Bless", clips = new[] { "Knock" }, mode = MoveMode.Play },
            },
            // 파계승: 선장 찌르기. 금강불괴(Stance)는 공격이 아니라 자세라 InkEnemyAnimator가 IsStanced를 보고 넘어가요
            ["Monk"] = new[]
            {
                new MoveSpec { attack = "선장", state = "Thrust", clips = new[] { "Thrust" } },
                new MoveSpec { state = "Stance", clips = new[] { "Stance" }, mode = MoveMode.Run },
            },
            // 장강 수채 — 수적 졸개: 치켜든 갈고리 칼로 내려베고 0.3초 뒤 되베요
            ["Pirate"] = new[] { new MoveSpec { attack = "두 번 베기", state = "DoubleSlash", clips = new[] { "DoubleSlash" } } },
            // 작살수: 허리춤에 겨눴다가 앞으로 쭉 찔러 뻗고 잡아채 당겨요
            ["Harpooner"] = new[] { new MoveSpec { attack = "작살 찌르기", state = "HarpoonThrust", clips = new[] { "HarpoonThrust" } } },
            // 쇠닻 거한: 닻을 머리 뒤로 감았다가 던지고 두 팔을 뻗어 끌려 날아가요 · 가까우면 앞을 반원으로 휩쓸어요
            ["Anchor"] = new[]
            {
                new MoveSpec { attack = "닻 던지기", state = "AnchorThrow", clips = new[] { "AnchorThrow" } },
                new MoveSpec { attack = "쇠닻 휘두르기", state = "AnchorSwing", clips = new[] { "AnchorSwing" } },
            },
            // 등패수: 기본 공격은 방패를 세운 채 베기(Attack) · 방패 굴리기는 왼팔을 감았다가 앞으로 뿌려요
            ["Rattan"] = new[] { new MoveSpec { attack = "방패 굴리기", state = "ShieldRoll", clips = new[] { "ShieldRoll" } } },
            // 수귀: 땅속에서 두 팔을 뻗으며 솟구쳐 붙잡아요 · 끌고 가는 동안 뒷걸음질(Drag, InkEnemyAnimator가 Dragging을 보고)
            ["WaterGhost"] = new[]
            {
                new MoveSpec { attack = "물귀신 손", state = "Emerge", clips = new[] { "Emerge" } },
                new MoveSpec { state = "Drag", clips = new[] { "Drag" }, mode = MoveMode.Run },
            },
            // 깃발수: 예고 1.5초 동안 깃대를 두 손으로 들고 좌우로 세 번 흔들어요 (그대로 재생)
            ["FlagBearer"] = new[] { new MoveSpec { attack = "지휘", state = "Wave", clips = new[] { "Wave" }, mode = MoveMode.Play } },
            // 탁랑객 (장강 고수, 장검): 베기 · 물결 베기(크게 휘둘러 검기) · 탁류 보법(웅크렸다 미끄러져 찌르기) · 소용돌이(머리 위로 칼을 빙빙 돌리다 땅에 내리꽂아 터뜨려요)
            ["Takrang"] = new[]
            {
                new MoveSpec { attack = "베기", state = "Slash", clips = new[] { "Slash" } },
                new MoveSpec { attack = "물결 베기", state = "WaveCut", clips = new[] { "WaveCut" } },
                new MoveSpec { attack = "탁류 보법", state = "Glide", clips = new[] { "Glide" } },
                new MoveSpec { state = "WhirlEnd", clips = new[] { "WhirlEnd" } },
                new MoveSpec { attack = "소용돌이", state = "Whirlpool", clips = new[] { "Whirl" }, mode = MoveMode.Loop, after = "WhirlEnd" },
            },
            // 수채주 철룡 (장강 보스, 유성추): 휘두르기 · 내려찍기 · 사슬 당기기 · 수하 부르기(호령) · 물기둥(땅 구르기). 2페이즈 전환엔 Roar (InkEnemyAnimator가 IsTransitioning을 보고)
            ["IronDragon"] = new[]
            {
                new MoveSpec { attack = "유성추 휘두르기", state = "MeteorSwing", clips = new[] { "MeteorSwing" } },
                new MoveSpec { attack = "유성추 내려찍기", state = "MeteorSlam", clips = new[] { "MeteorSlam" } },
                new MoveSpec { attack = "사슬 당기기", state = "ChainThrow", clips = new[] { "ChainThrow" } },
                new MoveSpec { attack = "수하 부르기", state = "Command", clips = new[] { "Command" }, mode = MoveMode.Play },
                new MoveSpec { attack = "물기둥", state = "Stomp", clips = new[] { "Stomp" } },
                new MoveSpec { state = "Roar", clips = new[] { "Roar" } },
            },
            // 천마총 — 마교도: 곡도를 치켜들었다 내려베기 (광폭이어도 같은 베기, 광폭에 들면 InkEnemyAnimator가 Frenzy를 한 번 틀고 몸을 붉게)
            ["Cultist"] = new[]
            {
                new MoveSpec { attack = "베기", state = "Slash", clips = new[] { "Attack" } },
                new MoveSpec { attack = "광폭 베기", state = "FrenzySlash", clips = new[] { "Attack" }, speed = 1.3f },
                new MoveSpec { state = "Frenzy", clips = new[] { "Frenzy" }, mode = MoveMode.Play },
            },
            // 혈궁수: 0.8초 겨누고 0.25초 간격 세 발 — 활 당기기를 예고부터 그대로 재생 (1.4초에 끝나게)
            ["BloodArcher"] = new[] { new MoveSpec { attack = "혈시 3연발", state = "TripleShot", clips = new[] { "ShootBow" }, mode = MoveMode.Play, seconds = 1.4f } },
            // 혈인: 1.2초 예고 동안 두 팔을 하늘로 들어 쓰러진 자를 일으켜요 (그대로 재생)
            ["BloodShaman"] = new[] { new MoveSpec { attack = "혈인술", state = "Raise", clips = new[] { "Raise" }, mode = MoveMode.Play, seconds = 1.4f } },
            // 천마 근위: 평소 공격은 기본 Attack(방패 세운 채 베기) · 반격: 0.3초 안에 빠르게 되받아 베기
            ["DemonGuard"] = new[] { new MoveSpec { attack = "반격", state = "Counter", clips = new[] { "Attack" }, mode = MoveMode.Play, seconds = 0.5f } },
            // 혈폭자: 핏덩이를 머리 위로 들었다가 내던져요
            ["BloodBomber"] = new[] { new MoveSpec { attack = "핏덩이", state = "Throw", clips = new[] { "Attack" } } },
            // 혈영검마 (천마총 고수, 장검): 혈영 베기(가로베기) · 혈선(바닥까지 낮췄다 튀어나가 찌르기) · 혈영보(뒤로 순간이동해 세 번 베어 파고들기).
            //   2페이즈 전환 동안엔 ShadowRush 첫 자세(낮게 웅크림)로 버텨요 (InkEnemyAnimator)
            ["BloodShadow"] = new[]
            {
                new MoveSpec { attack = "혈영 베기", state = "Slash", clips = new[] { "Slash" } },
                new MoveSpec { attack = "혈선", state = "Lunge", clips = new[] { "Lunge" } },
                new MoveSpec { attack = "혈영보", state = "ShadowRush", clips = new[] { "ShadowRush" } },
            },
            // 혈불 (폐사찰 보스): 손바닥 내려찍기 · 염불(합장 반복) · 피의 비(두 팔을 하늘로 반복) · 팔 휘두르기(2페이즈, 좌우 세 번). 목탁승 부르기는 기본 Attack(손짓)
            ["BloodBuddha"] = new[]
            {
                new MoveSpec { attack = "손바닥 내려찍기", state = "Palm", clips = new[] { "Palm" } },
                new MoveSpec { state = "Palm3", clips = new[] { "Palm3" } },   // 2페이즈 손바닥 연타: 오른손·왼손·오른손 0.4초 간격 (InkEnemyAnimator가 Palm 대신 골라요)
                new MoveSpec { attack = "염불", state = "Chant", clips = new[] { "Chant" }, mode = MoveMode.Loop },
                new MoveSpec { attack = "피의 비", state = "Rain", clips = new[] { "Rain" }, mode = MoveMode.Loop },
                new MoveSpec { attack = "팔 휘두르기", state = "Arms", clips = new[] { "Arms" } },
                new MoveSpec { attack = "혈수", state = "Grab", clips = new[] { "Grab" } },   // 예고부터 오른팔을 앞으로 쭉 뻗고 손바닥을 펴요, 판정 뒤 움켜쥐고 끌어당겨요 (손바닥 소용돌이는 아트2의 InkBloodBuddhaFx)
            },
            // 파계승 혜광 (폐사찰 고수): 선장 휘두르기 · 금강불괴(예고 3초 동안 버티는 자세 → 선장을 내리꽂아요) · 염주 던지기
            ["Hyegwang"] = new[]
            {
                new MoveSpec { attack = "선장 휘두르기", state = "StaffSweep", clips = new[] { "StaffSweep" } },
                new MoveSpec { attack = "금강불괴", state = "Vajra", clips = new[] { "Vajra" } },
                new MoveSpec { attack = "염주", state = "Beads", clips = new[] { "Beads" } },
                // 축지: 예고 0.4초 동안 선장을 뒤로 감아 겨누고 있다가 순식간에 다가가며 휘둘러요 (선장 휘두르기 동작)
                new MoveSpec { attack = "축지", state = "Rush", clips = new[] { "StaffSweep" } },
            },
            // 등불 귀신: 귀화 예고 동안 떨며 부풀었다가 터져요
            // 등불 귀신: 이제 터지지 않고 플레이어 둘레를 떠돌며 도깨비불 장판을 남겨요 (공격 동작 없이 떠도는 동작 그대로). 귀화(점화)는 옛 패턴
            ["Lantern"] = new[]
            {
                new MoveSpec { attack = "도깨비불", state = "Drift", clips = new[] { "Run" }, mode = MoveMode.Play },
                new MoveSpec { attack = "귀화", state = "Ignite", clips = new[] { "Attack" }, mode = MoveMode.Play },
            },
            // 멧돼지: 이름 없는 기본 공격(돌진) — 예고 동안 머리를 숙여 버티다가(Ready) 판정 동안 빠르게 달려요
            ["Boar"] = new[]
            {
                new MoveSpec { state = "Ready", clips = new[] { "Ready" }, mode = MoveMode.Run },
                new MoveSpec { attack = "", state = "Charge", clips = new[] { "Run" }, mode = MoveMode.Run, speed = 2.2f, ready = "Ready" },
            },
            // 청죽객: 쾌검 3연격(베기 셋을 0.3초씩) · 죽림섬(순간이동 베기 0.25초씩) · 대나무 베기(내려찍기)
            ["Qingzhu"] = new[]
            {
                // 고유 동작 (Generic): 내려베기 → 되받아 가로베기 → 찌르기 / 좌우 번갈아 가로베기 4번 / 머리 위에서 내려베기
                new MoveSpec { attack = "쾌검 3연격", state = "Combo", clips = new[] { "Combo" } },
                new MoveSpec { attack = "죽림섬", state = "Flash", clips = new[] { "Flash" } },
                new MoveSpec { attack = "대나무 베기", state = "Chop", clips = new[] { "Chop" } },
            },
            // 철권 마웅: 주먹 2연타(고유 동작) · 붕산권(예고부터 치켜들었다 땅 내려치기) · 돌진(달리기)
            ["IronFist"] = new[]
            {
                new MoveSpec { attack = "주먹 2연타", state = "Jab", clips = new[] { "Punch2" } },
                new MoveSpec { attack = "붕산권", state = "Quake", clips = new[] { "Quake" }, mode = MoveMode.Play },
                new MoveSpec { state = "ShoulderReady", clips = new[] { "ShoulderReady" }, mode = MoveMode.Run },
                new MoveSpec { attack = "돌진", state = "Charge", clips = new[] { "ShoulderRun" }, mode = MoveMode.Run, speed = 1.6f, ready = "ShoulderReady" },   // 어깨를 앞세워 들이받으며 달려요
            },
        };

        static void DressEnemyModelsInk()
        {
            var entries = new List<AThousandDawns.Art.InkEnemyModels.Entry>();
            foreach (var (name, kind, moveSpeed) in InkEnemies)
            {
                var made = SetupEnemy(name, moveSpeed);
                if (made.prefab != null)
                    entries.Add(new AThousandDawns.Art.InkEnemyModels.Entry
                    {
                        kind = kind, model = made.prefab, controller = made.controller, scale = 1f, yaw = 180f,
                        // 나팔수·궁수: 예고 동안 나팔 불기·시위 당기기 동작을 그대로 재생
                        windupPlays = kind == EnemyKind.Horn || kind == EnemyKind.Archer,
                        noAttackAnim = kind == EnemyKind.Bomber,   // 화약 산적: 손 들지 않고 그대로 터져요
                        moves = made.moves,
                    });
            }
            if (entries.Count == 0) return;
            var models = new GameObject("Ink Enemy Models").AddComponent<AThousandDawns.Art.InkEnemyModels>();
            models.SetEntries(entries.ToArray());
        }

        static (GameObject prefab, RuntimeAnimatorController controller, Move[] moves) SetupEnemy(string name, float moveSpeed)
        {
            string folder = $"{CharactersFolder}/{name}";
            string modelPath = $"{folder}/{name}_Rigged.fbx";
            string controllerPath = $"{folder}/{name}.controller";
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return (null, null, null);
            // 녹림왕은 Generic: 동작을 모두 Blender에서 만들어서, Humanoid 관절 한계에 잘려 칼이 틀어지지 않고 만든 그대로 움직여요
            bool generic = GenericRigs.Contains(name);
            var rigType = generic ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.Human;
            if (importer.animationType != rigType || !importer.bakeAxisConversion || !importer.importAnimation)
            {
                importer.animationType = rigType;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                importer.importAnimation = true;   // 고유 동작(나팔수 BlowHorn 등)이 들어 있으면 써요
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            // 고유 동작(BlowHorn·ShootBow)도 플레이어 동작처럼 몸통 방향·위치를 구워 넣어요.
            // 기본값(몸 방향 기준 루트 회전)으로 두면 모델을 돌려 세운 방향과 어긋나 뒤돌아서 쏴요
            var ownClips = importer.defaultClipAnimations;
            if (ownClips.Length > 0 && (importer.clipAnimations.Length != ownClips.Length || importer.clipAnimations.Any(c => !c.lockRootRotation || !c.keepOriginalOrientation || c.loopTime != LoopingOwnClips.Contains(c.name))))
            {
                foreach (var clip in ownClips)
                {
                    clip.name = clip.takeName.Split('|').Last();
                    clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
                    clip.loopTime = LoopingOwnClips.Contains(clip.name);   // 녹림왕 돌진 달리기는 반복
                }
                importer.clipAnimations = ownClips;
                importer.SaveAndReimport();
            }
            if (!generic && AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().All(a => !a.isHuman))
                Debug.LogWarning($"{modelPath}의 Humanoid 아바타가 만들어지지 않았어요");

            // 동작: 플레이어 동작을 그대로 옮겨 입혀요 (둘 다 Humanoid).
            // 플레이어 설정(SetupPlayerRig)은 Player.controller를 지우고 다시 만들어서, 이미 세운 플레이어 애니메이터 연결이 끊겨요.
            // 그래서 플레이어 모델이 아직 Humanoid로 안 가져와졌을 때만 불러요
            if (AssetImporter.GetAtPath(PlayerRiggedModel) is ModelImporter playerImporter
                && playerImporter.animationType != ModelImporterAnimationType.Human)
                SetupPlayerRig();
            var clips = AssetDatabase.LoadAllAssetsAtPath(PlayerRiggedModel).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            if (!clips.TryGetValue("Idle", out var idle) || !clips.TryGetValue("Run", out var run)) return (null, null, null);
            var attackClip = clips.TryGetValue("IKAttack1", out var ik) ? ik : clips.TryGetValue("Attack1", out var a1) ? a1 : null;
            // 모델 고유 동작 (나팔수 BlowHorn·궁수 ShootBow·철권 Punch2·Quake). 같은 이름이면 플레이어 동작보다 먼저 써요
            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                clips[c.name.Split('|').Last()] = c;
            // 녹림왕은 고유 대기(대도를 늘어뜨린 자세, 휘두르기 끝 자세와 같아서 칼이 돌지 않아요)·달리기를 써요
            idle = clips["Idle"];
            run = clips["Run"];
            // 플레이어 동작은 Generic에 못 써요: 기본 공격은 고유 Attack(동물 덤비기) 또는 Slam(녹림왕 부하 부르기 등)
            if (generic) attackClip = new[] { "Attack", "Slam", "Chop", "Punch2", "StaffSweep" }.Select(n => clips.TryGetValue(n, out var c) ? c : null).FirstOrDefault(c => c != null);
            // 기본 공격에 고유 동작이 있으면 그걸로 (나팔수·궁수 — 예고 동안 그대로 재생해요)
            if (clips.TryGetValue("BlowHorn", out var blow)) attackClip = blow;
            else if (clips.TryGetValue("ShootBow", out var shoot)) attackClip = shoot;

            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var sm = controller.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sRun = sm.AddState("Run"); sRun.motion = run; sRun.speed = generic ? moveSpeed / 3f : 1.2f * moveSpeed / 6f;   // 플레이어 달리기(6칸/초, 1.2배)에 맞춰 (녹림왕 고유 달리기는 3칸/초 기준)
            sm.defaultState = sIdle;
            var t = sIdle.AddTransition(sRun); t.hasExitTime = false; t.duration = 0.1f; t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t = sRun.AddTransition(sIdle); t.hasExitTime = false; t.duration = 0.12f; t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // 공격 상태로는 InkEnemyAnimator가 직접 넘겨요 (트리거로 넘기면 여러 번 걸려 거듭 휘둘러요).
            // 예고 동안 첫 자세(치켜듦)에서 멈췄다가 판정 순간 제 속도로 한 번 내려찍어요 (InkEnemyAnimator가 AttackSpeed를 0/1로 바꿔요)
            void AddMove(string state, AnimationClip[] chain, MoveMode mode, float seconds, float speed)
            {
                AnimatorState prev = null;
                for (int i = 0; i < chain.Length; i++)
                {
                    var s = sm.AddState(i == 0 ? state : $"{state}_{i + 1}");
                    s.motion = chain[i];
                    s.speedParameterActive = true; s.speedParameter = "AttackSpeed";
                    s.speed = seconds > 0f ? chain[i].length / seconds : speed > 0f ? speed : 1f;
                    if (prev != null) { var n = prev.AddTransition(s); n.hasExitTime = true; n.exitTime = 1f; n.duration = 0.05f; }
                    prev = s;
                }
                if (mode == MoveMode.Loop || mode == MoveMode.Whirl) { var again = prev.AddTransition(prev); again.hasExitTime = true; again.exitTime = 1f; again.duration = 0f; }
                else if (mode != MoveMode.Run) { var back = prev.AddTransition(sIdle); back.hasExitTime = true; back.exitTime = 1f; back.duration = 0.3f; }   // 천천히 대기로 (칼이 휙 돌지 않게)
            }
            if (attackClip != null) AddMove("Attack", new[] { attackClip }, MoveMode.Swing, 0f, 0f);
            var moves = new List<Move>();
            if (InkEnemyMoves.TryGetValue(name, out var specs))
                foreach (var spec in specs)
                {
                    var chain = spec.clips.Select(c => clips.TryGetValue(c, out var clip) ? clip : null).ToArray();
                    if (chain.Any(c => c == null))
                    {
                        Debug.LogWarning($"{name}: {spec.state} 동작에 필요한 클립({string.Join(",", spec.clips)})이 없어요");
                        continue;
                    }
                    AddMove(spec.state, chain, spec.mode, spec.seconds, spec.speed);
                    if (spec.attack != null) moves.Add(new Move { attack = spec.attack, state = spec.state, mode = spec.mode, after = spec.after, ready = spec.ready });
                }

            // 재질: 수묵 캐릭터 셰이더 + VARCO 색 텍스처. 채도는 지역 색 양(_InkColorAmount)을 따라요
            var body = GetInkMaterial($"Ink_{name}_Body", "#FFFFFF", 0.45f);
            body.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{name}_Albedo.png"));
            body.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(body);
            var weapon = GetInkMaterial("Ink_Enemy_Weapon", "#3A3F44", 0.1f);
            var red = GetInkMaterial("Ink_Enemy_Red", "#B3261E", 0.9f);       // 깃발·목도리처럼 알아보는 붉은 천
            var rattan = GetInkMaterial("Ink_Enemy_Rattan", "#9C7A45", 0.5f);  // 등나무 방패

            // 프리팹: 모델 + 재질 + 애니메이터
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material m;
                int pi = r.name.IndexOf("_Prop_");
                if (pi >= 0)
                {
                    // VARCO 소품: 자기 색 텍스처를 쓰는 수묵 재질 (방패·삿갓·나팔·폭탄)
                    string key = r.name.Substring(pi + 6);
                    m = GetInkMaterial($"Ink_{name}_{key}", "#FFFFFF", 0.6f);
                    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{name}_Prop_{key}_Albedo.png"));
                    m.SetColor("_BaseColor", Color.white);
                    EditorUtility.SetDirty(m);
                }
                else if (r.name.Contains("_Red_")) m = red;
                else if (r.name.Contains("_Rattan_")) m = rattan;
                else m = r.name.Contains("Weapon") || r.name.Contains("Dao") ? weapon : body;
                r.sharedMaterials = Enumerable.Repeat(m, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            }
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/{name}_Ink.prefab");
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            return (prefab, controller, moves.ToArray());
        }
    }
}
