using System.Collections.Generic;

namespace IndieGame.Gameplay.Stats
{
    /// <summary>
    /// 数值属性封装类：
    /// 用于管理“基础值 + 加成列表”组成的最终数值，并提供延迟刷新机制。
    /// </summary>
    public class Stat
    {
        // --- 内部字段 ---
        // 基础值（不含任何临时加成）
        private float _baseValue;
        // 缓存的最终值（避免每次读取都重新计算）
        private float _cachedValue;
        // 脏标记：当基础值或加成变化时置为 true
        private bool _isDirty = true;
        // 加成列表：可以是装备、BUFF、天赋等产生的增量
        private readonly List<float> _modifiers = new List<float>();

        /// <summary>
        /// 基础值：直接写入会触发重新计算。
        /// </summary>
        public float BaseValue
        {
            get => _baseValue;
            set
            {
                if (_baseValue == value) return;
                _baseValue = value;
                // 标记为脏，下一次读取 Value 时再重算
                _isDirty = true;
            }
        }

        /// <summary>
        /// 最终值：读取时按需重算，避免无效计算。
        /// </summary>
        public float Value
        {
            get
            {
                if (_isDirty)
                {
                    // 仅在脏时重算，节省性能
                    Recalculate();
                }
                return _cachedValue;
            }
        }

        /// <summary>
        /// 添加一个加成值。
        /// </summary>
        public void AddModifier(float value)
        {
            _modifiers.Add(value);
            // 数据变更，标记为脏
            _isDirty = true;
        }

        /// <summary>
        /// 移除一个加成值（按值移除一次）。
        /// </summary>
        public void RemoveModifier(float value)
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i] != value) continue;
                _modifiers.RemoveAt(i);
                // 数据变更，标记为脏
                _isDirty = true;
                return;
            }
        }

        /// <summary>
        /// 重新计算最终值：基础值 + 全部加成。
        /// </summary>
        private void Recalculate()
        {
            float sum = _baseValue;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                sum += _modifiers[i];
            }
            _cachedValue = sum;
            _isDirty = false;
        }
    }
}
