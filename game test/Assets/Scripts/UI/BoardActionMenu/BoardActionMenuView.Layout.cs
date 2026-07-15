using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.UI
{
    /// <summary>
    /// BoardActionMenuView 的圆弧布局 partial：
    /// 只包含"按钮在左右两侧圆弧上的位置计算"这一块纯数学逻辑，
    /// 与显示/输入/动画解耦，便于单独调整布局算法。
    /// </summary>
    public partial class BoardActionMenuView
    {
        /// <summary>
        /// 计算并设置按钮在圆弧布局中的位置：
        /// 左侧按钮以玩家左侧（180°）为中心呈弧形纵向展开，右侧按钮以玩家右侧（0°）为中心呈弧形纵向展开；
        /// 每侧内部 row 0 始终在最上方，向下依次排列，与方向键的“3x2 网格”选择逻辑一一对应。
        /// </summary>
        private void LayoutButtons()
        {
            LayoutSideArc(_leftFlatIndices, 180f, flipStep: true);
            LayoutSideArc(_rightFlatIndices, 0f, flipStep: false);
        }

        /// <summary>
        /// 将某一侧的按钮沿圆弧纵向排列（row 0 在最上方，中间行朝该侧鼓出最多）。
        /// </summary>
        /// <param name="flatIndices">该侧按钮在 _buttons 中的索引列表，按行从上到下排列</param>
        /// <param name="centerAngle">该侧圆弧中心角度：左侧 180°，右侧 0°</param>
        /// <param name="flipStep">左右两侧的角度增长方向相反，用于保证 row 0 始终位于最上方</param>
        private void LayoutSideArc(List<int> flatIndices, float centerAngle, bool flipStep)
        {
            int count = flatIndices.Count;
            if (count == 0) return;

            float half = arcAngle * 0.5f * (flipStep ? -1f : 1f);
            float step = (count > 1 ? arcAngle / (count - 1) : 0f) * (flipStep ? 1f : -1f);
            float startAngle = centerAngle + half;

            for (int row = 0; row < count; row++)
            {
                BoardActionButton button = _buttons[flatIndices[row]];
                if (button == null) continue;
                float angle = startAngle + step * row;
                float rad = angle * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius) + offset;

                RectTransform rt = button.GetComponent<RectTransform>();
                rt.anchoredPosition = pos;
            }
        }
    }
}
