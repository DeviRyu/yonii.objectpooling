using System.Collections.Generic;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace Yonii8.ObjectPooling
{
    public class ObjectPoolManager : MonoBehaviour
    {
        [Header("PoolList")]
        [SerializeField] private List<ObjectPool> _pools;
        
        public static ObjectPoolManager Instance;
        public readonly Dictionary<string, ObjectPool> PoolsDic = new();

        private void Awake()
        {
            if(!Instance)
                Instance = this;
        }

        private void Start()
        {
            _pools.ForEach(pool =>
            {
                pool.Initialise(poolManager: gameObject.transform);

                if (!PoolsDic.TryAdd(pool.name, pool))
                {
                    Debug.LogWarning(
                        $"Pool {pool.name} could not be added to dictionary."
                        );
                }
            });
        }
    }
}
