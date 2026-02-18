using System;
using DG.Tweening;
using UnityEngine;

namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// 平滑转向参数：
    /// 以纯数据结构承载转向配置，便于在任意系统里复用同一套参数。
    /// </summary>
    [Serializable]
    public struct SmoothFacingTweenOptions
    {
        [Tooltip("旋转时长（秒）")]
        public float RotateDuration;

        [Tooltip("旋转缓动曲线")]
        public Ease RotateEase;

        [Tooltip("是否只绕 Y 轴旋转（角色推荐开启）")]
        public bool YAxisOnly;

        [Tooltip("角度差小于该阈值时直接完成，不播放 Tween")]
        public float InstantThresholdAngle;

        /// <summary>
        /// 推荐默认值。
        /// </summary>
        public static SmoothFacingTweenOptions Default => new SmoothFacingTweenOptions
        {
            RotateDuration = 0.2f,
            RotateEase = Ease.OutSine,
            YAxisOnly = true,
            InstantThresholdAngle = 1f
        };
    }

    /// <summary>
    /// 通用平滑转向工具（非 Mono）：
    /// 用静态方法提供“朝向目标”的 DOTween 动画能力，供任意角色/系统复用。
    ///
    /// 设计目标：
    /// 1) 不依赖组件生命周期，避免每个对象都挂一个脚本。
    /// 2) 调用方自行持有 Tween 引用，便于在 OnDisable/状态切换时统一 Kill。
    /// 3) 支持平面转向（Y 轴）与完整三维转向两种模式。
    /// </summary>
    public static class SmoothFacingTween
    {
        /// <summary>
        /// 平滑转向到目标 Transform。
        /// </summary>
        public static bool TryRotateToTarget(
            Transform actor,
            Transform target,
            ref Tween activeTween,
            SmoothFacingTweenOptions options,
            Action onComplete = null)
        {
            if (target == null) return false;
            return TryRotateToWorldPosition(actor, target.position, ref activeTween, options, onComplete);
        }

        /// <summary>
        /// 平滑转向到世界坐标。
        /// </summary>
        public static bool TryRotateToWorldPosition(
            Transform actor,
            Vector3 worldPosition,
            ref Tween activeTween,
            SmoothFacingTweenOptions options,
            Action onComplete = null)
        {
            if (actor == null) return false;

            Vector3 toTarget = worldPosition - actor.position;
            if (options.YAxisOnly)
            {
                toTarget.y = 0f;
            }

            // 若方向向量几乎为零，无法构建有效朝向，返回 false 让调用方自行兜底。
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Quaternion targetRotation = options.YAxisOnly
                ? BuildYAxisOnlyRotation(actor, toTarget)
                : Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            // 角度极小时直接到位，避免“微抖动”。
            float deltaAngle = Quaternion.Angle(actor.rotation, targetRotation);
            if (deltaAngle <= Mathf.Max(0f, options.InstantThresholdAngle))
            {
                Kill(ref activeTween);
                actor.rotation = targetRotation;
                onComplete?.Invoke();
                return true;
            }

            Kill(ref activeTween);
            activeTween = actor
                .DORotateQuaternion(targetRotation, Mathf.Max(0.01f, options.RotateDuration))
                .SetEase(options.RotateEase)
                .OnComplete(() =>
                {
                    // 注意：这里不能访问 ref 参数 activeTween（C# 语法限制）。
                    // 引用清理由调用方在自身回调中维护。
                    onComplete?.Invoke();
                });

            return true;
        }

        /// <summary>
        /// 外部可调用的 Tween 终止入口。
        /// </summary>
        public static void Kill(ref Tween activeTween)
        {
            if (activeTween == null) return;
            activeTween.Kill();
            activeTween = null;
        }

        /// <summary>
        /// 仅构建 Y 轴旋转目标：
        /// 保持当前 X/Z 角不变，只改变角色的水平朝向角（Yaw）。
        /// </summary>
        private static Quaternion BuildYAxisOnlyRotation(Transform actor, Vector3 flatDirection)
        {
            Quaternion look = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            Vector3 currentEuler = actor.eulerAngles;
            Vector3 lookEuler = look.eulerAngles;
            return Quaternion.Euler(currentEuler.x, lookEuler.y, currentEuler.z);
        }
    }
}
