using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Gameplay.Exploration
{
    /// <summary>
    /// 出生点注册表（全局服务类）：
    /// 负责维护“LocationID -> SpawnPoint”的映射关系。
    /// 该类集中管理注册/注销与查询逻辑，让 SpawnPoint 只承担“标记位置”的职责。
    /// </summary>
    public static class SpawnPointRegistry
    {
        // 内部注册表：保存当前场景激活的出生点引用
        private static readonly Dictionary<LocationID, SpawnPoint> Registry = new Dictionary<LocationID, SpawnPoint>();

        /// <summary>
        /// 注册一个出生点。
        /// </summary>
        public static void Register(SpawnPoint spawnPoint)
        {
            if (spawnPoint == null)
            {
                CleanupNulls();
                return;
            }

            LocationID id = spawnPoint.LocationId;
            if (id == null)
            {
                Debug.LogWarning("[SpawnPointRegistry] SpawnPoint 缺失 LocationID，无法注册。");
                return;
            }

            CleanupNulls();

            // 同 ID 覆盖策略：后注册的覆盖先注册的（保持与旧逻辑一致）
            Registry[id] = spawnPoint;
        }

        /// <summary>
        /// 注销一个出生点。
        /// </summary>
        public static void Unregister(SpawnPoint spawnPoint)
        {
            if (spawnPoint == null)
            {
                CleanupNulls();
                return;
            }

            LocationID id = spawnPoint.LocationId;
            if (id == null) return;

            if (Registry.TryGetValue(id, out SpawnPoint existing) && existing == spawnPoint)
            {
                Registry.Remove(id);
            }

            CleanupNulls();
        }

        /// <summary>
        /// 按 LocationID 查找出生点。
        /// </summary>
        public static bool TryGet(LocationID id, out SpawnPoint spawnPoint)
        {
            CleanupNulls();
            if (id == null)
            {
                spawnPoint = null;
                return false;
            }
            return Registry.TryGetValue(id, out spawnPoint);
        }

        /// <summary>
        /// 清理注册表中的 null 引用，防止脏数据堆积。
        /// </summary>
        private static void CleanupNulls()
        {
            if (Registry.Count == 0) return;
            List<LocationID> toRemove = null;
            foreach (var pair in Registry)
            {
                if (pair.Value != null) continue;
                if (toRemove == null) toRemove = new List<LocationID>();
                toRemove.Add(pair.Key);
            }
            if (toRemove == null) return;
            for (int i = 0; i < toRemove.Count; i++)
            {
                Registry.Remove(toRemove[i]);
            }
        }
    }
}
