using System;
using System.Collections;
using System.Collections.Generic;
using ROC.Game.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ROC.Networking.World
{
    [DisallowMultipleComponent]
    public sealed class WorldInstanceManager : MonoBehaviour
    {
        public static WorldInstanceManager Instance { get; private set; }

        public event Action<ulong, string> ClientInstanceChanged;

        private readonly Dictionary<string, WorldInstance> _instancesById = new();
        private readonly Dictionary<ulong, string> _instanceIdByClientId = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public void EnsureInstanceLoaded(
            WorldLocation location,
            WorldSceneDefinition sceneDefinition,
            string ownerAccountId,
            string ownerCharacterId,
            Action<WorldInstance> onLoaded)
        {
            string instanceId = string.IsNullOrWhiteSpace(location.InstanceId)
                ? $"{location.SceneId}-default"
                : location.InstanceId;

            if (_instancesById.TryGetValue(instanceId, out WorldInstance existing))
            {
                if (existing.LifecycleState == WorldInstanceLifecycleState.Active)
                {
                    onLoaded?.Invoke(existing);
                    return;
                }

                existing.LoadCallbacks.Add(onLoaded);
                return;
            }

            var instance = new WorldInstance(
                instanceId,
                sceneDefinition.SceneId,
                sceneDefinition.UnitySceneName,
                sceneDefinition.DefaultInstanceKind,
                ownerAccountId,
                ownerCharacterId,
                sceneDefinition.UnloadServerInstanceWhenEmpty);

            instance.LoadCallbacks.Add(onLoaded);
            _instancesById.Add(instanceId, instance);

            Debug.Log($"[WorldInstanceManager] Loading instance {instanceId} from scene {sceneDefinition.UnitySceneName}.");

            StartCoroutine(LoadInstanceCoroutine(instance));
        }

        public void SetClientInstance(ulong clientId, string instanceId)
        {
            RemoveClient(clientId, unloadIfEmpty: true);

            if (!_instancesById.TryGetValue(instanceId, out WorldInstance instance))
            {
                Debug.LogWarning($"[WorldInstanceManager] Cannot assign client {clientId}; unknown instance {instanceId}.");
                return;
            }

            instance.ClientIds.Add(clientId);
            _instanceIdByClientId[clientId] = instanceId;

            ClientInstanceChanged?.Invoke(clientId, instanceId);

            InstanceVisibilityService.Instance?.RefreshClient(clientId);

            Debug.Log($"[WorldInstanceManager] Client {clientId} entered instance {instanceId}.");
        }

        public void RemoveClient(ulong clientId, bool unloadIfEmpty)
        {
            if (!_instanceIdByClientId.TryGetValue(clientId, out string instanceId))
            {
                return;
            }

            _instanceIdByClientId.Remove(clientId);

            if (_instancesById.TryGetValue(instanceId, out WorldInstance instance))
            {
                instance.ClientIds.Remove(clientId);

                if (unloadIfEmpty)
                {
                    TryUnloadIfEmpty(instance);
                }
            }

            ClientInstanceChanged?.Invoke(clientId, string.Empty);
            InstanceVisibilityService.Instance?.RefreshClient(clientId);
        }

        public bool TryGetClientInstanceId(ulong clientId, out string instanceId)
        {
            return _instanceIdByClientId.TryGetValue(clientId, out instanceId);
        }

        public bool TryGetInstance(string instanceId, out WorldInstance instance)
        {
            return _instancesById.TryGetValue(instanceId, out instance);
        }

        public bool AreClientAndInstanceObjectTogether(ulong clientId, string objectInstanceId)
        {
            return _instanceIdByClientId.TryGetValue(clientId, out string clientInstanceId)
                   && clientInstanceId == objectInstanceId;
        }

        public bool AreClientsInSameInstance(ulong clientA, ulong clientB)
        {
            return _instanceIdByClientId.TryGetValue(clientA, out string instanceA)
                   && _instanceIdByClientId.TryGetValue(clientB, out string instanceB)
                   && instanceA == instanceB;
        }

        private IEnumerator LoadInstanceCoroutine(WorldInstance instance)
        {
            HashSet<ulong> loadedBefore = CaptureLoadedSceneHandles();

            AsyncOperation operation = SceneManager.LoadSceneAsync(instance.UnitySceneName, LoadSceneMode.Additive);

            if (operation == null)
            {
                Debug.LogError($"[WorldInstanceManager] Failed to start loading scene {instance.UnitySceneName}.");
                instance.LifecycleState = WorldInstanceLifecycleState.Unloaded;
                InvokeLoadCallbacks(instance);
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }

            Scene loadedScene = FindNewlyLoadedScene(loadedBefore, instance.UnitySceneName);

            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                Debug.LogError($"[WorldInstanceManager] Could not resolve loaded scene for instance {instance.InstanceId}.");
                instance.LifecycleState = WorldInstanceLifecycleState.Unloaded;
                InvokeLoadCallbacks(instance);
                yield break;
            }

            instance.LoadedScene = loadedScene;
            instance.LifecycleState = WorldInstanceLifecycleState.Active;

            SpawnSceneNetworkObjects(instance);

            Debug.Log($"[WorldInstanceManager] Instance active: {instance.InstanceId}");

            InvokeLoadCallbacks(instance);
        }

        private void SpawnSceneNetworkObjects(WorldInstance instance)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                return;
            }

            List<InstanceNetworkObjectSpawnMarker> markers = new();
            GameObject[] roots = instance.LoadedScene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].GetComponentsInChildren(includeInactive: false, markers);
            }

            for (int i = 0; i < markers.Count; i++)
            {
                InstanceNetworkObjectSpawnMarker marker = markers[i];

                if (marker == null || marker.NetworkPrefab == null)
                {
                    continue;
                }

                NetworkObject spawned = Instantiate(
                    marker.NetworkPrefab,
                    marker.transform.position,
                    marker.transform.rotation);

                if (marker.ApplyMarkerScale)
                {
                    spawned.transform.localScale = marker.transform.lossyScale;
                }

                SceneManager.MoveGameObjectToScene(spawned.gameObject, instance.LoadedScene);

                if (spawned.TryGetComponent(out NetworkInstanceObject instanceObject))
                {
                    instanceObject.InitializeServer(instance.InstanceId, marker.StableObjectId);
                }

                spawned.SpawnWithObservers = false;
                spawned.Spawn(destroyWithScene: true);

                instance.SpawnedSceneObjects.Add(spawned);
            }
        }

        private void TryUnloadIfEmpty(WorldInstance instance)
        {
            if (instance.ClientIds.Count > 0)
            {
                return;
            }

            if (!instance.UnloadWhenEmpty)
            {
                return;
            }

            if (instance.LifecycleState != WorldInstanceLifecycleState.Active)
            {
                return;
            }

            StartCoroutine(UnloadInstanceCoroutine(instance));
        }

        private IEnumerator UnloadInstanceCoroutine(WorldInstance instance)
        {
            instance.LifecycleState = WorldInstanceLifecycleState.Unloading;

            for (int i = 0; i < instance.SpawnedSceneObjects.Count; i++)
            {
                NetworkObject netObj = instance.SpawnedSceneObjects[i];

                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(destroy: true);
                }
            }

            instance.SpawnedSceneObjects.Clear();

            if (instance.LoadedScene.IsValid() && instance.LoadedScene.isLoaded)
            {
                Debug.Log($"[WorldInstanceManager] Unloading empty instance {instance.InstanceId}.");

                AsyncOperation unload = SceneManager.UnloadSceneAsync(instance.LoadedScene);

                if (unload != null)
                {
                    while (!unload.isDone)
                    {
                        yield return null;
                    }
                }
            }

            instance.LifecycleState = WorldInstanceLifecycleState.Unloaded;
            _instancesById.Remove(instance.InstanceId);

            Debug.Log($"[WorldInstanceManager] Instance unloaded: {instance.InstanceId}");
        }

        private static HashSet<ulong> CaptureLoadedSceneHandles()
        {
            var handles = new HashSet<ulong>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                handles.Add(scene.handle.GetRawData());
            }

            return handles;
        }

        private static Scene FindNewlyLoadedScene(HashSet<ulong> existingHandles, string unitySceneName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene.name == unitySceneName &&
                    !existingHandles.Contains(scene.handle.GetRawData()))
                {
                    return scene;
                }
            }

            return default;
        }

        private static void InvokeLoadCallbacks(WorldInstance instance)
        {
            for (int i = 0; i < instance.LoadCallbacks.Count; i++)
            {
                instance.LoadCallbacks[i]?.Invoke(instance);
            }

            instance.LoadCallbacks.Clear();
        }
    }
}