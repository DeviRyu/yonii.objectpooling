using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Yonniie8.Unity.Utilities.Components;

// ReSharper disable InconsistentNaming

namespace Yonii8.ObjectPooling
{
    [CreateAssetMenu(fileName = nameof(ObjectPool), menuName = "Yonii/Object Pooling/Create New Pool")]
    public class ObjectPool : ScriptableObject
    {
        private List<GameObject> _objects;
        private GameObject _parent;
        private bool _initialised;
        
        [SerializeField] private int _initialCount;
        [SerializeField] private bool _nonAsyncInstantiation; 
        
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
                    "Initial count has been found as 0. It will be defaulted to 5." +
                    "Please check that you have properly populated the value.");
                _initialCount = 5;
            }
            
            if(_nonAsyncInstantiation)
                FillPool(_initialCount);
            else
                FillPoolAsync(_initialCount);
            
            Prefab.SetActive(true);
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += EditorApplicationOnplayModeStateChanged;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= EditorApplicationOnplayModeStateChanged;
#endif
        }

        public GameObject GetPooledObject()
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

        public GameObject GetPooledObject(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var obj = GetPooledObject();
            obj.transform.SetPositionAndRotation(position, rotation);

            if(parent)
                obj.transform.SetParent(parent);

            return obj;
        }

        public void Return(GameObject obj)
        {
            obj.transform.SetParent(_parent.transform, worldPositionStays: false);
            obj.SetActive(false);
        }

        private void FillPoolAsync(int initialCount)
        {
            var instantiateAsync = InstantiateAsync(Prefab, initialCount, _parent.transform);
            instantiateAsync.completed += (operation) => InstantiationCompleted(operation, instantiateAsync);
        }

        private void FillPool(int initialCount)
        {
            for (var i = 0; i < initialCount; i++)
            {
                var obj = CreatePooledGameObject();
                if (!obj.TryGetComponent<PooledMonoBehaviour>(out var pooledMonoBehaviour))
                {
                    Debug.LogWarning(
                        $"Could not find PooledMonoBehaviour for pooled object {obj.name} in pool {_parent.name} " +
                        "If that is intended please ignore. " +
                        "Otherwise please check your object."
                    );
                    
                    UpdateNameAndAddToObjects(pooledObject: obj, index: i);
                }

                SetPoolAndUpdateNameOnMono(pooledMonoBehaviour, index: i);
                _objects.Add(obj);
            }
        }

        private void UpdatePooledMonoBehaviours(GameObject[] gameObjects)
        {
            var index = _objects.Count;
            foreach (var pooledObject in gameObjects)
            {
                if (!pooledObject.TryGetComponentsInChildren<PooledMonoBehaviour>(out var pooledMonoBehaviours))
                {
                    Debug.LogWarning(
                        $"Could not find PooledMonoBehaviour for pooled object {pooledObject.name} in pool {_parent.name} " +
                        "If that is intended please ignore. " +
                        "Otherwise please check your object."
                    );

                    UpdateNameAndAddToObjects(pooledObject, index);

                    index++;
                    continue;
                }
                
                foreach (var pooledMonoBehaviour in pooledMonoBehaviours) 
                    SetPoolAndUpdateNameOnMono(pooledMonoBehaviour, index);

                index++;
                _objects.Add(pooledObject);
            }
        }

        private void SetPoolAndUpdateNameOnMono(PooledMonoBehaviour pooledMonoBehaviour, int index)
        {
            pooledMonoBehaviour.SetPool(this);
            pooledMonoBehaviour.UpdateName(index.ToString());
        }

        private void UpdateNameAndAddToObjects(GameObject pooledObject, int index)
        {
            pooledObject.name += $" - {index}";
            _objects.Add(pooledObject);
        }

        private GameObject ExpandPool()
        {
            var newObject = CreatePooledGameObject();
            UpdatePooledMonoBehaviours(new []{newObject});
            newObject.SetActive(true);

            return newObject;
        }
        
        private void Clear() => _objects.Clear();

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
        
#if UNITY_EDITOR
        private void EditorApplicationOnplayModeStateChanged(PlayModeStateChange state)
        {
            if(state != PlayModeStateChange.EnteredEditMode)
                return;

            Clear();
        }
#endif
    }
}