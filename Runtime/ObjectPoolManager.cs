using System.Collections.Generic;
using UnityEngine;

namespace Yonii8.Unity.ObjectPooling
{
    public class ObjectPoolManager : MonoBehaviour
    {
        public static ObjectPoolManager Instance;

        [Header("PoolList")]
        public List<ObjectPool> Pools;

        private void Awake()
        {
            if(!Instance)
                Instance = this;
        }

        private void Start()
        {
            Pools.ForEach(p =>
            {
                p.Initialise(gameObject.transform);
            });
        }
    }
}
