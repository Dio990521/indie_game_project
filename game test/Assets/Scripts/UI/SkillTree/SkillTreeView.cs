using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.SkillTree;

namespace IndieGame.UI.SkillTree
{
    /// <summary>
    /// 技能树 UI 视图层（View）：
    /// 负责技能网格的动态生成、节点状态刷新、SP 文本更新。
    /// 不订阅 EventBus，不判断业务规则，纯粹负责"如何显示"。
    /// </summary>
    public class SkillTreeView : MonoBehaviour
    {
        [SerializeField] private SkillTreeBinder binder;

        // skillId → 当前活跃节点，用于按 ID 精确刷新
        private readonly Dictionary<string, SkillNodeUI> _activeNodes
            = new Dictionary<string, SkillNodeUI>();

        // 对象池，复用节点避免每次重建都 Instantiate/Destroy
        private readonly List<SkillNodeUI> _nodePool = new List<SkillNodeUI>();

        /// <summary>
        /// 通过 CanvasGroup 控制整体显隐（无动画，即时切换）。
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (binder == null || binder.CanvasGroup == null) return;
            binder.CanvasGroup.alpha          = visible ? 1f : 0f;
            binder.CanvasGroup.blocksRaycasts = visible;
            binder.CanvasGroup.interactable   = visible;
        }

        /// <summary>
        /// 刷新 SP 数值文本。
        /// </summary>
        public void RefreshSP(int currentSP)
        {
            if (binder == null || binder.SpValueText == null) return;
            binder.SpValueText.text = $"技能点：{Mathf.Max(0, currentSP)}";
        }

        /// <summary>
        /// 更新 Tab 高亮，仅切换高亮对象，不重建技能列表。
        /// </summary>
        public void RefreshTabHighlight(int activeTabIndex)
        {
            if (binder == null || binder.CategoryTabHighlights == null) return;
            for (int i = 0; i < binder.CategoryTabHighlights.Length; i++)
            {
                if (binder.CategoryTabHighlights[i] != null)
                    binder.CategoryTabHighlights[i].SetActive(i == activeTabIndex);
            }
        }

        /// <summary>
        /// 重建当前 Tab 的技能网格。
        /// skillsForTab：当前分类下的技能列表，由 Controller 传入；
        /// stateGetter：按 ID 查询 SkillLearnState 的委托，由 Controller 提供；
        /// nodeClickCallback：节点点击后的回调，由 Controller 提供。
        /// </summary>
        public void RebuildSkillGrid(
            IReadOnlyList<SkillDataSO> skillsForTab,
            Func<string, SkillLearnState> stateGetter,
            Action<string> nodeClickCallback)
        {
            if (binder == null) return;

            ReleaseAllNodes();

            bool hasSkills = skillsForTab != null && skillsForTab.Count > 0;

            if (binder.EmptyStateNode != null)
                binder.EmptyStateNode.SetActive(!hasSkills);

            if (!hasSkills) return;

            foreach (SkillDataSO data in skillsForTab)
            {
                if (data == null) continue;

                SkillNodeUI node = GetOrCreateNode();
                if (node == null) continue;

                node.transform.SetParent(binder.SkillGridRoot, false);
                node.gameObject.SetActive(true);

                SkillLearnState state = stateGetter != null
                    ? stateGetter(data.SkillId)
                    : SkillLearnState.Locked;

                node.OnNodeClicked = nodeClickCallback;
                node.Setup(data, state);

                _activeNodes[data.SkillId] = node;
            }
        }

        /// <summary>
        /// 刷新指定技能节点的状态（学习成功后单独调用，避免整体重建）。
        /// </summary>
        public void RefreshNodeState(string skillId, SkillLearnState newState)
        {
            if (_activeNodes.TryGetValue(skillId, out SkillNodeUI node))
                node.RefreshState(newState);
        }

        /// <summary>
        /// 刷新当前网格内所有节点的状态（SP 变化或技能解锁后整体同步）。
        /// </summary>
        public void RefreshAllNodeStates(Func<string, SkillLearnState> stateGetter)
        {
            if (stateGetter == null) return;
            foreach (var kv in _activeNodes)
                kv.Value.RefreshState(stateGetter(kv.Key));
        }

        // ─── 对象池 ───────────────────────────────────────────

        private SkillNodeUI GetOrCreateNode()
        {
            if (binder.SkillNodePrefab == null)
            {
                DebugTools.LogWarning("[SkillTreeView] SkillNodePrefab 未配置，无法生成节点。");
                return null;
            }

            // 从池中取未激活的节点
            for (int i = _nodePool.Count - 1; i >= 0; i--)
            {
                SkillNodeUI pooled = _nodePool[i];
                if (pooled != null && !pooled.gameObject.activeSelf)
                {
                    _nodePool.RemoveAt(i);
                    return pooled;
                }
            }

            // 池中无空闲，新建
            GameObject go = Instantiate(binder.SkillNodePrefab);
            SkillNodeUI node = go.GetComponent<SkillNodeUI>();
            if (node == null)
            {
                DebugTools.LogWarning("[SkillTreeView] SkillNodePrefab 上缺少 SkillNodeUI 组件。");
                Destroy(go);
                return null;
            }
            return node;
        }

        private void ReleaseAllNodes()
        {
            foreach (var kv in _activeNodes)
            {
                if (kv.Value != null)
                {
                    kv.Value.gameObject.SetActive(false);
                    kv.Value.OnNodeClicked = null;
                    _nodePool.Add(kv.Value);
                }
            }
            _activeNodes.Clear();
        }

        private void OnDestroy()
        {
            foreach (SkillNodeUI node in _nodePool)
            {
                if (node != null) Destroy(node.gameObject);
            }
            _nodePool.Clear();
        }
    }
}
