using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗道具生产系统（纯 C#，由 CombatManager 持有、CombatActiveState 逐帧驱动）：
    /// - 只有"后台（Backline）且存活"的名册成员推进生产；上场暂停（保留进度）、阵亡停止；
    /// - 每个生产周期从该角色的生产特长候选（Definition.ProducibleItems 前 2 个）中随机选一种；
    /// - 生产完成时尝试放入道具栏：同类已达携带上限或无空种类槽 → 进度保持满值挂起
    ///   （"等待空槽"），之后每帧重试直至玩家用掉道具腾出空间；
    /// - 进度变化按整数百分比节流广播 ItemProductionChangedEvent 供 HUD 刷新。
    /// 不消耗任何材料（符合设计：道具生产免费、仅受时间与槽位约束）。
    /// </summary>
    public class ItemProductionSystem
    {
        /// <summary>
        /// 单个成员的生产线状态。
        /// </summary>
        private class ProducerState
        {
            // 归属名册成员
            public RosterMember Member;
            // 生产候选（≤2 个，来自角色定义）
            public readonly List<CombatItemSO> Candidates = new List<CombatItemSO>(2);
            // 当前周期正在生产的道具（null = 下次 Tick 时随机开新周期）
            public CombatItemSO Current;
            // 当前进度（0~1）
            public float Progress;
            // 是否处于"等待空槽"挂起（进度保持满值）
            public bool Waiting;
            // 广播节流：上次广播的整数百分比（-1 = 强制广播一次）
            public int LastBroadcastPercent = -1;
        }

        private readonly List<ProducerState> _producers = new List<ProducerState>(CombatRoster.MaxRosterSize);
        private CombatItemBar _bar;

        /// <summary>
        /// 战斗开始时初始化：为每个有生产候选的名册成员建立生产线。
        /// </summary>
        public void Initialize(CombatRoster roster, CombatItemBar bar)
        {
            Clear();
            _bar = bar;
            if (roster == null) return;

            for (int i = 0; i < roster.Members.Count; i++)
            {
                RosterMember member = roster.Members[i];
                if (member.Definition == null || member.Definition.ProducibleItems == null) continue;

                var producer = new ProducerState { Member = member };
                // 取前 2 个非空候选（对应"战前最多配置 2 个配方"的设计）
                var items = member.Definition.ProducibleItems;
                for (int n = 0; n < items.Count && producer.Candidates.Count < 2; n++)
                {
                    if (items[n] != null) producer.Candidates.Add(items[n]);
                }
                if (producer.Candidates.Count == 0) continue;

                _producers.Add(producer);
            }
        }

        /// <summary>
        /// 逐帧驱动生产（仅战斗进行中由 CombatActiveState 调用）。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_bar == null) return;

            for (int i = 0; i < _producers.Count; i++)
            {
                ProducerState producer = _producers[i];

                // 只有后台且存活的成员推进生产：上场暂停（保留进度）、阵亡停止
                if (producer.Member.State != RosterMemberState.Backline) continue;

                // 开新周期：随机选一种候选道具
                if (producer.Current == null)
                {
                    producer.Current = producer.Candidates[Random.Range(0, producer.Candidates.Count)];
                    producer.Progress = 0f;
                    producer.Waiting = false;
                    producer.LastBroadcastPercent = -1;
                }

                if (producer.Waiting)
                {
                    // 等待空槽：每帧重试投递（进度保持满值）
                    TryDeliver(producer);
                }
                else
                {
                    float productionTime = Mathf.Max(0.1f, producer.Current.ProductionTime);
                    producer.Progress += deltaTime / productionTime;
                    if (producer.Progress >= 1f)
                    {
                        producer.Progress = 1f;
                        TryDeliver(producer);
                    }
                }

                BroadcastThrottled(producer);
            }
        }

        /// <summary>
        /// 清空全部生产线（战斗结束/休眠时调用）。
        /// </summary>
        public void Clear()
        {
            _producers.Clear();
            _bar = null;
        }

        /// <summary>
        /// 调试用：把所有进行中的生产线直接推到完成。
        /// </summary>
        public void DebugCompleteAll()
        {
            for (int i = 0; i < _producers.Count; i++)
            {
                if (_producers[i].Member.State != RosterMemberState.Backline) continue;
                if (_producers[i].Current == null)
                {
                    _producers[i].Current = _producers[i].Candidates[0];
                }
                _producers[i].Progress = 1f;
                TryDeliver(_producers[i]);
                BroadcastThrottled(_producers[i]);
            }
        }

        /// <summary>
        /// 投递产出到道具栏：成功则结束本周期，失败则进入"等待空槽"挂起。
        /// </summary>
        private void TryDeliver(ProducerState producer)
        {
            if (_bar.TryAdd(producer.Current))
            {
                producer.Current = null;
                producer.Progress = 0f;
                producer.Waiting = false;
                producer.LastBroadcastPercent = -1;
            }
            else
            {
                producer.Waiting = true;
            }
        }

        /// <summary>
        /// 整数百分比节流广播（挂起状态变化时 LastBroadcastPercent 会被重置以强制广播）。
        /// </summary>
        private void BroadcastThrottled(ProducerState producer)
        {
            int percent = Mathf.FloorToInt(producer.Progress * 100f);
            if (percent == producer.LastBroadcastPercent) return;
            producer.LastBroadcastPercent = percent;
            EventBus.Raise(new ItemProductionChangedEvent
            {
                Member = producer.Member,
                Progress = producer.Progress,
                Waiting = producer.Waiting
            });
        }
    }
}
