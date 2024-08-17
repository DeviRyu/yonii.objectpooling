using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Yonniie8.Unity.Utilities.Components;

// ReSharper disable InconsistentNaming

namespace Yonii8.ObjectPooling
{
    [CreateAssetMenu(fileName = nameof(ObjectPool), menuName = "Yonii/Object Pooling/Create New Pool")]
    public class ObjectPool : ScriptableObject
    {
        // TODO: Investigate. On a save/reload system this might have the wrong IsTakenOut values due to being a ScriptableObject
        private List<PooledObjectData> _objects;
        private GameObject _parent;
        private bool _initialised;
        
        [SerializeField] private int _initialCount;
        [SerializeField] private bool _nonAsyncInstantiation;
        
        public GameObject Prefab;
        public bool ExpandablePool;

        #region Events

        public UnityEvent<bool> HasFilledChanged;

        #endregion

        #region Public

        public void Initialise(Transform poolManager)
        {            
            _parent = new GameObject(name: $"{Prefab.name}_Pool");
            _parent.transform.SetParent(poolManager, worldPositionStays: false);
            _objects = new List<PooledObjectData>();

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
        
        public GameObject GetPooledObject(bool shouldActivateObject = true, bool shouldDeactivatePrefabIfExpanding = false)
        {
            if (!_initialised)
            {
                Debug.LogWarning(
                    "Pool has not initialised yet!" +
                    "Please make sure that whatever objects you are grabbing are done post initialising!" +
                    "Pool will create a new object so that game can continue. (will ignore non-expandable condition)!"
                );

                return ExpandPool(shouldActivateObject, shouldDeactivatePrefabIfExpanding);
            }
            
            foreach (var data in _objects)
            {
                if(data.isTakenOut)
                    continue;
                
                if(data.GameObject.activeSelf || data.GameObject.activeInHierarchy)
                    continue;

                data.GameObject.SetActive(shouldActivateObject);
                data.isTakenOut = true;
                
                return data.GameObject;
            }

            if (!ExpandablePool)
                throw new ApplicationException($"Non-expandable pool is out of pooled objects. This object pool is for prefab - {Prefab.name}");

            return ExpandPool(shouldActivateObject, shouldDeactivatePrefabIfExpanding);
        }

        public GameObject GetPooledObject(
            Vector3 position,
            Quaternion rotation, 
            bool shouldActivateObject = true, 
            bool shouldDeactivatePrefabIfExpanding = false,
            Transform parent = null, 
            bool worldPositionStays = true
        )
        {
            var obj = GetPooledObject(shouldActivateObject, shouldDeactivatePrefabIfExpanding);
            obj.transform.SetPositionAndRotation(position, rotation);

            if(parent)
                obj.transform.SetParent(parent, worldPositionStays);

            return obj;
        }

        /// <summary>
        /// Returning a game object to its pool.
        /// Will set the parent of the object to the pool with a worldPositionStays with false and then will deactivate the object.
        /// It will also reset the position and rotation to 0.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="worldPositionStays"></param>
        /// <param name="resetPositionAndRotation"></param>
        /// <param name="resetLocalPositionAndRotation"></param>
        public void Return(
            GameObject obj, 
            bool worldPositionStays = false, 
            bool resetPositionAndRotation = true,
            bool resetLocalPositionAndRotation = true
        )
        {
            var data = _objects.FirstOrDefault(d => d.GameObject == obj);
            if (data == null)
            {
                Debug.LogWarning(
                    $"Return - Could not find data for GameObject {obj.name}" +
                    "This object doesn't exist in the pool anymore." +
                    "Please make sure it has not been removed by mistake!"
                );
                
                return;
            }

            obj.transform.SetParent(_parent.transform, worldPositionStays: worldPositionStays);
            obj.SetActive(false);
            data.isTakenOut = false;

            if (!resetPositionAndRotation) 
                return;

            if(resetLocalPositionAndRotation)
                obj.transform.SetLocalPositionAndRotation(localPosition: Vector3.zero, localRotation: Quaternion.identity);
            else
                obj.transform.SetPositionAndRotation(position: Vector3.zero, rotation: Quaternion.identity);
        }

        /// <summary>
        /// Return all objects from a specific scene
        /// </summary>
        /// <param name="scene"></param>
        public void ReturnAllObjects(Scene scene)
        {
            _objects.ForEach(data =>
            {
                if(data.Scene == scene)
                    Return(data.GameObject);
            });
        }

        public void ReturnAllObjects() => _objects.ForEach(data => Return(data.GameObject));

        /// <summary>
        /// If your pooled object(s) have pooled objects from other pools as children
        /// this method can be used to return those objects back to their respective pools
        /// without having to ReFill your pool by instantiating new objects.
        /// WARNING - Child pooled object(s) need to have a PooledMonoBehaviour to be returned! 
        /// </summary>
        public void CleanPooledObjects()
        {
            _objects.ForEach(o =>
            {
                if(!o.GameObject.TryGetComponentsInChildren<PooledMonoBehaviour>(out var pooledMonoBehaviours))
                {
                    Debug.LogWarning(
                        "Attempting to return pooled objects that do not belong to this pool failed." +
                        $"No PooledMonoBehaviours have been found in this GameObject {o.GameObject.name}."
                    );
                    
                    return;
                }

                foreach (var pooledMonoBehaviour in pooledMonoBehaviours) 
                    pooledMonoBehaviour.ReturnToPool();
            });
        }
        
        public void Clear() => _objects.Clear();

        #endregion

        #region Private

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
                UpdatePooledMonoBehaviours(new []{obj});
            }

