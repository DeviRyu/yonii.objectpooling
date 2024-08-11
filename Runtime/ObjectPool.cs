using System;
using System.Collections.Generic;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace Yonii8.Unity.ObjectPooling
{
    
    [CreateAssetMenu(fileName = nameof(ObjectPool), menuName = "Yonii/Object Pooling/Create New Pool")]
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

            _parent.GetInstanceID();

            Prefab.SetActive(false);

            if (_initialCount == 0)
            {
                Debug.LogWarning(
                    "Initial count has been found as 0. It will be defaulted to 5." +
                    "Please check that you have properly populated the value.");
                _initialCount = 5;
            }
            FillPool(_initialCount);
            Prefab.SetActive(true);
        }
        
        private GameObject GetPooledObject()
        {
            if (!_initialised)
            {
                Debug.LogWarning(
                    "Pool has not initialised yet!" +
                    "Please make sure that whatever objects you are grabbing are done post initialising!" +
                    "Pool will create a new object so that game can continue. (will ignore non-expandable condition)!"
                    );

                return ExpandPool();
            }
            
            foreach (var obj in _objects)
            {
                if(obj.activeSelf || obj.activeInHierarchy)
                    continue;

                obj.SetActive(true);
                return obj;
            }

            if (!ExpandablePool)
                throw new ApplicationException($"Non-expandable pool is out of pooled objects. This object pool is for prefab - {Prefab.name}");

            return ExpandPool();
        }

        public void Return(GameObject obj)
        {
            obj.transform.SetParent(_parent.transform, worldPositionStays: false);
            obj.SetActive(false);
        }

        private void FillPool(int initialCount)
        {
            var instantiateAsync = InstantiateAsync(Prefab, initialCount, _parent.transform);
            instantiateAsync.completed += (operation) => InstantiationCompleted(operation, instantiateAsync);
        }

        private void UpdatePooledMonoBehaviours(GameObject[] gameObjects)
        {
            var index = _objects.Count;
            foreach (var pooledObject in gameObjects)
            {
                if (!pooledObject.TryGetComponent<PooledMonoBehaviour>(out var pooledMonoBehaviour))
                {
                    Debug.LogWarning(
                        $"Could not find PooledMonoBehaviour for pooled object {pooledObject.name} in pool {_parent.name} " +
                        "If that is intended please ignore. " +
                        "Otherwise please check your object."
                    );

                    pooledObject.name += $" - {index}";
                    index++;
                    _objects.Add(pooledObject);

                    continue;
                }
                
                pooledMonoBehaviour.SetPool(this);
                pooledMonoBehaviour.UpdateName(index.ToString());
                index++;
                _objects.Add(pooledObject);
            }
        }

        private GameObject ExpandPool()
        {
            var newObject = CreatePooledGameObject();
            UpdatePooledMonoBehaviours(new []{newObject});
            newObject.SetActive(true);

            return newObject;
        }

        private GameObject CreatePooledGameObject()
        {
            var obj = Instantiate(Prefab, _parent.transform);
            return obj;
        }

        private void InstantiationCompleted(AsyncOperation obj, AsyncInstantiateOperation<GameObject> instantiateAsync)
        {
            if (!obj.isDone)
            {
                Debug.LogWarning(
                    $"Completed event has been hit but AsyncOperation is not done! Most likely the pool has no objects inside! Progress - {obj.progress}");
                return;
            }

            UpdatePooledMonoBehaviours(instantiateAsync.Result);
            _initialised = true;
        }
    }
}