using System;
using System.Collections.Generic;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace Yonii8.Unity.ObjectPooling
{
    
    [CreateAssetMenu(fileName = "Unnamed Pool", menuName = "Yonii/Object Pooling/Create New Pool")]
    public class ObjectPool : ScriptableObject
    {
        private List<GameObject> _objects;
        private GameObject _parent;
        private bool _initialised;
        
        [SerializeField] private int _initialCount;
        
        public GameObject Prefab;
        public bool ExpandablePool;

        public void Initialise(Transform poolManager)
        {            
            _parent = new GameObject(name: $"{Prefab.name}_Pool");
            _parent.transform.SetParent(poolManager, worldPositionStays: false);

            Prefab.SetActive(false);

            if (_initialCount == 0)
            {
                Debug.LogWarning(
                    "Initial count has been found as 0. It will be defaulted to 5, please check that you have properly populated the value.");
                _initialCount = 5;
            }
            _objects = FillPool(_initialCount);
            Prefab.SetActive(true);
        }

        public GameObject GetPooledObject()
        {
            if (!_initialised)
            {
                Debug.LogWarning(
                    "Pool has not initialised yet!" +
                    "Please make sure that whatever objects you are grabbing are done post initialising!"
                    );

                throw new ApplicationException();
            }
            
            foreach (var obj in _objects)
            {
                if(obj.gameObject.activeSelf || obj.gameObject.activeInHierarchy)
                    continue;

                obj.SetActive(true);
                return obj;
            }

            if (!ExpandablePool)
                throw new ApplicationException($"Non-expandable pool is out of pooled objects. This object pool is for prefab - {Prefab.name}");

            var newObject = CreatePooledGameObject();
            _objects.Add(newObject);

            newObject.SetActive(true);
            return newObject;
        }

        public void ReturnToPool(GameObject obj)
        {
            obj.transform.SetParent(_parent.transform, worldPositionStays: false);
            obj.SetActive(false);
        }

        private List<GameObject> FillPool(int initialCount)
        {
            var capacity = Math.Max(4, initialCount);
            var pool = new List<GameObject>(capacity);

            var res = InstantiateAsync(Prefab, initialCount, _parent.transform);
            res.completed += (operation) => InstantiationCompleted(operation, res);

            return pool;
        }

        private void UpdatePooledMonoBehaviours()
        {
            var index = 0;
            Debug.Log($"_objectsCount {_objects.Count}");
            _objects.ForEach(po =>
            {
                if (!po.TryGetComponent<PooledMonoBehaviour>(out var pooledMonoBehaviour))
                {
                    Debug.LogWarning(
                        $"Could not find PooledMonoBehaviour for {po.name}. " +
                        "If that is intended please ignore. " +
                        "Otherwise please check your object."
                    );
                    
                    return;
                }
                
                pooledMonoBehaviour.SetPool(this);
                pooledMonoBehaviour.UpdateName(index.ToString());
                index++;
            });
        }

        private GameObject CreatePooledGameObject()
        {
            var obj = Instantiate(Prefab, _parent.transform);
            return obj;
        }

        private void InstantiationCompleted(AsyncOperation obj, AsyncInstantiateOperation<GameObject> res)
        {
            Debug.Log("Are we hitting this?");
            if (!obj.isDone)
            {
                Debug.LogWarning(
                    $"Completed event has been hit but AsyncOperation is not done! Most likely the pool has no objects inside! Progress - {obj.progress}");
                return;
            }
            
            _objects.AddRange(res.Result);
            UpdatePooledMonoBehaviours();
            _initialised = true;
        }
    }
}