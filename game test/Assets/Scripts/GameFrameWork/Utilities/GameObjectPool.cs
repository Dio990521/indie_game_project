using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// 一个极其轻量级的 GameObject 对象池。
    /// <para>
    /// 设计要点：
    /// - 复用同一对象时 <see cref="Release"/> 会重置 localPosition / localRotation / localScale，
    ///   避免下次 <see cref="Get"/> 取出的对象残留上次使用时的 Transform 状态
    ///   （UI 列表、粒子等场景下"位置错位"Bug 的主要来源）；
    /// - 重置策略可通过构造函数关闭，让特殊场景（如保留世界位置的弹道池）按需绕开。
    /// </para>
    /// </summary>
    public class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parentRoot;
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();

        // 是否在 Release 时自动重置 Transform。
        // 默认开启：绝大多数 UI / 特效场景都希望"复用对象 = 干净的初始状态"。
        // 关闭场景：调用方明确需要保留对象在世界中的位置/旋转/缩放（如保留弹道轨迹）。
        private readonly bool _autoResetTransformOnRelease;

        public GameObjectPool(GameObject prefab, Transform parentRoot, int initialCapacity = 5, bool autoResetTransformOnRelease = true)
        {
            _prefab = prefab;
            _parentRoot = parentRoot;
            _autoResetTransformOnRelease = autoResetTransformOnRelease;

            // 预加载
            for (int i = 0; i < initialCapacity; i++)
            {
                var obj = CreateNew();
                obj.SetActive(false);
                _pool.Push(obj);
            }
        }

        private GameObject CreateNew()
        {
            GameObject obj = Object.Instantiate(_prefab, _parentRoot);
            return obj;
        }

        public GameObject Get()
        {
            GameObject obj;
            if (_pool.Count > 0)
            {
                obj = _pool.Pop();
            }
            else
            {
                obj = CreateNew();
            }

            obj.SetActive(true);
            return obj;
        }

        public void Release(GameObject obj)
        {
            if (obj == null) return;

            obj.SetActive(false);

            // 重置 Transform：避免复用对象时残留上次的 position/rotation/scale。
            // 仅在对象仍挂在池根节点下时安全重置（若调用方把对象移到别的父节点，
            // 重置 localPosition 反而会破坏调用方的布局意图，因此用 parent 检查兜底）。
            if (_autoResetTransformOnRelease && obj.transform.parent == _parentRoot)
            {
                Transform t = obj.transform;
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
            }

            _pool.Push(obj);
        }

        // 扩展：如果你想销毁所有对象清理内存
        public void Clear()
        {
            while(_pool.Count > 0)
            {
                var obj = _pool.Pop();
                if(obj) Object.Destroy(obj);
            }
        }
    }
}