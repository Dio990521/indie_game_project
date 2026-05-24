using System;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using UnityEngine;

namespace IndieGame.Gameplay.Date
{
    /// <summary>
    /// 日期系统：
    /// 负责追踪游戏内日期并在每次 Sleep/Inn 后推进一天。
    /// 日历规则：每月固定 30 天，全年 12 个月，起始日期 第1年1月1日。
    /// 继承 SaveableMonoSingleton 以自动接入存档系统。
    /// </summary>
    public class DateSystem : SaveableMonoSingleton<DateSystem>
    {
        // 当前年份（从 1 开始）
        private int _year;
        // 当前月份（1-12）
        private int _month;
        // 当前日（1-30）
        private int _day;

        // 初始化保护标记，避免 CaptureState 在初始化前被调用时返回错误值
        private bool _isInitialized;

        /// <summary> 存档模块唯一 ID。 </summary>
        public override string SaveID => "DateSystem";

        protected override void Awake()
        {
            base.Awake();
            EnsureInitialized();
        }

        /// <summary>
        /// 推进一天：
        /// 日 +1，满 30 进月，满 12 月进年。
        /// 推进完成后广播 DateChangedEvent。
        /// </summary>
        public void AdvanceDay()
        {
            EnsureInitialized();

            _day++;
            if (_day > 30)
            {
                _day = 1;
                _month++;
            }
            if (_month > 12)
            {
                _month = 1;
                _year++;
            }

            RaiseDateChanged();
        }

        /// <summary>
        /// 返回当前格式化日期字符串，如 "第1年1月1日"。
        /// </summary>
        public string GetFormattedDate()
        {
            EnsureInitialized();
            return FormatDate(_year, _month, _day);
        }

        // ──────────────────────────────────────────────
        // ISaveable 实现
        // ──────────────────────────────────────────────

        public override object CaptureState()
        {
            EnsureInitialized();
            return new DateSaveState
            {
                Year  = _year,
                Month = _month,
                Day   = _day
            };
        }

        public override void RestoreState(object data)
        {
            EnsureInitialized();

            if (!(data is DateSaveState state)) return;

            // 防守性校验：保证恢复的值在合法范围内
            _year  = Mathf.Max(1, state.Year);
            _month = Mathf.Clamp(state.Month, 1, 12);
            _day   = Mathf.Clamp(state.Day,   1, 30);

            // 读档后通知 UI 刷新
            RaiseDateChanged();
        }

        // ──────────────────────────────────────────────
        // 私有辅助
        // ──────────────────────────────────────────────

        /// <summary>
        /// 确保已完成初始化（幂等）。
        /// </summary>
        private void EnsureInitialized()
        {
            if (_isInitialized) return;

            _year  = 1;
            _month = 1;
            _day   = 1;
            _isInitialized = true;
        }

        /// <summary>
        /// 广播 DateChangedEvent。
        /// </summary>
        private void RaiseDateChanged()
        {
            EventBus.Raise(new DateChangedEvent
            {
                Year          = _year,
                Month         = _month,
                Day           = _day,
                FormattedDate = FormatDate(_year, _month, _day)
            });
        }

        /// <summary>
        /// 将年/月/日格式化为"第X年X月X日"字符串。
        /// </summary>
        private static string FormatDate(int year, int month, int day)
        {
            return $"第{year}年{month}月{day}日";
        }

        // ──────────────────────────────────────────────
        // 存档数据结构
        // ──────────────────────────────────────────────

        [Serializable]
        private class DateSaveState
        {
            public int Year;
            public int Month;
            public int Day;
        }
    }
}
