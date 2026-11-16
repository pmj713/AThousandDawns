using System.Collections.Generic;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 장비가 몸에 보여요: 끼운 기물(PlayerItems에서 판·장비 칸에 놓인 것)을 부위마다 기본 도형으로 만들어 몸에 달아요.
    //   머리(두건·비녀·깃털·머리띠·가면·고깔모자·서클릿·투구) · 상의(비늘·거북 등껍질·죽갑·판금·사슬·조끼·가사·부적·망토·로브)
    //   하의(각반·모래주머니·정강이받이·비단 끈) · 신발(장화·짚신·날개·수정·구름) · 귀걸이 · 목걸이 · 팔찌·장갑 · 허리춤 물건 · 칼자루 술
    //   기물마다 모양·색은 아래 Looks 표 (없으면 부위 기본 모양에 키워드 색). 이계 기물은 보석·룬이 가장자리 기운(_AuraColor)으로 빛나요.
    // 굳은 장식은 뼈를 따라가고(묶는 자세: 몸통·머리·발은 스킨 바인드 자세 기준, 팔다리는 뼈 줄기 방향 기준이라 지금 동작과 상관없어요),
    //   매달린 것(망토·귀걸이·목걸이 펜던트·허리춤·칼자루 술·비단 끈)은 무게로 아래로 처지고 움직임에 끌려 흔들려요.
    // 다시 만드는 건 끼운 기물이 바뀔 때만 (PlayerItems.Changed → 목록이 달라졌을 때). 충돌체 없음. 모델이 숨으면 같이 숨어요
    // 씬 빌더(PrototypeSceneBuilder.InkPlayer.cs)가 플레이어에 붙이고 재질 틀을 넘겨요
    [RequireComponent(typeof(PlayerController))]
    public class InkPlayerGear : MonoBehaviour
    {
        [SerializeField] Material template;   // InkCharacter 흰 재질 (색마다 복제)

        public void Setup(Material mat) => template = mat;

        enum G
        {
            Hood, Pin, Feather, Headband, Mask, Rod, WizardHat, Circlet, Helm, HolyHelm,
            Scale, Shell, Bamboo, Plate, Chain, Vest, Kasaya, Talisman, Scroll, Cloak, Robe,
            Wraps, Sandbags, Greaves, Silk,
            Boots, Sandals, WingBoots, CrystalBoots, CloudBoots, ShadowBoots,
            Hoop, Gem, EarFeather, Bell, Coin,
            Pendant, Mirror, Claw, Tooth, Paper, Figure, Bead, Holy, Crystal, Amber,
            Band, Beads, Bracer, Glove, Gauntlet, FeatherBand,
            Gourd, Pot, Lamp, Pouch, Stone, Drum, Flute, Tablet, Norigae, ChainBelt, Mushroom, Belt, Scabbard,
            Tassel, TasselBlade, TasselFeather, TasselFan,
        }

        // 기물 id → 모양 · 주색 · 보조색 (보석·끈·무늬)
        static readonly Dictionary<string, (G look, string a, string b)> Looks = new Dictionary<string, (G, string, string)>
        {
            // 열양
            ["ember_ring"] = (G.Band, "#C8402A", "#FF8A30"), ["ember_dagger"] = (G.TasselBlade, "#D8401F", "#C8C0B0"),
            ["flint"] = (G.Stone, "#5A5550", "#FF7A30"), ["censer"] = (G.Pot, "#B08A3A", "#FF6A20"),
            ["flame_fan"] = (G.TasselFan, "#E0502A", "#F2C040"), ["oil_lamp"] = (G.Lamp, "#B08A3A", "#FFB040"),
            ["crimson_gloves"] = (G.Glove, "#C02A20", "#F2A030"), ["fire_gourd"] = (G.Gourd, "#D8401F", "#6A3A20"),
            ["fire_scale"] = (G.Scale, "#C83A20", "#F2A030"), ["vermilion_feather"] = (G.Feather, "#E8361C", "#F2C040"),
            ["ash_urn"] = (G.Pot, "#6A6058", "#FF6A20"), ["charcoal_bag"] = (G.Pouch, "#3A3430", "#D8401F"),
            ["fire_rune_ring"] = (G.Band, "#8A2A20", "#FF7A20"), ["salamander_cloak"] = (G.Cloak, "#D8401F", "#F2A030"),
            ["fire_spirit_earring"] = (G.Gem, "#F2A030", "#FF5A20"), ["phoenix_charm"] = (G.TasselFeather, "#F0602A", "#F2C040"),
            ["dragon_tooth"] = (G.Tooth, "#F2EEDC", "#C8301F"),
            // 빙백
            ["frost_ring"] = (G.Hoop, "#BFE6F8", "#6AB8E8"), ["icicle_needle"] = (G.TasselBlade, "#A8DCF4", "#E8F6FF"),
            ["snow_hood"] = (G.Hood, "#F2F6FA", "#8FC8E8"), ["snowflake_crystal"] = (G.Crystal, "#BFE8FF", "#FFFFFF"),
            ["coldiron_pin"] = (G.Pin, "#6A8AA8", "#BFE6F8"), ["ice_beads"] = (G.Beads, "#CFEFFF", "#6AB8E8"),
            ["cold_amber"] = (G.Amber, "#E8A040", "#BFE6F8"), ["snow_lotus_sachet"] = (G.Pouch, "#E8F0F8", "#E890B0"),
            ["snow_reishi"] = (G.Mushroom, "#EEF2F4", "#A8D8F0"), ["icemirror"] = (G.Mirror, "#DDF2FF", "#8FC8E8"),
            ["frost_bead"] = (G.Gem, "#A8DCF4", "#FFFFFF"), ["frost_armor"] = (G.Plate, "#A8D4EC", "#E8F6FF"),
            ["slime_bracelet"] = (G.Band, "#6AD0E8", "#BFF4FF"), ["wizard_hat"] = (G.WizardHat, "#3050A8", "#BFE6F8"),
            ["ice_spirit_pendant"] = (G.Crystal, "#8FD8FF", "#FFFFFF"), ["frost_robe"] = (G.Robe, "#4A78C8", "#DDF2FF"),
            ["ice_crystal_boots"] = (G.CrystalBoots, "#6A90C8", "#BFF0FF"),
            // 뇌정
            ["lightningwood_comb"] = (G.Pin, "#6A4A2A", "#F0C030"), ["copper_coin"] = (G.Coin, "#C8803A", "#5A3418"),
            ["thunder_brush"] = (G.Tassel, "#F0C030", "#3A2A20"), ["thunderstone"] = (G.Pendant, "#4A4A58", "#F0D040"),
            ["thunder_bell"] = (G.Bell, "#E0B030", "#8A5A20"), ["thundercloud_talisman"] = (G.Talisman, "#F2D850", "#C82A1E"),
            ["hawk_talon"] = (G.Claw, "#3A3028", "#F0C030"), ["thunder_drum"] = (G.Drum, "#C8302A", "#F0D8A0"),
            ["cloud_flute"] = (G.Flute, "#6A8A40", "#F0C030"), ["thunder_seal"] = (G.Tablet, "#E8C040", "#6A3A20"),
            ["lightning_rod"] = (G.Rod, "#8A8F98", "#FFE040"), ["thunder_glove"] = (G.Glove, "#3A3A48", "#FFE040"),
            ["rune_earring"] = (G.Gem, "#4A5AC8", "#FFE040"), ["storm_circlet"] = (G.Circlet, "#E0B030", "#FFE860"),
            ["thunder_rune_belt"] = (G.Belt, "#3A3040", "#FFE040"), ["storm_bracer"] = (G.Bracer, "#5A6AA8", "#FFE860"),
            ["lightning_boots"] = (G.WingBoots, "#3A4060", "#FFE040"),
            // 강기
            ["breeze_tassel"] = (G.Tassel, "#4AB0C8", "#E8F0F0"), ["whetstone"] = (G.Stone, "#6A7080", "#A8B0C0"),
            ["leather_bracer"] = (G.Bracer, "#7A4E2E", "#3A2A1E"), ["wooden_sword"] = (G.TasselBlade, "#A8784A", "#C89A60"),
            ["ink_scabbard"] = (G.Tassel, "#2A2A30", "#8A90A0"), ["iron_sandbag"] = (G.Sandbags, "#8A7656", "#4A4A50"),
            ["blood_bandage"] = (G.Bracer, "#F0ECE0", "#B8201E"), ["ganggi_bead"] = (G.Bead, "#4A60C0", "#BFD0FF"),
            ["sword_manual"] = (G.Scroll, "#E8DCB8", "#4A60C0"), ["broken_blade"] = (G.TasselBlade, "#4A60C0", "#B8C0CC"),
            ["finishing_edge"] = (G.Tassel, "#C8202A", "#2A2A30"), ["sword_scroll"] = (G.Tassel, "#E8DCB8", "#4A60C0"),
            ["spellblade_tassel"] = (G.Tassel, "#3A48C8", "#8FB0FF"), ["knight_gauntlet"] = (G.Gauntlet, "#A8B0BC", "#4A60C0"),
            ["rune_scabbard"] = (G.Scabbard, "#3A2E28", "#6A8AFF"), ["knight_helm"] = (G.Helm, "#A8B0BC", "#C8202A"),
            ["spellsword_boots"] = (G.Boots, "#2A3060", "#6A8AFF"),
            // 금강
            ["guardian_norigae"] = (G.Norigae, "#C8202A", "#4FB088"), ["turtle_shell"] = (G.Shell, "#6A7A3A", "#C8B070"),
            ["wooden_figure"] = (G.Figure, "#A8784A", "#D8A830"), ["bamboo_armor"] = (G.Bamboo, "#8AA850", "#5A4A30"),
            ["iron_bracelet"] = (G.Band, "#6A7078", "#D8A830"), ["iron_chain"] = (G.ChainBelt, "#6A7078", "#4A5058"),
            ["blood_stone"] = (G.Gem, "#D8A830", "#A8141E"), ["vajra"] = (G.TasselBlade, "#C8202A", "#E0B040"),
            ["golden_kasaya"] = (G.Kasaya, "#E8B030", "#C8302A"), ["unbreakable_tablet"] = (G.Tablet, "#3A3530", "#E8C040"),
            ["iron_vest"] = (G.Vest, "#5A6068", "#D8A830"), ["guard_talisman"] = (G.Paper, "#F2D850", "#C82A1E"),
            ["dwarf_belt"] = (G.Belt, "#7A4E2E", "#C8A040"), ["mithril_chainmail"] = (G.Chain, "#D8E4EE", "#7FA8D0"),
            ["paladin_helm"] = (G.HolyHelm, "#E8ECF2", "#E8C040"), ["dwarf_greaves"] = (G.Greaves, "#8A9098", "#C8A040"),
            ["holy_amulet"] = (G.Holy, "#F2D860", "#FFFFFF"),
            // 경공
            ["straw_sandals"] = (G.Sandals, "#D8B870", "#8A6A3A"), ["wind_knot"] = (G.Headband, "#4AB880", "#E8F0E0"),
            ["leggings"] = (G.Wraps, "#E8E0CC", "#4A8A5A"), ["pinwheel"] = (G.TasselFan, "#E84A4A", "#4AB0E8"),
            ["swallow_feather"] = (G.EarFeather, "#2A2A38", "#F2F2F2"), ["willow_dart"] = (G.TasselBlade, "#6AB850", "#A8B0BC"),
            ["black_wind_cloak"] = (G.Cloak, "#2A2A34", "#4AB880"), ["afterimage_mask"] = (G.Mask, "#F2EEE4", "#3FAF6A"),
            ["cloud_dragon_belt"] = (G.Belt, "#3FAF6A", "#E8C040"), ["cloud_silk"] = (G.Silk, "#E8F4FF", "#9FD8E8"),
            ["shadow_step"] = (G.ShadowBoots, "#2A2A34", "#8A6AC8"), ["cloud_boots"] = (G.CloudBoots, "#F2F6FA", "#9FD8E8"),
            ["goblin_boots"] = (G.Boots, "#6A7A3A", "#4A3A28"), ["elven_leggings"] = (G.Wraps, "#4A8A3A", "#C8A060"),
            ["wind_spirit_cloak"] = (G.Cloak, "#6AD8A0", "#E8FFF0"), ["elf_feather_earring"] = (G.EarFeather, "#6AD890", "#F2E8A0"),
            ["harpy_feather_bracelet"] = (G.FeatherBand, "#8A6AC8", "#F2E8C0"),
        };

        static readonly Dictionary<Keyword, string> KeywordColor = new Dictionary<Keyword, string>
        {
            [Keyword.Yeolyang] = "#D8401F", [Keyword.Bingbaek] = "#8FD0F0", [Keyword.Noejeong] = "#F0C030",
            [Keyword.Ganggi] = "#4A60C0", [Keyword.Geumgang] = "#D8A830", [Keyword.Gyeonggong] = "#3FAF6A",
        };

        static G DefaultLook(EquipSlot slot) => slot switch
        {
            EquipSlot.Head => G.Headband, EquipSlot.Top => G.Kasaya, EquipSlot.Bottom => G.Wraps, EquipSlot.Shoes => G.Boots,
            EquipSlot.Earring => G.Gem, EquipSlot.Neck => G.Pendant, EquipSlot.Bracelet => G.Band, EquipSlot.Waist => G.Pouch,
            _ => G.Tassel,
        };

        // 굳은 장식: 뼈 기준 자리·방향
        sealed class Attach { public Transform Bone, Holder; public Vector3 Pos; public Quaternion Rot; }
        // 매달린 장식: 뼈 기준 매단 점 + 처진 방향 (스프링)
        sealed class Dangle
        {
            public Transform Bone, Pivot; public Vector3 Pos, Rest, Dir, DirVel, LastPos, LastVel; public float Stiff, Drag, MaxAngle; public bool Fresh = true;
        }

        PlayerController _player;
        PlayerItems _items;
        Animator _animator;
        SkinnedMeshRenderer[] _skins;
        SkinnedMeshRenderer _shownSkin;
        Transform _root;
        readonly List<Attach> _attaches = new List<Attach>();
        readonly List<Dangle> _dangles = new List<Dangle>();
        string _signature = "";
        bool _dirty = true;
        float _s = 1f;   // 모델 크기 (원본 1.9m 모델 기준 배율)
        Quaternion _frameOffset = Quaternion.identity;   // Animator 축 → 몸 앞·위·오른쪽
        static readonly Dictionary<(Color32, bool), Material> Mats = new Dictionary<(Color32, bool), Material>();
        static Mesh _cube, _sphere, _cylinder, _capsule;

        void Awake() => _player = GetComponent<PlayerController>();

        void OnDisable()
        {
            if (_items != null) _items.Changed -= MarkDirty;
            _items = null;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        void MarkDirty() => _dirty = true;

        void LateUpdate()
        {
            if (_player == null || template == null) return;
            var items = _player.Items;
            if (items != _items)
            {
                if (_items != null) _items.Changed -= MarkDirty;
                _items = items;
                if (_items != null) _items.Changed += MarkDirty;
                _dirty = true;
            }
            if (_items == null) return;
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator == null || !_animator.isHuman) { _animator = null; return; }
                _skins = _animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                _shownSkin = _skins.Length > 0 ? _skins[0] : null;
            }
            if (_dirty)
            {
                _dirty = false;
                string sig = Signature();
                if (sig != _signature) { _signature = sig; Rebuild(); }
            }
            if (_root == null) return;

            bool shown = _shownSkin == null || (_shownSkin.enabled && _shownSkin.gameObject.activeInHierarchy);
            if (_root.gameObject.activeSelf != shown) _root.gameObject.SetActive(shown);
            if (shown) Pose();
        }

        void Pose()
        {
            foreach (var a in _attaches)
            {
                if (a.Bone == null) continue;
                var rot = a.Bone.rotation * a.Rot;
                a.Holder.SetPositionAndRotation(a.Bone.position + a.Bone.rotation * a.Pos, rot);
            }
            var frame = Frame;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            foreach (var d in _dangles)
            {
                if (d.Bone == null) continue;
                var pos = d.Bone.position + d.Bone.rotation * d.Pos;
                var rest = (frame * d.Rest).normalized;
                if (d.Fresh) { d.Fresh = false; d.LastPos = pos; d.LastVel = Vector3.zero; d.Dir = rest; d.DirVel = Vector3.zero; }
                var vel = (pos - d.LastPos) / dt;
                if (vel.sqrMagnitude > 400f) vel = Vector3.zero;   // 순간이동(비도 등)은 무시
                var acc = Vector3.ClampMagnitude((vel - d.LastVel) / dt, 60f);
                d.LastPos = pos; d.LastVel = vel;
                // 무게(쉴 때 방향) + 움직임 반대로 끌림 + 가속 반대로 쏠림
                var target = rest * 9.8f - vel * d.Drag - acc * 0.12f;
                target = Vector3.RotateTowards(rest, target.normalized, d.MaxAngle * Mathf.Deg2Rad, 0f);
                d.DirVel += ((target - d.Dir) * d.Stiff - d.DirVel * Mathf.Sqrt(d.Stiff) * 1.4f) * dt;
                d.Dir = (d.Dir + d.DirVel * dt).normalized;
                var baseRot = frame * Quaternion.FromToRotation(Vector3.down, d.Rest.normalized);
                d.Pivot.SetPositionAndRotation(pos, Quaternion.FromToRotation(rest, d.Dir) * baseRot);
            }
        }

        string Signature()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var item in _items.Items)
                if (item.OnBoard) sb.Append(item.Def.id).Append('@').Append(item.Cell.x).Append(';');
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 만들기

        void Rebuild()
        {
            if (_root != null) Destroy(_root.gameObject);
            _attaches.Clear();
            _dangles.Clear();
            _root = new GameObject("Ink Gear (장비)").transform;
            Meshes();
            if (!Calibrate()) return;

            int earrings = 0;
            foreach (var item in _items.Items)
            {
                if (!item.OnBoard) continue;
                var def = item.Def;
                var slot = def.slot;
                G look;
                Color a, b;
                if (Looks.TryGetValue(def.id, out var entry)) { look = entry.look; a = Hex(entry.a); b = Hex(entry.b); }
                else
                {
                    if (slot == EquipSlot.None) continue;   // 부위가 없는 옛 기물은 몸에 안 달아요
                    look = DefaultLook(slot);
                    a = Hex(KeywordColor.TryGetValue(def.keyword, out var hex) ? hex : "#B8B0A0");
                    b = Hex("#F2E8C8");
                }
                bool glow = def.otherworld;
                bool right = slot == EquipSlot.Earring && (PlayerItems.EquipMode ? item.Cell.x == 5 : earrings++ % 2 == 1);
                Build(look, a, b, glow, right);
            }
        }

        void Build(G look, Color a, Color b, bool glow, bool rightEar)
        {
            var dark = Hex("#2E2620");
            Transform h;
            switch (look)
            {
                // ── 머리 (머리뼈 = 목 위 머리 밑동, 머리 가운데는 위로 0.16)
                case G.Hood:
                    h = Rigid(HumanBodyBones.Head, Vector3.zero);
                    P(h, _sphere, 0f, .17f, -.035f, .27f, .31f, .27f, a);
                    P(h, _cube, 0f, .03f, -.12f, .22f, .2f, .06f, a, -10f);
                    P(h, _cylinder, 0f, .1f, .015f, .25f, .02f, .2f, b, -12f);
                    break;
                case G.Pin:
                    h = Rigid(HumanBodyBones.Head, Vector3.zero);
                    P(h, _cylinder, 0f, .27f, -.11f, .022f, .14f, .022f, a, 0f, 0f, 70f);
                    P(h, _sphere, .13f, .32f, -.11f, .055f, .055f, .055f, b, glow: true);
                    P(h, _sphere, -.13f, .22f, -.11f, .03f, .03f, .03f, b);
                    break;
                case G.Feather:
                    h = Rigid(HumanBodyBones.Head, Vector3.zero);
                    P(h, _sphere, .05f, .4f, -.1f, .06f, .3f, .02f, a, -25f, 0f, -15f);
                    P(h, _sphere, .09f, .52f, -.15f, .045f, .12f, .018f, b, -30f, 0f, -18f);
                    P(h, _sphere, .02f, .27f, -.07f, .05f, .05f, .05f, b, glow: true);
                    break;
                case G.Headband:
                    h = Rigid(HumanBodyBones.Head, Vector3.zero);
                    P(h, _cylinder, 0f, .2f, -.02f, .26f, .025f, .26f, a);
                    P(h, _sphere, 0f, .2f, -.15f, .06f, .05f, .04f, b);
                    P(h, _cube, .035f, .1f, -.17f, .045f, .2f, .012f, a, 20f, 0f, -12f);
                    P(h, _cube, -.035f, .1f, -.17f, .045f, .2f, .012f, a, 20f, 0f, 12f);
                    break;
                case G.Mask:
                    h = Rigid(HumanBodyBones.Head, Vector3.zero);
                    P(h, _cube, 0f, .115f, .1f, .21f, .09f, .035f, a);
                    P(h, _cube, .048f, .123f, .12f, .05f, .02f, .01f, dark);
                    P(h, _cube, -.048f, .123f, .12f, .05f, .02f, .01f, dark);
                    P(h, _cube, 0f, .085f, .12f, .15f, .012f, .01f, b);
                    break;
                case G.Rod:
                    h = Rigid(HumanBodyBones.Head, Vector3.zero);
                    P(h, _cylinder, -.04f, .37f, -.1f, .02f, .13f, .02f, a, 0f, 0f, 15f);
                    P(h, _sphere, -.075f, .5f, -.1f, .065f, .065f, .065f, b, glow: true);
                    P(h, _cylinder, -.025f, .3f, -.1f, .05f, .012f, .05f, a, 0f, 0f, 15f);
                    break;
                case G.WizardHat:
                    h = Rigid(HumanBodyBones.Head, Vector3.zero);
                    P(h, _cylinder, 0f, .2f, -.03f, .44f, .012f, .44f, a);
                    P(h, _cylinder, 0f, .23f, -.03f, .26f, .03f, .26f, b);
                    for (int k = 0; k < 6; k++)
                    {
                        float r = .25f * (1f - k / 6.5f);
                        P(h, _cylinder, 0f, .27f + k * .065f, -.03f - k * k * .006f, r, .036f, r, a, -k * 4f);
                    }
                    P(h, _sphere, 0f, .66f, -.24f, .06f, .06f, .06f, b, glow: true);
                    break;
                case G.Circlet:
                    h = Rigid(HumanBodyBones.Head, Vector3.zero);
                    P(h, _cylinder, 0f, .21f, -.02f, .26f, .02f, .26f, a);
                    P(h, _sphere, 0f, .22f, .115f, .055f, .07f, .035f, b, glow: true);
                    P(h, _cube, .07f, .25f, .1f, .02f, .07f, .015f, a, 0f, 0f, -20f);
                    P(h, _cube, -.07f, .25f, .1f, .02f, .07f, .015f, a, 0f, 0f, 20f);
                    break;
                case G.Helm:
                    h = Rigid(HumanBodyBones.Head, Vector3.zero);
                    P(h, _sphere, 0f, .18f, -.03f, .29f, .31f, .29f, a);
                    P(h, _cube, 0f, .13f, .112f, .2f, .03f, .03f, dark);
                    P(h, _cube, 0f, .34f, -.04f, .035f, .1f, .26f, b);
                    P(h, _cube, 0f, .27f, -.19f, .035f, .2f, .07f, b, 20f);
                    break;
                case G.HolyHelm:
                    h = Rigid(HumanBodyBones.Head, Vector3.zero);
                    P(h, _sphere, 0f, .18f, -.03f, .29f, .31f, .29f, a);
                    P(h, _cylinder, 0f, .12f, -.03f, .3f, .025f, .3f, b);
                    P(h, _cube, 0f, .2f, .117f, .028f, .11f, .012f, b, glow: true);
                    P(h, _cube, 0f, .22f, .117f, .075f, .025f, .012f, b, glow: true);
                    P(h, _cube, .15f, .23f, -.05f, .015f, .11f, .16f, b, -25f);
                    P(h, _cube, -.15f, .23f, -.05f, .015f, .11f, .16f, b, -25f);
                    break;

                // ── 상의 (가슴뼈 = 명치쯤, 어깨는 위로 0.23, 가슴 앞면 z 0.08)
                case G.Scale:
                    h = Rigid(HumanBodyBones.Chest, Vector3.zero);
                    P(h, _cube, 0f, .12f, .13f, .36f, .28f, .06f, a, -5f);
                    for (int row = 0; row < 3; row++)
                        for (int k = -2; k <= 2; k++)
                            P(h, _sphere, k * .06f + (row % 2) * .03f, .2f - row * .07f, .17f, .07f, .05f, .03f, row == 0 ? b : a, -5f);
                    Pauldrons(a, b);
                    break;
                case G.Shell:
                    h = Rigid(HumanBodyBones.Chest, Vector3.zero);
                    P(h, _sphere, 0f, .1f, -.2f, .38f, .44f, .17f, a);
                    for (int k = 0; k < 5; k++)
                        P(h, _sphere, (k % 2 == 0 ? -1 : 1) * (k == 0 ? 0f : .08f), .1f + (k - 2) * .08f, -.28f, .09f, .07f, .03f, b);
                    P(h, _cube, 0f, .12f, .09f, .04f, .38f, .02f, b, 0f, 0f, 30f);
                    break;
                case G.Bamboo:
                    h = Rigid(HumanBodyBones.Chest, Vector3.zero);
                    for (int k = -3; k <= 3; k++)
                        P(h, _cylinder, k * .046f, .12f, .15f - Mathf.Abs(k) * .012f, .04f, .15f, .04f, a);
                    P(h, _cube, 0f, .21f, .155f, .36f, .025f, .05f, b);
                    P(h, _cube, 0f, .04f, .155f, .36f, .025f, .05f, b);
                    Pauldrons(a, b);
                    break;
                case G.Plate:
                    h = Rigid(HumanBodyBones.Chest, Vector3.zero);
                    P(h, _cube, 0f, .13f, .13f, .36f, .27f, .06f, a, -5f);
                    P(h, _cube, 0f, .2f, .165f, .28f, .02f, .01f, b, -5f);
                    P(h, _cylinder, 0f, .27f, -.04f, .22f, .035f, .2f, b);
                    Pauldrons(a, b);
                    break;
                case G.Chain:
                    h = Rigid(HumanBodyBones.Chest, Vector3.zero);
                    P(h, _cube, 0f, .07f, -.01f, .42f, .28f, .34f, a);
                    P(h, _sphere, 0f, .1f, .14f, .36f, .28f, .1f, a);
                    P(h, _cube, 0f, -.07f, -.01f, .43f, .03f, .35f, b, glow: true);
                    P(h, _cylinder, 0f, .27f, -.04f, .22f, .04f, .2f, a);
                    Pauldrons(a, b);
                    break;
                case G.Vest:
                    h = Rigid(HumanBodyBones.Chest, Vector3.zero);
                    P(h, _cube, 0f, .07f, -.01f, .42f, .28f, .34f, a);
                    P(h, _sphere, 0f, .1f, .14f, .34f, .26f, .08f, a);
                    P(h, _cube, 0f, .14f, .19f, .24f, .022f, .01f, b);
                    P(h, _cube, 0f, .04f, .175f, .3f, .022f, .01f, b);
                    break;
                case G.Kasaya:
                    h = Rigid(HumanBodyBones.Chest, Vector3.zero);
                    P(h, _cube, 0f, .1f, .16f, .1f, .52f, .03f, a, 0f, 0f, -35f);
                    P(h, _cube, 0f, .1f, -.2f, .1f, .52f, .03f, a, 0f, 0f, 35f);
                    P(h, _sphere, -.13f, .24f, -.05f, .16f, .09f, .22f, a);
                    P(h, _cylinder, -.06f, .2f, .17f, .06f, .012f, .06f, b, 90f);
                    break;
                case G.Talisman:
                    h = Rigid(HumanBodyBones.Chest, Vector3.zero);
                    P(h, _cube, .06f, .14f, .165f, .1f, .2f, .01f, a, 0f, 0f, -8f);
                    P(h, _cube, .06f, .15f, .171f, .014f, .14f, .005f, b, 0f, 0f, -8f);
                    P(h, _cube, -.05f, .1f, -.2f, .1f, .2f, .01f, a, 0f, 0f, 6f);
                    break;
                case G.Scroll:
                    h = Rigid(HumanBodyBones.Chest, Vector3.zero);
                    P(h, _cylinder, .07f, .17f, .165f, .055f, .1f, .055f, a, 0f, 0f, 80f);
                    P(h, _cylinder, -.035f, .152f, .165f, .065f, .012f, .065f, b, 0f, 0f, 80f);
                    P(h, _cylinder, .175f, .188f, .165f, .065f, .012f, .065f, b, 0f, 0f, 80f);
                    break;
                case G.Cloak:
                case G.Robe:
                    h = Rigid(HumanBodyBones.Chest, Vector3.zero);
                    P(h, _sphere, 0f, .24f, -.04f, .48f, .15f, .38f, a);   // 어깨를 덮는 망토 깃
                    P(h, _sphere, 0f, .25f, .14f, .055f, .055f, .035f, b, glow: true);
                    if (look == G.Robe)
                    {
                        P(h, _sphere, 0f, .27f, -.13f, .3f, .13f, .15f, a);
                        P(h, _cube, .07f, .05f, .165f, .04f, .32f, .012f, b);
                        P(h, _cube, -.07f, .05f, .165f, .04f, .32f, .012f, b);
                    }
                    h = Hang(HumanBodyBones.Chest, new Vector3(0f, .25f, -.2f), new Vector3(0f, -1f, -.14f), 40f, .25f, 70f);
                    P(h, _cube, 0f, -.42f, 0f, .42f, .84f, .025f, a);
                    P(h, _cube, 0f, -.83f, 0f, .43f, .045f, .03f, b);
                    if (look == G.Robe) P(h, _cube, 0f, -.5f, -.015f, .06f, .55f, .01f, b);
                    break;

                // ── 하의
                case G.Wraps:
                    foreach (bool left in Sides)
                    {
                        h = Limb(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, .55f);
                        P(h, _cylinder, 0f, 0f, 0f, .13f, .14f, .13f, a);
                        for (int k = -1; k <= 1; k++) P(h, _cylinder, 0f, k * .07f, 0f, .135f, .012f, .135f, b, 8f);
                    }
                    break;
                case G.Sandbags:
                    foreach (bool left in Sides)
                    {
                        h = Limb(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, .45f);
                        P(h, _sphere, 0f, 0f, .06f, .11f, .15f, .09f, a);
                        P(h, _sphere, 0f, 0f, -.06f, .1f, .13f, .08f, a);
                        P(h, _cylinder, 0f, .03f, 0f, .135f, .016f, .135f, b);
                        P(h, _cylinder, 0f, -.04f, 0f, .135f, .016f, .135f, b);
                    }
                    break;
                case G.Greaves:
                    foreach (bool left in Sides)
                    {
                        var calf = left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
                        var foot = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
                        h = Limb(calf, foot, .5f);
                        P(h, _cube, 0f, 0f, .05f, .12f, .32f, .05f, a);
                        P(h, _cylinder, 0f, -.12f, 0f, .13f, .02f, .13f, b);
                        h = Limb(calf, foot, .02f);
                        P(h, _sphere, 0f, 0f, .05f, .12f, .1f, .08f, b);
                    }
                    break;
                case G.Silk:
                    h = Rigid(HumanBodyBones.Hips, Vector3.zero);
                    P(h, _cylinder, 0f, .07f, 0f, .5f, .04f, .41f, a);
                    foreach (bool left in Sides)
                    {
                        h = Hang(HumanBodyBones.Hips, new Vector3(left ? -.24f : .24f, .06f, -.02f), new Vector3(left ? -.1f : .1f, -1f, -.25f), 18f, .5f, 80f);
                        P(h, _cube, 0f, -.3f, 0f, .07f, .6f, .012f, a);
                        P(h, _cube, 0f, -.62f, 0f, .07f, .06f, .014f, b);
                    }
                    break;

                // ── 신발 (발뼈 = 발목, 발 가운데는 아래 0.07 · 앞 0.085)
                case G.Boots:
                case G.WingBoots:
                case G.CrystalBoots:
                case G.CloudBoots:
                case G.ShadowBoots:
                    foreach (bool left in Sides)
                    {
                        var calf = left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
                        var footBone = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
                        h = Rigid(footBone, Vector3.zero);
                        P(h, _sphere, 0f, -.065f, .075f, .15f, .17f, .33f, a);
                        P(h, _cube, 0f, -.14f, .08f, .13f, .03f, .3f, look == G.ShadowBoots ? b : dark);
                        float side = left ? -1f : 1f;
                        if (look == G.WingBoots)
                        {
                            P(h, _cube, side * .08f, -.01f, -.04f, .012f, .08f, .14f, b, -30f, glow: true);
                            P(h, _cube, side * .08f, .03f, -.09f, .012f, .06f, .1f, b, -45f, glow: true);
                        }
                        if (look == G.CrystalBoots)
                            for (int k = 0; k < 3; k++)
                                P(h, _cube, side * .07f, -.02f + k * .02f, .02f + k * .06f, .04f, .1f, .04f, b, 20f, 0f, side * -30f, true);
                        var c = Limb(calf, footBone, .82f);
                        P(c, _cylinder, 0f, 0f, 0f, .135f, .085f, .135f, a);
                        P(c, _cylinder, 0f, .075f, 0f, .14f, .016f, .14f, look == G.ShadowBoots ? dark : b);
                        if (look == G.CloudBoots)
                            for (int k = 0; k < 4; k++)
                            {
                                float ang = k * Mathf.PI * 0.5f + 0.4f;
                                P(c, _sphere, Mathf.Cos(ang) * .075f, .06f, Mathf.Sin(ang) * .075f, .08f, .06f, .08f, b);
                            }
                        if (look == G.ShadowBoots)
                            for (int k = -1; k <= 1; k++) P(c, _cylinder, 0f, k * .03f - .02f, 0f, .14f, .008f, .14f, b, 10f);
                    }
                    break;
                case G.Sandals:
                    foreach (bool left in Sides)
                    {
                        var footBone = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
                        h = Rigid(footBone, Vector3.zero);
                        P(h, _cube, 0f, -.15f, .085f, .135f, .035f, .32f, a);
                        P(h, _cube, 0f, -.085f, .13f, .135f, .025f, .035f, b);
                        P(h, _cube, 0f, -.065f, .02f, .135f, .025f, .035f, b);
                        var c = Limb(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, footBone, .94f);
                        P(c, _cylinder, 0f, 0f, 0f, .12f, .018f, .12f, b);
                    }
                    break;

                // ── 귀걸이 (귀 = 머리뼈에서 옆 0.095 · 위 0.1)
                case G.Hoop:
                case G.Gem:
                case G.EarFeather:
                case G.Bell:
                case G.Coin:
                    h = Hang(HumanBodyBones.Head, new Vector3(rightEar ? .095f : -.095f, .1f, 0f), Vector3.down, 60f, .2f, 50f);
                    switch (look)
                    {
                        case G.Hoop:
                            for (int k = 0; k < 8; k++)
                            {
                                float ang = k * Mathf.PI / 4f;
                                P(h, _sphere, 0f, -.045f + Mathf.Cos(ang) * .035f, Mathf.Sin(ang) * .035f, .02f, .02f, .02f, k == 4 ? b : a, glow: glow && k == 4);
                            }
                            break;
                        case G.Gem:
                            P(h, _sphere, 0f, -.02f, 0f, .014f, .035f, .014f, a);
                            P(h, _sphere, 0f, -.06f, 0f, .045f, .055f, .045f, b, glow: true);
                            break;
                        case G.EarFeather:
                            P(h, _sphere, 0f, -.02f, 0f, .02f, .02f, .02f, b);
                            P(h, _sphere, 0f, -.08f, 0f, .03f, .11f, .01f, a);
                            P(h, _sphere, 0f, -.13f, 0f, .022f, .05f, .008f, b);
                            break;
                        case G.Bell:
                            P(h, _sphere, 0f, -.05f, 0f, .055f, .055f, .055f, a);
                            P(h, _sphere, 0f, -.08f, 0f, .02f, .02f, .02f, b);
                            break;
                        default:
                            P(h, _cylinder, 0f, -.05f, 0f, .065f, .007f, .065f, a, 0f, 0f, 90f);
                            P(h, _cube, 0f, -.05f, 0f, .02f, .02f, .02f, b);
                            break;
                    }
                    break;

                // ── 목걸이 (가슴뼈 기준 목둘레 끈 + 앞가슴에 매달린 펜던트)
                case G.Pendant:
                case G.Mirror:
                case G.Claw:
                case G.Tooth:
                case G.Paper:
                case G.Figure:
                case G.Bead:
                case G.Holy:
                case G.Crystal:
                case G.Amber:
                    h = Rigid(HumanBodyBones.Chest, Vector3.zero);
                    P(h, _cylinder, 0f, .27f, -.03f, .26f, .008f, .28f, look == G.Bead ? a : dark, -25f);
                    h = Hang(HumanBodyBones.Chest, new Vector3(0f, .19f, .16f), new Vector3(0f, -1f, .15f), 50f, .15f, 45f);
                    switch (look)
                    {
                        case G.Pendant:
                            P(h, _sphere, 0f, -.06f, 0f, .07f, .09f, .045f, a);
                            P(h, _sphere, 0f, -.06f, .02f, .035f, .045f, .02f, b, glow: true);
                            break;
                        case G.Mirror:
                            P(h, _cylinder, 0f, -.07f, 0f, .13f, .008f, .13f, b, 90f);
                            P(h, _cylinder, 0f, -.07f, .006f, .1f, .006f, .1f, a, 90f, glow: true);
                            break;
                        case G.Claw:
                            P(h, _sphere, 0f, -.03f, 0f, .03f, .03f, .03f, b);
                            for (int k = -1; k <= 1; k++) P(h, _cube, k * .025f, -.08f, 0f, .016f, .08f, .016f, a, 0f, 0f, k * 18f);
                            break;
                        case G.Tooth:
                            P(h, _sphere, 0f, -.03f, 0f, .03f, .03f, .03f, b);
                            P(h, _cube, 0f, -.09f, 0f, .04f, .11f, .03f, a, 0f, 45f, 8f);
                            break;
                        case G.Paper:
                            P(h, _cube, 0f, -.08f, 0f, .07f, .13f, .006f, a);
                            P(h, _cube, 0f, -.08f, .005f, .014f, .09f, .004f, b);
                            break;
                        case G.Figure:
                            P(h, _capsule, 0f, -.08f, 0f, .045f, .04f, .045f, a);
                            P(h, _sphere, 0f, -.03f, 0f, .04f, .04f, .04f, a);
                            P(h, _cube, 0f, -.08f, .02f, .05f, .012f, .01f, b);
                            break;
                        case G.Bead:
                            P(h, _sphere, 0f, -.05f, 0f, .07f, .07f, .07f, a, glow: true);
                            P(h, _cylinder, 0f, -.1f, 0f, .03f, .03f, .03f, b);
                            break;
                        case G.Holy:
                            P(h, _cube, 0f, -.08f, 0f, .028f, .12f, .014f, a);
                            P(h, _cube, 0f, -.055f, 0f, .085f, .028f, .014f, a);
                            P(h, _sphere, 0f, -.06f, .012f, .03f, .03f, .015f, b, glow: true);
                            break;
                        case G.Crystal:
                            P(h, _cube, 0f, -.07f, 0f, .045f, .1f, .045f, a, 0f, 45f, 0f, true);
                            P(h, _cube, 0f, -.07f, 0f, .03f, .07f, .03f, b, 0f, 0f, 45f, true);
                            break;
                        default:
                            P(h, _sphere, 0f, -.06f, 0f, .07f, .085f, .05f, a);
                            P(h, _sphere, 0f, -.06f, .015f, .03f, .035f, .03f, b);
                            break;
                    }
                    break;

                // ── 팔찌·장갑 (팔찌는 왼 팔목, 장갑은 두 손)
                case G.Band:
                    h = Limb(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, .88f);
                    P(h, _cylinder, 0f, 0f, 0f, .105f, .024f, .105f, a);
                    P(h, _sphere, 0f, 0f, .052f, .035f, .035f, .025f, b, glow: true);
                    break;
                case G.Beads:
                    h = Limb(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, .88f);
                    for (int k = 0; k < 9; k++)
                    {
                        float ang = k * Mathf.PI * 2f / 9f;
                        P(h, _sphere, Mathf.Cos(ang) * .05f, 0f, Mathf.Sin(ang) * .05f, .034f, .034f, .034f, k == 0 ? b : a);
                    }
                    break;
                case G.Bracer:
                    h = Limb(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, .68f);
                    P(h, _cylinder, 0f, 0f, 0f, .11f, .1f, .11f, a);
                    for (int k = -1; k <= 1; k++) P(h, _cylinder, 0f, k * .055f, 0f, .115f, .01f, .115f, b, glow: glow);
                    break;
                case G.FeatherBand:
                    h = Limb(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, .85f);
                    P(h, _cylinder, 0f, 0f, 0f, .105f, .022f, .105f, b);
                    for (int k = -1; k <= 1; k++) P(h, _sphere, k * .03f, -.02f, .06f, .025f, .08f, .01f, a, -60f, 0f, k * 25f);
                    break;
                case G.Glove:
                case G.Gauntlet:
                    foreach (bool left in Sides)
                    {
                        var arm = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
                        var hand = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                        bool gaunt = look == G.Gauntlet;
                        h = Limb(arm, hand, gaunt ? .82f : .92f);
                        P(h, _cylinder, 0f, 0f, 0f, gaunt ? .13f : .11f, gaunt ? .07f : .035f, gaunt ? .13f : .11f, a);
                        P(h, _cylinder, 0f, gaunt ? .07f : .035f, 0f, gaunt ? .135f : .115f, .012f, gaunt ? .135f : .115f, b);
                        h = Limb(hand, left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal, .4f);
                        P(h, _sphere, 0f, 0f, 0f, .1f, .13f, .09f, a);
                        if (gaunt) P(h, _cube, 0f, .02f, 0f, .07f, .06f, .1f, b);
                    }
                    break;

                // ── 허리 (골반뼈, 허리띠는 위로 0.07, 매다는 건 오른쪽 허리 — 주머니는 왼쪽이에요)
                case G.Belt:
                    h = Rigid(HumanBodyBones.Hips, Vector3.zero);
                    P(h, _cylinder, 0f, .07f, 0f, .47f, .05f, .4f, a);
                    P(h, _cube, 0f, .07f, .205f, .1f, .08f, .025f, b, glow: glow);
                    if (glow)
                        for (int k = -1; k <= 1; k += 2) P(h, _cube, k * .15f, .07f, .17f, .035f, .035f, .02f, b, 0f, k * 35f, 0f, true);
                    break;
                case G.ChainBelt:
                    h = Rigid(HumanBodyBones.Hips, Vector3.zero);
                    for (int k = 0; k < 14; k++)
                    {
                        float ang = k * Mathf.PI * 2f / 14f;
                        P(h, _cylinder, Mathf.Sin(ang) * .25f, .06f - Mathf.Abs(Mathf.Sin(ang * .5f)) * .03f, Mathf.Cos(ang) * .21f, .045f, .008f, .03f, k % 2 == 0 ? a : b, 90f, ang * Mathf.Rad2Deg, k % 2 == 0 ? 0f : 90f);
                    }
                    break;
                case G.Flute:
                    h = Rigid(HumanBodyBones.Hips, Vector3.zero);
                    P(h, _cylinder, .16f, .05f, .06f, .028f, .2f, .028f, a, 0f, 0f, -55f);
                    P(h, _cylinder, .16f, .05f, .06f, .032f, .01f, .032f, b, 0f, 0f, -55f);
                    break;
                case G.Scabbard:
                    h = Rigid(HumanBodyBones.Hips, Vector3.zero);
                    P(h, _cylinder, .14f, -.04f, -.16f, .055f, .3f, .04f, a, 25f, 0f, -20f);
                    P(h, _cube, .2f, -.17f, -.21f, .04f, .06f, .045f, b, 25f, 0f, -20f, true);
                    P(h, _cube, .09f, .09f, -.1f, .06f, .03f, .05f, b, 25f, 0f, -20f, true);
                    break;
                case G.Gourd:
                case G.Pot:
                case G.Lamp:
                case G.Pouch:
                case G.Stone:
                case G.Drum:
                case G.Tablet:
                case G.Norigae:
                case G.Mushroom:
                    h = Hang(HumanBodyBones.Hips, new Vector3(.24f, .06f, .05f), Vector3.down, 30f, .25f, 55f);
                    P(h, _cylinder, 0f, -.02f, 0f, .012f, .025f, .012f, dark);
                    switch (look)
                    {
                        case G.Gourd:
                            P(h, _sphere, 0f, -.07f, 0f, .085f, .085f, .085f, a);
                            P(h, _sphere, 0f, -.16f, 0f, .13f, .13f, .13f, a);
                            P(h, _cylinder, 0f, -.11f, 0f, .06f, .01f, .06f, b);
                            break;
                        case G.Pot:
                            P(h, _cylinder, 0f, -.11f, 0f, .12f, .05f, .12f, a);
                            P(h, _sphere, 0f, -.06f, 0f, .1f, .05f, .1f, a);
                            P(h, _sphere, 0f, -.03f, 0f, .035f, .035f, .035f, b, glow: true);
                            break;
                        case G.Lamp:
                            P(h, _sphere, 0f, -.1f, 0f, .11f, .07f, .11f, a);
                            P(h, _sphere, 0f, -.05f, 0f, .035f, .055f, .035f, b, glow: true);
                            break;
                        case G.Pouch:
                            P(h, _sphere, 0f, -.1f, 0f, .12f, .13f, .1f, a);
                            P(h, _cylinder, 0f, -.04f, 0f, .055f, .015f, .055f, b);
                            break;
                        case G.Stone:
                            P(h, _cube, 0f, -.07f, 0f, .09f, .055f, .045f, a, 0f, 0f, 12f);
                            P(h, _sphere, .03f, -.04f, .02f, .025f, .025f, .025f, b, glow: true);
                            break;
                        case G.Drum:
                            P(h, _cylinder, 0f, -.1f, 0f, .14f, .05f, .14f, a, 90f);
                            P(h, _cylinder, 0f, -.1f, .052f, .125f, .004f, .125f, b, 90f);
                            P(h, _cylinder, 0f, -.1f, -.052f, .125f, .004f, .125f, b, 90f);
                            break;
                        case G.Tablet:
                            P(h, _cube, 0f, -.1f, 0f, .09f, .13f, .022f, a);
                            P(h, _cube, 0f, -.1f, .012f, .045f, .08f, .005f, b);
                            break;
                        case G.Norigae:
                            P(h, _sphere, 0f, -.045f, 0f, .055f, .055f, .03f, a);
                            P(h, _sphere, 0f, -.1f, 0f, .055f, .065f, .03f, b);
                            P(h, _cylinder, 0f, -.18f, 0f, .035f, .06f, .035f, a);
                            break;
                        default:
                            P(h, _sphere, 0f, -.08f, 0f, .13f, .055f, .11f, a);
                            P(h, _cylinder, 0f, -.12f, 0f, .035f, .035f, .035f, b);
                            break;
                    }
                    break;

                // ── 무기 장신구: 칼자루 끝(새끼손가락 쪽)에 매단 술
                default:
                    {
                        var hand = Bone(HumanBodyBones.RightHand);
                        var index = Bone(HumanBodyBones.RightIndexProximal);
                        var little = Bone(HumanBodyBones.RightLittleProximal);
                        if (hand == null) break;
                        var pommel = hand.position;
                        if (index != null && little != null) pommel += (little.position - index.position).normalized * .07f * _s;
                        h = HangAt(hand, pommel, Vector3.down, 30f, .35f, 70f);
                        P(h, _cylinder, 0f, -.04f, 0f, .012f, .04f, .012f, a);
                        P(h, _sphere, 0f, -.09f, 0f, .045f, .045f, .045f, a);
                        switch (look)
                        {
                            case G.TasselBlade: P(h, _cube, 0f, -.16f, 0f, .025f, .09f, .01f, b, glow: glow); break;
                            case G.TasselFeather: P(h, _sphere, 0f, -.18f, 0f, .035f, .1f, .012f, b, glow: glow); break;
                            case G.TasselFan: P(h, _cylinder, 0f, -.15f, 0f, .1f, .005f, .1f, b, 90f); break;
                            default: P(h, _sphere, 0f, -.135f, 0f, .04f, .04f, .04f, b, glow: glow); break;
                        }
                        P(h, _cylinder, 0f, look == G.Tassel ? -.21f : -.25f, 0f, .04f, .06f, .04f, a);
                    }
                    break;
            }
        }

        static readonly bool[] Sides = { true, false };

        // 어깨 덮개 (판금·비늘·죽갑·사슬)
        void Pauldrons(Color a, Color b)
        {
            foreach (bool left in Sides)
            {
                var h = Limb(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, .1f);
                P(h, _sphere, 0f, 0f, 0f, .17f, .2f, .17f, a);
                P(h, _cylinder, 0f, -.08f, 0f, .16f, .012f, .16f, b);
            }
        }

        Transform Bone(HumanBodyBones b)
        {
            if (_animator == null) return null;
            var t = _animator.GetBoneTransform(b);
            // 아바타가 가슴뼈를 위가슴(UpperChest)이나 등뼈(Spine)로만 잡았을 수도 있어요
            //   (플레이어 아바타는 Chest를 비워 둬서, 등뼈 아래 "Chest" 뼈를 직접 찾아요 — 다시 만들 때 한 번만)
            if (t == null && b == HumanBodyBones.Chest)
            {
                t = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
                var spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
                if (t == null && spine != null)
                {
                    foreach (Transform child in spine)
                        if (child.name.EndsWith("Chest")) { t = child; break; }
                    if (t == null) t = spine;
                }
            }
            return t;
        }

        // 모델 크기와 몸의 앞 방향: 플레이어 모델은 Animator 축과 몸 앞이 달라서(뒤로 돌아 있어요), 바인드 자세의 두 허벅지로 몸 오른쪽을 잡고
        //   앞 = 오른쪽 × 위 로 정해요 (지금 동작과 상관없이)
        bool Calibrate()
        {
            var hips = Bone(HumanBodyBones.Hips);
            var head = Bone(HumanBodyBones.Head);
            if (hips == null || head == null) return false;
            _s = Mathf.Max(0.1f, Vector3.Distance(hips.position, head.position) / 0.67f);
            _frameOffset = Quaternion.identity;
            var l = Bone(HumanBodyBones.LeftUpperLeg);
            var r = Bone(HumanBodyBones.RightUpperLeg);
            if (l == null || r == null) return true;
            var up = _animator.transform.up;
            var right = Vector3.ProjectOnPlane(BindPosition(r) - BindPosition(l), up);
            if (right.sqrMagnitude < 1e-6f) return true;
            var fwd = Vector3.Cross(right.normalized, up);
            _frameOffset = Quaternion.Inverse(_animator.transform.rotation) * Quaternion.LookRotation(fwd, up);
            return true;
        }

        Quaternion Frame => _animator.transform.rotation * _frameOffset;

        Vector3 BindPosition(Transform bone)
        {
            foreach (var skin in _skins)
            {
                var bones = skin.bones;
                var binds = skin.sharedMesh != null ? skin.sharedMesh.bindposes : null;
                if (binds == null) continue;
                for (int i = 0; i < bones.Length && i < binds.Length; i++)
                    if (bones[i] == bone) return skin.transform.localToWorldMatrix.MultiplyPoint3x4(binds[i].inverse.GetColumn(3));
            }
            return bone.position;
        }

        Transform Holder(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_root, false);
            t.localScale = Vector3.one * _s;
            return t;
        }

        // 모델 기준 방향(앞 +Z · 위 +Y · 오른쪽 +X)을 이 뼈가 바인드 자세일 때 기준으로 뼈에 묶어요 (지금 동작과 상관없게)
        Quaternion BindFrame(Transform bone)
        {
            var frame = Frame;
            foreach (var skin in _skins)
            {
                var bones = skin.bones;
                var binds = skin.sharedMesh != null ? skin.sharedMesh.bindposes : null;
                if (binds == null) continue;
                for (int i = 0; i < bones.Length && i < binds.Length; i++)
                {
                    if (bones[i] != bone) continue;
                    var boneInMesh = binds[i].inverse.rotation;
                    var boneAtBind = skin.transform.rotation * boneInMesh;
                    return Quaternion.Inverse(boneAtBind) * frame;
                }
            }
            return Quaternion.Inverse(bone.rotation) * frame;
        }

        Transform Rigid(HumanBodyBones which, Vector3 offset)
        {
            var bone = Bone(which);
            var h = Holder($"Gear {which}");
            if (bone == null) return h;
            var local = BindFrame(bone);
            var a = new Attach { Bone = bone, Holder = h, Rot = local, Pos = local * (offset * _s) };
            _attaches.Add(a);
            return h;
        }

        // 팔다리: 뼈 줄기(from → to) 위 t 자리, 위(+Y)가 줄기 방향
        Transform Limb(HumanBodyBones from, HumanBodyBones to, float t)
        {
            var a = Bone(from);
            var b = Bone(to);
            var h = Holder($"Gear {from}");
            if (a == null || b == null) return h;
            var axis = (b.position - a.position).normalized;
            var fwd = Vector3.ProjectOnPlane(Frame * Vector3.forward, axis);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(Frame * Vector3.up, axis);
            var rot = Quaternion.LookRotation(fwd.normalized, axis);
            var pos = Vector3.Lerp(a.position, b.position, t);
            _attaches.Add(new Attach
            {
                Bone = a, Holder = h,
                Pos = Quaternion.Inverse(a.rotation) * (pos - a.position),
                Rot = Quaternion.Inverse(a.rotation) * rot,
            });
            return h;
        }

        // 매단 장식: 뼈 기준 offset(모델 방향) 자리에서 rest 방향(모델 기준)으로 처져요
        Transform Hang(HumanBodyBones which, Vector3 offset, Vector3 rest, float stiff, float drag, float maxAngle)
        {
            var bone = Bone(which);
            if (bone == null) return Holder("Gear (뼈 없음)");
            var local = BindFrame(bone);
            var world = bone.position + bone.rotation * (local * (offset * _s));
            return HangAt(bone, world, rest, stiff, drag, maxAngle);
        }

        Transform HangAt(Transform bone, Vector3 world, Vector3 rest, float stiff, float drag, float maxAngle)
        {
            var h = Holder($"Gear Hang {bone.name}");
            _dangles.Add(new Dangle
            {
                Bone = bone, Pivot = h, Pos = Quaternion.Inverse(bone.rotation) * (world - bone.position),
                Rest = rest, Stiff = stiff, Drag = drag, MaxAngle = maxAngle,
            });
            return h;
        }

        void P(Transform parent, Mesh mesh, float x, float y, float z, float sx, float sy, float sz, Color color,
            float rx = 0f, float ry = 0f, float rz = 0f, bool glow = false)
        {
            var go = new GameObject("Piece");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localRotation = Quaternion.Euler(rx, ry, rz);
            go.transform.localScale = new Vector3(sx, sy, sz);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(color, glow);
        }

        Material Mat(Color color, bool glow)
        {
            var key = ((Color32)color, glow);
            if (Mats.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(template) { name = $"Gear {ColorUtility.ToHtmlStringRGB(color)}{(glow ? " glow" : "")}" };
            m.SetColor("_BaseColor", color);
            if (glow) m.SetColor("_AuraColor", new Color(color.r, color.g, color.b, 0.85f));
            Mats[key] = m;
            return m;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        static void Meshes()
        {
            if (_cube != null) return;
            Mesh Grab(PrimitiveType type)
            {
                var go = GameObject.CreatePrimitive(type);
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(go);
                return mesh;
            }
            _cube = Grab(PrimitiveType.Cube);
            _sphere = Grab(PrimitiveType.Sphere);
            _cylinder = Grab(PrimitiveType.Cylinder);
            _capsule = Grab(PrimitiveType.Capsule);
        }
    }
}
