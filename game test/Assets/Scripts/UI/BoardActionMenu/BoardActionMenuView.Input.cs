using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;

namespace IndieGame.UI
{
    /// <summary>
    /// BoardActionMenuView 的键盘导航 partial：
    /// 包含"3 行 x 2 列网格式方向键选择"的全部输入逻辑——
    /// 输入订阅、轴节流（新按键立即响应 + 按住连发）、侧/行切换、选中高亮。
    /// 确认键的业务分发在主文件的 ConfirmSelection 中（转交 BoardActionDispatcher）。
    /// </summary>
    public partial class BoardActionMenuView
    {
        /// <summary>
        /// 处理移动输入：将左右两侧按钮视为一个 3 行 x 2 列的网格。
        /// 水平方向（A/D 或左右方向键）在“左侧栏 / 右侧栏”之间切换；
        /// 垂直方向（W/S 或上下方向键）在当前所在侧内部按行循环切换。
        /// 复用现有的 Move 输入轴（与角色移动共用同一组按键），无需额外绑定。
        ///
        /// 两个轴各自独立做“新按键立即响应 + 按住连发”的节流判定，互不干扰；
        /// 节流逻辑只用于“按住不放”时的自动连发，新的单次按键（方向从中立或反方向切换过来）
        /// 一律立即响应，避免快速连续按键时因节流而出现选择顿挫、漏按的问题。
        /// </summary>
        private void OnMoveInput(InputMoveEvent evt)
        {
            Vector2 v = evt.Value;
            if (_options.Count == 0) { _lastInputX = v.x; _lastInputY = v.y; return; }

            ProcessAxis(v.x, ref _lastInputX, ref _nextInputTime, MoveSide);
            // 屏幕纵坐标向上为正，而 row 0 在最上方，因此按“上”时需要让 row 减小，符号取反
            ProcessAxis(v.y, ref _lastInputY, ref _nextInputTimeY, dir => MoveRow(-dir));
        }

        /// <summary>
        /// 单个输入轴的“新按键立即响应 + 按住连发”节流通用逻辑。
        /// </summary>
        private void ProcessAxis(float value, ref float lastValue, ref float nextTime, Action<int> onStep)
        {
            bool wasActive = Mathf.Abs(lastValue) > 0.5f;
            bool isActive = Mathf.Abs(value) > 0.5f;
            bool isNewPress = isActive && (!wasActive || Mathf.Sign(value) != Mathf.Sign(lastValue));

            if (isNewPress)
            {
                onStep(value > 0f ? 1 : -1);
                nextTime = Time.time + inputRepeatDelay;
            }
            else if (isActive && Time.time >= nextTime)
            {
                onStep(value > 0f ? 1 : -1);
                nextTime = Time.time + inputRepeatDelay;
            }

            lastValue = value;
        }

        /// <summary>
        /// 左右切换当前选中所在的侧（+1 右，-1 左）；已在最左/最右侧时不循环。
        /// 若目标侧的行数少于当前行号，则夹取到该侧最后一行。
        /// </summary>
        private void MoveSide(int direction)
        {
            int targetSide = direction > 0 ? 1 : 0;
            if (targetSide == _selectedSide) return;

            List<int> target = targetSide == 0 ? _leftFlatIndices : _rightFlatIndices;
            if (target.Count == 0) return;

            int row = Mathf.Min(_selectedRow, target.Count - 1);
            SelectSideRow(targetSide, row);
        }

        /// <summary>
        /// 在当前侧内部上下循环切换选中行（+1 向下，-1 向上）。
        /// </summary>
        private void MoveRow(int direction)
        {
            List<int> current = _selectedSide == 0 ? _leftFlatIndices : _rightFlatIndices;
            if (current.Count == 0) return;

            int row = (_selectedRow + direction + current.Count) % current.Count;
            SelectSideRow(_selectedSide, row);
        }

        /// <summary>
        /// 处理交互输入（E 键 / Interact 输入 -> 确认当前选中的选项）。
        /// </summary>
        private void OnInteractInput(InputInteractEvent evt)
        {
            ConfirmSelection();
        }

        /// <summary>
        /// 选中指定侧、指定行的按钮，并更新所有按钮的高亮缩放。
        /// </summary>
        private void SelectSideRow(int side, int row, bool instant = false)
        {
            List<int> list = side == 0 ? _leftFlatIndices : _rightFlatIndices;
            if (list.Count == 0) return;
            row = Mathf.Clamp(row, 0, list.Count - 1);

            _selectedSide = side;
            _selectedRow = row;
            _selectedIndex = list[row];

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] == null || !_buttons[i].gameObject.activeSelf) continue;
                bool isSelected = i == _selectedIndex;
                _buttons[i].SetSelected(isSelected, isSelected ? selectedScale : normalScale, instant ? 0f : selectTweenDuration);
            }
        }

        /// <summary>
        /// 订阅输入事件（EventBus）。
        /// </summary>
        private void SubscribeInput()
        {
            if (_inputSubscribed) return;
            EventBus.Subscribe<InputMoveEvent>(OnMoveInput);
            EventBus.Subscribe<InputInteractEvent>(OnInteractInput);
            _inputSubscribed = true;
        }

        /// <summary>
        /// 退订输入事件。
        /// </summary>
        private void UnsubscribeInput()
        {
            if (!_inputSubscribed) return;
            EventBus.Unsubscribe<InputMoveEvent>(OnMoveInput);
            EventBus.Unsubscribe<InputInteractEvent>(OnInteractInput);
            _inputSubscribed = false;
        }
    }
}