            _initialised = true;
            HasFilledChanged.Invoke(true);
        }

        private void UpdatePooledMonoBehaviours(GameObject[] gameObjects)
        {
            if (_objects == null)
            {
                Debug.LogError(
                    "UpdatePooledMonoBehaviours - _objects list has been found null. This list should always be instantiated!" +
                    "This code monkey has done something wrong!:("
                );
            }
            
            var index = _objects?.Count ?? 0;
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
                CreateNewPooledObjectDataAndAddToObjects(pooledObject);
            }
        }

        private void CreateNewPooledObjectDataAndAddToObjects(GameObject gameObject)
        {
            var pooledObjectData = new PooledObjectData
            {
                GameObject = gameObject,
                isTakenOut = false,
            };
            
            _objects.Add(pooledObjectData);
        }

        private void SetPoolAndUpdateNameOnMono(PooledMonoBehaviour pooledMonoBehaviour, int index)
        {
            pooledMonoBehaviour.SetPool(this);
            pooledMonoBehaviour.UpdateName(index.ToString());
        }

        private void UpdateNameAndAddToObjects(GameObject pooledObject, int index)
        {
            pooledObject.name += $" - {index}";
            CreateNewPooledObjectDataAndAddToObjects(pooledObject);
        }

        private GameObject ExpandPool(bool shouldActivateObject, bool shouldDeactivatePrefabIfExpanding)
        {
            if (shouldDeactivatePrefabIfExpanding) 
                Prefab.SetActive(false);
            
            var newObject = CreatePooledGameObject();
            UpdatePooledMonoBehaviours(new []{newObject});
            newObject.SetActive(shouldActivateObject);
            
            if(shouldDeactivatePrefabIfExpanding)
                Prefab.SetActive(true);

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
            HasFilledChanged.Invoke(true);
        }

        #endregion

        #region UnityEvents

        private void OnEnable()
        {
            HasFilledChanged ??= new UnityEvent<bool>();

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
        
#if UNITY_EDITOR
        private void EditorApplicationOnplayModeStateChanged(PlayModeStateChange state)
        {
            if(state != PlayModeStateChange.EnteredEditMode)
                return;

            Clear();
        }
#endif

        #endregion

    }

    internal class PooledObjectData
    {
        public GameObject GameObject { get; set; }
        public bool isTakenOut { get; set; }
        public Scene Scene => GameObject.scene;
    }
}