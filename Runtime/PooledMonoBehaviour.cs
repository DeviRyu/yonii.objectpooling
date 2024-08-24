using UnityEngine;

namespace Yonii.ObjectPooling
{
    public class PooledMonoBehaviour : MonoBehaviour
    {
        private ObjectPool _pool;
        
        public void ReturnToPool() => _pool.Return(gameObject);

        public void SetPool(ObjectPool pool) => _pool = pool;
        public void UpdateName(string index) => name += $" {index}";
    }
}