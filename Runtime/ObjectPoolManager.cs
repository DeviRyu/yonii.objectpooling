using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ReSharper disable InconsistentNaming

namespace Yonii8.ObjectPooling
{
    public class ObjectPoolManager : MonoBehaviour
    {
        [Header("PoolList")]
        [SerializeField] private List<ObjectPool> _pools;
        private readonly Dictionary<string, ObjectPool> _poolsDic = new();
        
        public static ObjectPoolManager Instance;

        private void Awake()
        {
            if(!Instance)
                Instance = this;
            
            _pools.ForEach(pool =>
            {
                pool.Initialise(poolManager: gameObject.transform);

                if (!_poolsDic.TryAdd(pool.name, pool))
                {
                    Debug.LogWarning(
                        $"Pool {pool.name} could not be added to dictionary."
                    );
                }
            });
        }

        public ObjectPool GetPool(string poolName)
        {
            if (!_poolsDic.TryGetValue(poolName, out var pool))
                Debug.LogWarning(
                    $"Could not find pool with name - {poolName}"
                );

            return pool;
        }

        public List<ObjectPool> GetPools(List<string> poolNames)
        {
            var poolList = new List<ObjectPool>(capacity: poolNames.Count);
            poolNames.ForEach(poolName =>
            {
                var pool = GetPool(poolName);
                if(!pool)
                    return;
                
                poolList.Add(pool);
            });

            return poolList;
        }

        public void ReturnAllObjects(Scene scene) => _pools.ForEach(p => p.ReturnAllObjects(scene));
        public void ReturnAllObjects() => _pools.ForEach(p => p.ReturnAllObjects());
    }
}
