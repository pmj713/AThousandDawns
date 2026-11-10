using AThousandDawns.Player;
using AThousandDawns.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.Enemies
{
    // 녹림왕 2페이즈 "불타는 죽림": 결투장 바깥 2칸에 불길. 2초 예고 뒤 불이 붙고, 20초마다 안전한 곳이 가로·세로 1칸씩 좁아져요 (최소 10×6).
    // 불길 안에 있으면 0.5초마다 화상 1중첩.
    public sealed class BurningGrove : MonoBehaviour
    {
        Vector3 _center;
        Vector2 _outer, _safe, _minSafe;
        float _windup, _shrinkInterval, _shrinkStep, _tick, _burnPerStack;
        float _elapsed, _shrinkTimer, _tickTimer;
        PlayerController _player;
        Material _warnMaterial, _fireMaterial;
        readonly Transform[] _strips = new Transform[4];
        readonly Renderer[] _renderers = new Renderer[4];

        public bool IsBurning => _elapsed >= _windup;
        public Vector2 SafeSize => _safe;

        public static BurningGrove Create(Vector3 center, Vector2 zoneSize, float band, float windup, float shrinkInterval, float shrinkStep,
            Vector2 minSafe, float tick, float burnPerStack, PlayerController player, Material warn, Material fire)
        {
            var go = new GameObject("불타는 죽림");
            var grove = go.AddComponent<BurningGrove>();
            grove._center = new Vector3(center.x, 0f, center.z);
            grove._outer = zoneSize;
            grove._safe = new Vector2(Mathf.Max(minSafe.x, zoneSize.x - band * 2f), Mathf.Max(minSafe.y, zoneSize.y - band * 2f));
            grove._minSafe = minSafe;
            grove._windup = windup;
            grove._shrinkInterval = shrinkInterval;
            grove._shrinkStep = shrinkStep;
            grove._tick = Mathf.Max(0.05f, tick);
            grove._burnPerStack = burnPerStack;
            grove._player = player;
            grove._warnMaterial = warn;
            grove._fireMaterial = fire != null ? fire : warn;
            for (int i = 0; i < 4; i++)
            {
                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = $"불길_{i + 1}";
                var collider = strip.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                strip.transform.SetParent(go.transform, false);
                var renderer = strip.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (warn != null) renderer.sharedMaterial = warn;
                grove._strips[i] = strip.transform;
                grove._renderers[i] = renderer;
            }
            grove.Layout();
            return grove;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool wasBurning = IsBurning;
            _elapsed += dt;

            if (!IsBurning)
            {
                // 예고: 깜빡여요
                bool on = Mathf.Repeat(_elapsed, 0.3f) < 0.18f;
                foreach (var r in _renderers) r.enabled = on;
                return;
            }
            if (!wasBurning)
            {
                Sfx.Play(SfxId.GroveIgnite);
                foreach (var r in _renderers)
                {
                    r.enabled = true;
                    if (_fireMaterial != null) r.sharedMaterial = _fireMaterial;
                }
            }

            _shrinkTimer += dt;
            if (_shrinkTimer >= _shrinkInterval)
            {
                _shrinkTimer -= _shrinkInterval;
                if (_safe.x > _minSafe.x || _safe.y > _minSafe.y) Sfx.Play(SfxId.GroveShrink, 0.7f); // 다 좁혀진 뒤엔 조용히
                _safe = new Vector2(Mathf.Max(_minSafe.x, _safe.x - _shrinkStep), Mathf.Max(_minSafe.y, _safe.y - _shrinkStep));
                Layout();
            }

            _tickTimer += dt;
            while (_tickTimer >= _tick)
            {
                _tickTimer -= _tick;
                if (_player != null && InFire(_player.transform.position)) _player.AddBurnStack(_burnPerStack);
            }
        }

        public bool InFire(Vector3 p)
        {
            float dx = Mathf.Abs(p.x - _center.x), dz = Mathf.Abs(p.z - _center.z);
            bool insideZone = dx <= _outer.x * 0.5f && dz <= _outer.y * 0.5f;
            bool insideSafe = dx <= _safe.x * 0.5f && dz <= _safe.y * 0.5f;
            return insideZone && !insideSafe;
        }

        // 바깥 사각형과 안전한 사각형 사이를 띠 4개로 채워요 (북·남·서·동).
        void Layout()
        {
            float ow = _outer.x * 0.5f, od = _outer.y * 0.5f, sw = _safe.x * 0.5f, sd = _safe.y * 0.5f;
            float bandZ = od - sd, bandX = ow - sw;
            SetStrip(0, new Vector3(0f, 0f, (od + sd) * 0.5f), new Vector3(_outer.x, 0.06f, bandZ));
            SetStrip(1, new Vector3(0f, 0f, -(od + sd) * 0.5f), new Vector3(_outer.x, 0.06f, bandZ));
            SetStrip(2, new Vector3(-(ow + sw) * 0.5f, 0f, 0f), new Vector3(bandX, 0.06f, _safe.y));
            SetStrip(3, new Vector3((ow + sw) * 0.5f, 0f, 0f), new Vector3(bandX, 0.06f, _safe.y));
        }

        void SetStrip(int i, Vector3 offset, Vector3 size)
        {
            _strips[i].position = _center + offset + Vector3.up * 0.04f;
            _strips[i].localScale = size;
        }
    }
}
