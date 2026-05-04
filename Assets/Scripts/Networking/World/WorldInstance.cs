using System;
using System.Collections.Generic;
using ROC.Game.World;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace ROC.Networking.World
{
    public enum WorldInstanceLifecycleState : byte
    {
        Loading = 0,
        Active = 1,
        Unloading = 2,
        Unloaded = 3
    }

    public sealed class WorldInstance
    {
        public string InstanceId;
        public string SceneId;
        public string UnitySceneName;
        public WorldInstanceKind InstanceKind;

        public string OwnerAccountId;
        public string OwnerCharacterId;

        public bool UnloadWhenEmpty;

        public Scene LoadedScene;
        public WorldInstanceLifecycleState LifecycleState;

        public readonly HashSet<ulong> ClientIds = new();
        public readonly List<NetworkObject> SpawnedSceneObjects = new();
        public readonly List<Action<WorldInstance>> LoadCallbacks = new();

        public WorldInstance(
            string instanceId,
            string sceneId,
            string unitySceneName,
            WorldInstanceKind instanceKind,
            string ownerAccountId,
            string ownerCharacterId,
            bool unloadWhenEmpty)
        {
            InstanceId = instanceId;
            SceneId = sceneId;
            UnitySceneName = unitySceneName;
            InstanceKind = instanceKind;
            OwnerAccountId = ownerAccountId;
            OwnerCharacterId = ownerCharacterId;
            UnloadWhenEmpty = unloadWhenEmpty;
            LifecycleState = WorldInstanceLifecycleState.Loading;
        }
    }
}