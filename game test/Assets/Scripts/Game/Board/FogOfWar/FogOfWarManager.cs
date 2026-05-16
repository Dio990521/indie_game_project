using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.FogOfWar
{
    /// <summary>
    /// 大世界战争迷雾管理器（单例）。
    /// 进入棋盘模式时自动从节点数据计算世界边界并同步 Quad；
    /// 通过 Update 轮询 + PlayerReachedNodeEvent 双重触发揭开迷雾，支持存档还原。
    /// </summary>
    public class FogOfWarManager : SaveableMonoSingleton<FogOfWarManager>
    {
        [SerializeField] private FogOfWarConfig _config;
        // 迷雾层 Quad 的 Renderer（在 Inspector 中拖入）
        [SerializeField] private Renderer _fogQuadRenderer;

        private Texture2D _fogTexture;
        private Material  _fogMaterial;
        private bool      _isTrackingActive;
        // 正无穷保证首次移动一定触发揭开
        private Vector3   _lastRevealedPos = Vector3.positiveInfinity;

        // 运行时计算的世界 XZ 边界（自动对齐棋盘节点范围，无需手动配置）
        private Vector2 _worldMin;
        private Vector2 _worldMax;

        // ── 生命周期 ──────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            // 先用 config 兜底值初始化，进入棋盘模式后自动刷新
            InitBoundsFromConfig();
            InitTexture();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<GameModeChangedEvent>(OnGameModeChanged);
            // 每步落地保底揭开，不依赖 Update 阈值
            EventBus.Subscribe<PlayerReachedNodeEvent>(OnPlayerReachedNode);
            // 处理事件在本组件启用前已发布的情况
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.BoardMode)
            {
                _isTrackingActive = true;
                StartCoroutine(RefreshWorldBoundsNextFrame());
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<GameModeChangedEvent>(OnGameModeChanged);
            EventBus.Unsubscribe<PlayerReachedNodeEvent>(OnPlayerReachedNode);
            _isTrackingActive = false;
        }

        private void Update()
        {
            if (!_isTrackingActive) return;

            GameObject player = GameManager.Instance?.CurrentPlayer;
            if (player == null) return;

            Vector3 pos = player.transform.position;
            if (Vector3.Distance(pos, _lastRevealedPos) >= _config.updateThreshold)
            {
                RevealAt(pos);
                _lastRevealedPos = pos;
            }
        }

        // ── 世界边界：自动计算 ────────────────────────────────────────────────

        private void InitBoundsFromConfig()
        {
            float halfW = _config.worldSize.x * 0.5f;
            float halfH = _config.worldSize.y * 0.5f;
            _worldMin = new Vector2(_config.worldCenter.x - halfW, _config.worldCenter.y - halfH);
            _worldMax = new Vector2(_config.worldCenter.x + halfW, _config.worldCenter.y + halfH);
        }

        // 等一帧再取节点，确保 BoardMapManager 已完成 Init()
        private IEnumerator RefreshWorldBoundsNextFrame()
        {
            yield return null;
            RefreshWorldBounds();
        }

        private void RefreshWorldBounds()
        {
            List<MapWaypoint> nodes = BoardMapManager.Instance?.GetAllNodes();
            if (nodes == null || nodes.Count == 0)
            {
                // BoardMapManager 尚未就绪，回退到 config 手动值
                InitBoundsFromConfig();
                return;
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (MapWaypoint node in nodes)
            {
                Vector3 p = node.transform.position;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }

            // padding = 一个揭开半径，保证边缘节点圆圈不被裁切
            float pad = _config.revealRadius;
            _worldMin = new Vector2(minX - pad, minZ - pad);
            _worldMax = new Vector2(maxX + pad, maxZ + pad);

            SyncFogQuadToWorldBounds();
        }

        // 自动对齐迷雾 Quad 的 XZ 位置和缩放，无需 Inspector 手动设置
        private void SyncFogQuadToWorldBounds()
        {
            if (_fogQuadRenderer == null) return;
            Vector2 size   = _worldMax - _worldMin;
            Vector2 center = (_worldMin + _worldMax) * 0.5f;
            Transform t    = _fogQuadRenderer.transform;
            // 只更新 XZ，保留 Y（高度由用户在 Inspector 中设定）
            t.position   = new Vector3(center.x, t.position.y, center.y);
            // Quad 以 Rotation(90,0,0) 放置：localScale.x=世界X范围，localScale.y=世界Z范围
            t.localScale = new Vector3(size.x, size.y, 1f);
        }

        // ── 初始化纹理 ────────────────────────────────────────────────────────

        private void InitTexture()
        {
            int res = _config.textureResolution;
            _fogTexture = new Texture2D(res, res, TextureFormat.R8, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp
            };

            // 全黑 = 全部迷雾
            NativeArray<byte> raw = _fogTexture.GetRawTextureData<byte>();
            for (int i = 0; i < raw.Length; i++) raw[i] = 0;
            _fogTexture.Apply(false);

            _fogMaterial = _fogQuadRenderer.material;
            _fogMaterial.SetTexture("_FogTex", _fogTexture);
            _fogMaterial.SetColor("_FogColor", _config.fogColor);
        }

        // ── 核心：圆形揭开 ────────────────────────────────────────────────────

        /// <summary>单点揭开（普通移动每帧调用）。</summary>
        public void RevealAt(Vector3 worldPos)
        {
            int res = _config.textureResolution;
            int r   = CalculateRadiusPixels(res);
            NativeArray<byte> raw = _fogTexture.GetRawTextureData<byte>();
            WriteCircle(raw, WorldToPixelX(worldPos.x), WorldToPixelZ(worldPos.z), r, res);
            _fogTexture.Apply(false);
        }

        /// <summary>以自定义半径揭开指定位置的迷雾（供雷达格等特殊 Tile 调用）。</summary>
        public void RevealAreaAt(Vector3 worldPos, float radius)
        {
            int res = _config.textureResolution;
            int r   = CalculateRadiusPixels(res, radius);
            NativeArray<byte> raw = _fogTexture.GetRawTextureData<byte>();
            WriteCircle(raw, WorldToPixelX(worldPos.x), WorldToPixelZ(worldPos.z), r, res);
            _fogTexture.Apply(false);
        }

        /// <summary>
        /// 查询世界坐标是否已被迷雾揭开。
        /// R8 纹理：像素值 > 127 视为已揭开（白），否则视为仍在迷雾中（黑）。
        /// 使用 GetRawTextureData 直接读取原始字节，不触发 CPU 回读开销。
        /// </summary>
        /// <param name="worldPos">要查询的世界坐标（Y 轴忽略，只用 X/Z）</param>
        public bool IsRevealedAt(Vector3 worldPos)
        {
            if (_fogTexture == null) return false;

            int res = _config.textureResolution;
            int px  = Mathf.Clamp(WorldToPixelX(worldPos.x), 0, res - 1);
            int pz  = Mathf.Clamp(WorldToPixelZ(worldPos.z), 0, res - 1);

            // R8 格式行主序：索引 pz * res + px（pz 对应行/Y，px 对应列/X）
            Unity.Collections.NativeArray<byte> raw = _fogTexture.GetRawTextureData<byte>();
            return raw[pz * res + px] > 127;
        }

        /// <summary>
        /// 直线路径批量揭开（大炮弹射落地后调用）。
        /// 沿 XZ 直线采样若干圆圈后一次性上传，避免逐帧 Apply 开销。
        /// </summary>
        public void RevealLine(Vector3 from, Vector3 to)
        {
            float xzDist = Mathf.Sqrt(
                (to.x - from.x) * (to.x - from.x) + (to.z - from.z) * (to.z - from.z));
            // 间距 = 半径 × 1.5，保证相邻圆有重叠
            int samples = Mathf.Max(1, Mathf.CeilToInt(xzDist / (_config.revealRadius * 1.5f)));

            int res = _config.textureResolution;
            int r   = CalculateRadiusPixels(res);
            NativeArray<byte> raw = _fogTexture.GetRawTextureData<byte>();

            for (int i = 0; i <= samples; i++)
            {
                float t  = (float)i / samples;
                float wx = Mathf.Lerp(from.x, to.x, t);
                float wz = Mathf.Lerp(from.z, to.z, t);
                WriteCircle(raw, WorldToPixelX(wx), WorldToPixelZ(wz), r, res);
            }
            _fogTexture.Apply(false);
        }

        // 世界半径 → 像素半径（使用 config 默认半径）
        private int CalculateRadiusPixels(int res) => CalculateRadiusPixels(res, _config.revealRadius);

        // 世界半径 → 像素半径（接受任意世界单位半径）
        private int CalculateRadiusPixels(int res, float worldRadius)
        {
            Vector2 worldSize = _worldMax - _worldMin;
            // 防止边界未初始化导致除零
            if (worldSize.x <= 0 || worldSize.y <= 0) return 1;
            float ppu = (res / worldSize.x + res / worldSize.y) * 0.5f;
            return Mathf.Max(1, Mathf.RoundToInt(worldRadius * ppu));
        }

        // 向原始字节数组写入实心圆（不调用 Apply，供批量操作使用）
        private static void WriteCircle(NativeArray<byte> raw, int cx, int cy, int r, int res)
        {
            int rSq  = r * r;
            int yMin = Mathf.Max(0, cy - r);
            int yMax = Mathf.Min(res - 1, cy + r);
            int xMin = Mathf.Max(0, cx - r);
            int xMax = Mathf.Min(res - 1, cx + r);
            for (int y = yMin; y <= yMax; y++)
            {
                int dy = y - cy;
                for (int x = xMin; x <= xMax; x++)
                {
                    int dx = x - cx;
                    if (dx * dx + dy * dy <= rSq)
                        raw[y * res + x] = 255;
                }
            }
        }

        // ── 事件处理 ──────────────────────────────────────────────────────────

        private void OnGameModeChanged(GameModeChangedEvent evt)
        {
            _isTrackingActive = evt.Mode == GameMode.Board;
            if (_isTrackingActive)
                StartCoroutine(RefreshWorldBoundsNextFrame());
        }

        // 每步落地保底揭开：不论 Update 轮询状态如何，节点到达必触发
        private void OnPlayerReachedNode(PlayerReachedNodeEvent evt)
        {
            if (evt.Node != null)
                RevealAt(evt.Node.transform.position);
        }

        // ── 坐标映射（使用运行时边界）────────────────────────────────────────

        private int WorldToPixelX(float wx)
        {
            float range = _worldMax.x - _worldMin.x;
            if (range <= 0) return 0;
            float t = (wx - _worldMin.x) / range;
            return Mathf.Clamp((int)(t * _config.textureResolution), 0, _config.textureResolution - 1);
        }

        private int WorldToPixelZ(float wz)
        {
            float range = _worldMax.y - _worldMin.y;
            if (range <= 0) return 0;
            float t = (wz - _worldMin.y) / range;
            return Mathf.Clamp((int)(t * _config.textureResolution), 0, _config.textureResolution - 1);
        }

        // ── 存档接口 ──────────────────────────────────────────────────────────

        public override string SaveID => "FogOfWar";

        public override object CaptureState()
        {
            return new FogSaveState
            {
                TextureData = Convert.ToBase64String(_fogTexture.GetRawTextureData()),
                Resolution  = _config.textureResolution
            };
        }

        public override void RestoreState(object data)
        {
            if (data is not FogSaveState state) return;
            _fogTexture.LoadRawTextureData(Convert.FromBase64String(state.TextureData));
            _fogTexture.Apply(false);
            _lastRevealedPos = Vector3.positiveInfinity;
        }

        [Serializable]
        private class FogSaveState
        {
            public string TextureData;
            public int    Resolution;
        }
    }
}
