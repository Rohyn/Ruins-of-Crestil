using System.Collections.Generic;
using ROC.Game.World;
using ROC.Game.Common;
using ROC.Networking.Characters;
using ROC.Networking.World;
using ROC.Networking.Sessions;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ROC.Game.Sessions
{
    [DisallowMultipleComponent]
    public sealed class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private NetworkPlayerAvatar playerAvatarPrefab;
        [SerializeField] private WorldSceneCatalog sceneCatalog;
        [SerializeField] private LocalCharacterRepository characterRepository;
        [SerializeField] private CharacterRoutingService routingService;
        [SerializeField] private WorldInstanceManager instanceManager;
        [SerializeField] private PlayerSessionRegistry sessionRegistry;
        [SerializeField] private ServerRepositoryProvider repositoryProvider;

        private IPlayerLocationRepository _locationRepository;
        private readonly Dictionary<ulong, PendingSpawn> _pendingSpawns = new();
        private readonly Dictionary<ulong, ActivePlayerSession> _activeSessions = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            ResolveReferences();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void EnterGame(ulong clientId, PersistentCharacterRecord character)
        {
            ResolveReferences();

            if (!CanRunServerFlow(out string error))
            {
                Debug.LogWarning($"[GameSessionManager] EnterGame failed: {error}");
                return;
            }

            if (!routingService.TryResolveEntryLocation(character, clientId, out WorldLocation location, out error))
            {
                Debug.LogWarning($"[GameSessionManager] Route failed: {error}");
                return;
            }

            BeginMoveClientToLocation(clientId, character, location);
        }

        public void CompleteIntroAndEnterSharedWorld(ulong clientId, string characterId)
        {
            ResolveReferences();

            if (!CanRunServerFlow(out string error))
            {
                Debug.LogWarning($"[GameSessionManager] CompleteIntro failed: {error}");
                return;
            }

            string accountId = LocalAccountStub.GetAccountId(clientId);

            if (!characterRepository.TryGetCharacter(accountId, characterId, out PersistentCharacterRecord character))
            {
                Debug.LogWarning($"[GameSessionManager] Character not found: {characterId}");
                return;
            }

            if (!routingService.TryResolveIntroCompletionLocation(character, out WorldLocation sharedLocation, out error))
            {
                Debug.LogWarning($"[GameSessionManager] Intro completion route failed: {error}");
                return;
            }

            if (!characterRepository.MarkIntroComplete(accountId, characterId, sharedLocation))
            {
                Debug.LogWarning($"[GameSessionManager] Failed to mark intro complete for {characterId}.");
                return;
            }

            if (!characterRepository.TryGetCharacter(accountId, characterId, out PersistentCharacterRecord updatedCharacter))
            {
                Debug.LogWarning($"[GameSessionManager] Failed to reload character after intro completion.");
                return;
            }

            BeginMoveClientToLocation(clientId, updatedCharacter, updatedCharacter.CurrentLocation);
        }

        public bool TryGetClientAvatarObject(ulong clientId, out NetworkObject avatarObject)
        {
            if (_activeSessions.TryGetValue(clientId, out ActivePlayerSession session) &&
                session.AvatarObject != null &&
                session.AvatarObject.IsSpawned)
            {
                avatarObject = session.AvatarObject;
                return true;
            }

            avatarObject = null;
            return false;
        }

        public bool TryGetClientInstanceId(ulong clientId, out string instanceId)
        {
            instanceId = string.Empty;

            return instanceManager != null &&
                   instanceManager.TryGetClientInstanceId(clientId, out instanceId);
        }

        private void BeginMoveClientToLocation(
            ulong clientId,
            PersistentCharacterRecord character,
            WorldLocation location)
        {
            SaveActiveClientLocation(clientId);
            DespawnActiveAvatar(clientId);

            if (!sceneCatalog.TryGetScene(location.SceneId, out WorldSceneDefinition sceneDefinition))
            {
                Debug.LogWarning($"[GameSessionManager] Unknown SceneId: {location.SceneId}");
                return;
            }

            if (string.IsNullOrWhiteSpace(location.InstanceId))
            {
                location.InstanceId = sceneDefinition.DefaultInstanceId;
            }

            ServerActionResult loadingResult =
                sessionRegistry != null
                    ? sessionRegistry.SetLoadingWorld(clientId, location)
                    : ServerActionResult.Ok();

            if (!loadingResult.Success)
            {
                Debug.LogWarning($"[GameSessionManager] Cannot move client to world: {loadingResult}");
                return;
            }

            _pendingSpawns[clientId] = new PendingSpawn
            {
                AccountId = character.AccountId,
                CharacterId = character.CharacterId,
                DisplayName = character.DisplayName,
                Location = location
            };

            instanceManager.EnsureInstanceLoaded(
                location,
                sceneDefinition,
                character.AccountId,
                character.CharacterId,
                loadedInstance => RequestClientStream(clientId, sceneDefinition, loadedInstance));
        }

        private void RequestClientStream(
            ulong clientId,
            WorldSceneDefinition sceneDefinition,
            WorldInstance loadedInstance)
        {
            if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
            {
                Debug.LogWarning($"[GameSessionManager] Client {clientId} disconnected before stream request.");
                return;
            }

            if (client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent(out ClientWorldSceneStreamer streamer))
            {
                Debug.LogWarning($"[GameSessionManager] Client {clientId} missing ClientWorldSceneStreamer on session proxy.");
                return;
            }

            streamer.StreamClientToWorldScene(
                clientId,
                sceneDefinition.SceneId,
                sceneDefinition.UnitySceneName,
                loadedInstance.InstanceId);
        }

        private void HandleClientWorldSceneReady(ulong clientId, string sceneId, string instanceId)
        {
            if (!_pendingSpawns.TryGetValue(clientId, out PendingSpawn pending))
            {
                return;
            }

            if (pending.Location.SceneId != sceneId || pending.Location.InstanceId != instanceId)
            {
                Debug.LogWarning(
                    $"[GameSessionManager] Ignoring scene ready mismatch for client {clientId}. " +
                    $"Expected {pending.Location.SceneId}/{pending.Location.InstanceId}, got {sceneId}/{instanceId}.");

                return;
            }

            instanceManager.SetClientInstance(clientId, instanceId);

            SpawnAvatarForClient(clientId, pending);
            _pendingSpawns.Remove(clientId);
        }

        private void SpawnAvatarForClient(ulong clientId, PendingSpawn pending)
        {
            if (!instanceManager.TryGetInstance(pending.Location.InstanceId, out WorldInstance instance))
            {
                Debug.LogWarning($"[GameSessionManager] Missing instance {pending.Location.InstanceId}.");
                return;
            }

            ResolveSpawnTransform(
                instance,
                pending.Location,
                out Vector3 spawnPosition,
                out Quaternion spawnRotation);

            NetworkPlayerAvatar avatar = Instantiate(playerAvatarPrefab, spawnPosition, spawnRotation);

            SceneManager.MoveGameObjectToScene(avatar.gameObject, instance.LoadedScene);

            if (avatar.TryGetComponent(out NetworkInstanceObject instanceObject))
            {
                instanceObject.InitializeServer(instance.InstanceId, pending.CharacterId);
            }

            avatar.InitializeServer(
                pending.AccountId,
                pending.CharacterId,
                pending.DisplayName,
                pending.Location);

            NetworkObject networkObject = avatar.GetComponent<NetworkObject>();
            networkObject.SpawnWithOwnership(clientId, destroyWithScene: true);

            _activeSessions[clientId] = new ActivePlayerSession
            {
                AccountId = pending.AccountId,
                CharacterId = pending.CharacterId,
                Location = pending.Location,
                AvatarObject = networkObject,
                LocationTracker = avatar.GetComponent<ServerPlayerLocationTracker>()
            };

            ServerActionResult inWorldResult =
                sessionRegistry != null
                    ? sessionRegistry.SetInWorld(clientId, pending.Location, networkObject)
                    : ServerActionResult.Ok();

            if (!inWorldResult.Success)
            {
                Debug.LogWarning($"[GameSessionManager] Failed to update session registry: {inWorldResult}");
            }

            InstanceVisibilityService.Instance?.RefreshAll();

            Debug.Log(
                $"[GameSessionManager] Spawned {pending.DisplayName} for client {clientId}. " +
                $"SceneId={pending.Location.SceneId}, InstanceId={pending.Location.InstanceId}");
        }

        private void ResolveSpawnTransform(
            WorldInstance instance,
            WorldLocation location,
            out Vector3 position,
            out Quaternion rotation)
        {
            if (location.UseExplicitTransform)
            {
                position = location.Position;
                rotation = location.Rotation;
                return;
            }

            if (NetworkSpawnPoint.TryFindInScene(instance.LoadedScene, location, sceneCatalog, out NetworkSpawnPoint spawnPoint))
            {
                position = spawnPoint.transform.position;
                rotation = spawnPoint.transform.rotation;
                return;
            }

            position = location.Position;
            rotation = location.Rotation == default ? Quaternion.identity : location.Rotation;

            Debug.LogWarning(
                $"[GameSessionManager] No spawn point found for SceneId={location.SceneId}, " +
                $"SpawnPointId={location.SpawnPointId}. Using stored transform fallback.");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            SaveActiveClientLocation(clientId);
            DespawnActiveAvatar(clientId);

            _pendingSpawns.Remove(clientId);
            _activeSessions.Remove(clientId);

            instanceManager?.RemoveClient(clientId, unloadIfEmpty: true);
            sessionRegistry?.RemoveClient(clientId);
        }

        private void SaveActiveClientLocation(ulong clientId)
        {
            if (!_activeSessions.TryGetValue(clientId, out ActivePlayerSession session))
            {
                return;
            }

            if (session.LocationTracker == null)
            {
                return;
            }

            WorldLocation savedLocation = session.LocationTracker.CaptureLocation(session.Location);

            IPlayerLocationRepository locationWriter = _locationRepository ?? characterRepository;

            bool saved = locationWriter != null &&
                         locationWriter.UpdateCharacterLocation(
                             session.AccountId,
                             session.CharacterId,
                             savedLocation);

            if (saved)
            {
                Debug.Log($"[GameSessionManager] Saved location for {session.CharacterId}.");
            }
        }

        private void DespawnActiveAvatar(ulong clientId)
        {
            if (!_activeSessions.TryGetValue(clientId, out ActivePlayerSession session))
            {
                return;
            }

            if (session.AvatarObject != null && session.AvatarObject.IsSpawned)
            {
                session.AvatarObject.Despawn(destroy: true);
            }

            sessionRegistry?.ClearAvatar(clientId);

            _activeSessions.Remove(clientId);
        }

        private void ResolveReferences()
        {
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            if (characterRepository == null)
            {
                characterRepository = LocalCharacterRepository.Instance;
            }

            if (routingService == null)
            {
                routingService = CharacterRoutingService.Instance;
            }

            if (instanceManager == null)
            {
                instanceManager = WorldInstanceManager.Instance;
            }

            if (sceneCatalog == null && routingService != null)
            {
                sceneCatalog = routingService.SceneCatalog;
            }

            if (sessionRegistry == null)
            {
                sessionRegistry = PlayerSessionRegistry.Instance;
            }

            if (repositoryProvider == null)
            {
                repositoryProvider = ServerRepositoryProvider.Instance;
            }

            if (_locationRepository == null)
            {
                if (repositoryProvider != null)
                {
                    _locationRepository = repositoryProvider.PlayerLocationRepository;
                }

                if (_locationRepository == null && characterRepository != null)
                {
                    _locationRepository = characterRepository;
                }
            }
        }

        private void SubscribeEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            }

            ClientWorldSceneStreamer.ServerClientWorldSceneReady -= HandleClientWorldSceneReady;
            ClientWorldSceneStreamer.ServerClientWorldSceneReady += HandleClientWorldSceneReady;
        }

        private void UnsubscribeEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            ClientWorldSceneStreamer.ServerClientWorldSceneReady -= HandleClientWorldSceneReady;
        }

        private bool CanRunServerFlow(out string error)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                error = "Server flow called outside server context.";
                return false;
            }

            if (playerAvatarPrefab == null)
            {
                error = "Missing player avatar prefab.";
                return false;
            }

            if (sceneCatalog == null)
            {
                error = "Missing world scene catalog.";
                return false;
            }

            if (characterRepository == null)
            {
                error = "Missing character repository.";
                return false;
            }

            if (routingService == null)
            {
                error = "Missing character routing service.";
                return false;
            }

            if (instanceManager == null)
            {
                error = "Missing world instance manager.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private struct PendingSpawn
        {
            public string AccountId;
            public string CharacterId;
            public string DisplayName;
            public WorldLocation Location;
        }

        private sealed class ActivePlayerSession
        {
            public string AccountId;
            public string CharacterId;
            public WorldLocation Location;
            public NetworkObject AvatarObject;
            public ServerPlayerLocationTracker LocationTracker;
        }
    }
}