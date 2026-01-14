using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// 一个极其轻量级的 GameObject 对象池
    /// </summary>
    public class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parentRoot;
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();

        public GameObjectPool(GameObject prefab, Transform parentRoot, int initialCapacity = 5)
        {
            _prefab = prefab;
            _parentRoot = parentRoot;
            
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
            // 可以在这里重置 transform 等
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