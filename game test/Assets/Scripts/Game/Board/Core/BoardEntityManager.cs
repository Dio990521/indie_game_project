using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// BoardEntity 管理器（单例）。
    /// 负责统一管理场景中所有 BoardEntity 的注册/注销与高效查询，
    /// 以替代 BoardEntity 内部的静态列表，降低耦合并集中生命周期管理。
    /// </summary>
    public class BoardEntityManager : MonoSingleton<BoardEntityManager>
    {
        // 内部缓存：保存当前场景所有存活的 BoardEntity 引用
        private readonly List<BoardEntity> _entities = new List<BoardEntity>();

        /// <summary>
        /// 只读访问当前缓存的实体列表。
        /// 注意：这是只读视图，外部不可修改集合内容。
        /// </summary>
        public IReadOnlyList<BoardEntity> Entities => _entities;

        /// <summary>
        /// 注册实体（通常在 BoardEntity.OnEnable 调用）。
        /// </summary>
        /// <param name="entity">要注册的实体</param>
        public void Register(BoardEntity entity)
        {
            if (entity == null) return;
            // 清理脏引用，避免列表不断增长
            CleanupNulls();
            if (_entities.Contains(entity)) return;
            _entities.Add(entity);
        }

        /// <summary>
        /// 注销实体（通常在 BoardEntity.OnDisable 调用）。
        /// 会安全移除指定实体，并同步清理 null 脏引用。
        /// </summary>
        /// <param name="entity">要注销的实体</param>
        public void Unregister(BoardEntity entity)
        {
            if (entity == null)
            {
                // 仍然清理一次，避免残留 null
                CleanupNulls();
                return;
            }

            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                if (_entities[i] == null || _entities[i] == entity)
                {
                    _entities.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 清理列表中的 null 引用（脏数据保护）。
        /// </summary>
        private void CleanupNulls()
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                if (_entities[i] == null)
                {
                    _entities.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 查找场景中第一个 NPC（非玩家实体）。
        /// </summary>
        public BoardEntity FindFirstNpc()
        {
            CleanupNulls();
            for (int i = 0; i < _entities.Count; i++)
            {
                BoardEntity entity = _entities[i];
                if (entity != null && !entity.IsPlayer) return entity;
            }
            return null;
        }

        /// <summary>
        /// 查找是否有其他实体停留在指定节点（排除自身）。
        /// </summary>
        public BoardEntity FindOtherAtNode(MapWaypoint node, BoardEntity exclude)
        {
            if (node == null) return null;
            CleanupNulls();
            for (int i = 0; i < _entities.Count; i++)
            {
                BoardEntity entity = _entities[i];
                if (entity == null || entity == exclude) continue;
                if (entity.CurrentNode == node) return entity;
            }
            return null;
        }
    }
}
