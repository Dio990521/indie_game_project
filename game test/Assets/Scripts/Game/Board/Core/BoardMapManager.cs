using System.Collections.Generic;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 棋盘地图管理器：负责对场景中所有的棋盘地块（MapWaypoint）进行索引和缓存。
    /// 通过 ID 映射提供 O(1) 的常数级查询性能，避免在游戏运行时频繁使用高开销的场景搜索。
    /// 继承自 MonoSingleton，确保在棋盘玩法中全局唯一且易于访问。
    /// </summary>
    public class BoardMapManager : MonoSingleton<BoardMapManager>
    {
        // 核心缓存容器：Key 为地块的唯一 nodeID，Value 为地块组件引用
        private readonly Dictionary<int, MapWaypoint> _nodes = new Dictionary<int, MapWaypoint>();

        // 标识位：标记地块数据是否已成功抓取并建立索引
        private bool _isReady;

        /// <summary> 地图数据是否就绪 </summary>
        public bool IsReady => _isReady;

        private void OnEnable()
        {
            // 订阅游戏模式变更事件：当从其他模式（如探索模式）切回棋盘模式时，自动刷新地图缓存
            EventBus.Subscribe<GameModeChangedEvent>(HandleGameModeChanged);
        }

        private void OnDisable()
        {
            // 销毁或禁用时清理订阅，防止内存泄漏
            EventBus.Unsubscribe<GameModeChangedEvent>(HandleGameModeChanged);
        }

        /// <summary>
        /// 初始化地图缓存：强制扫描当前场景并重新建立索引表。
        /// 该方法由 BoardGameManager 显式驱动，或在数据未就绪时被动触发。
        /// </summary>
        public void Init()
        {
            // 1. 清理旧缓存：防止场景切换或动态加载后残留过期引用
            _nodes.Clear();

            // 2. 场景扫描：一次性获取场景中所有的地块节点。
            // 使用 FindObjectsSortMode.None 以获得最高搜索性能，因为后续会通过 Dictionary 自行组织逻辑顺序。
            MapWaypoint[] waypoints = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);

            // 3. 建立映射：遍历扫描结果，按 ID 存入字典
            for (int i = 0; i < waypoints.Length; i++)
            {
                MapWaypoint node = waypoints[i];
                if (node == null) continue;

                // 将 ID 与节点对象关联，方便后续通过 ID 直接调取位置、连接等信息
                _nodes[node.nodeID] = node;
            }

            // 4. 状态更新：如果找到了地块，则标记为就绪状态
            if (_nodes.Count > 0)
            {
                _isReady = true;
                Debug.Log($"[BoardMapManager] 地图初始化完成，已缓存地块数量: {_nodes.Count}");
            }
        }

        /// <summary>
        /// 根据地块 ID 获取对应的地块对象。
        /// </summary>
        /// <param name="id">目标地块的 nodeID</param>
        /// <returns>找到的地块实例，若 ID 不存在则返回 null</returns>
        public MapWaypoint GetNode(int id)
        {
            if (!_isReady)
            {
                // 延迟初始化 (Lazy Initialization)：
                // 如果外部在地图未准备好时尝试查询（例如在 Awake 中），则立即执行一次初始化。
                Init();
            }

            // 尝试在字典中进行高速检索
            _nodes.TryGetValue(id, out MapWaypoint node);
            return node;
        }

        /// <summary>
        /// 获取当前地图中所有已缓存地块的列表。
        /// 常用于遍历所有节点以进行视觉重置、批量计算或存档处理。
        /// </summary>
        public List<MapWaypoint> GetAllNodes()
        {
            if (!_isReady)
            {
                // 确保外部获取的数据始终是基于最新扫描结果的
                Init();
            }

            // 返回字典 Value 集合的副本，保护内部字典不被外部直接修改
            return new List<MapWaypoint>(_nodes.Values);
        }

        /// <summary>
        /// 监听全局游戏模式变更。
        /// </summary>
        private void HandleGameModeChanged(GameModeChangedEvent evt)
        {
            // 只有在进入“棋盘模式”时才执行初始化。
            // 这样可以确保如果玩家在同一个场景内切换玩法，地图数据能及时同步。
            if (evt.Mode != GameMode.Board) return;

            Init();
        }
    }
}