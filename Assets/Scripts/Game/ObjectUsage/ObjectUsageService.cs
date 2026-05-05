using System;
using ROC.Game.Common;
using ROC.Networking.Sessions;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Game.ObjectUsage
{
    /// <summary>
    /// Server-authoritative service for object-scoped interaction usage.
    /// Use this for facts such as "character X has used object Y" or
    /// "object Y has consumed N shared uses." Do not use progress flags for this category.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectUsageService : MonoBehaviour
    {
        public static ObjectUsageService Instance { get; private set; }

        public event Action<string> UsageChanged;

        [Header("Repository")]
        [Tooltip("Assign LocalObjectUsageRepository for now. Later this can be replaced by a database-backed repository.")]
        [SerializeField] private MonoBehaviour repositoryBehaviour;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private IObjectUsageRepository _repository;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ResolveRepository();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool HasCharacterUsedForClient(ulong clientId, string usageKey)
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return false;
            }

            return HasCharacterUsed(characterId, usageKey);
        }

        public bool HasCharacterUsed(string characterId, string usageKey)
        {
            ResolveRepository();
            return _repository != null &&
                   !string.IsNullOrWhiteSpace(characterId) &&
                   ObjectUsageKeyUtility.IsValidKey(usageKey) &&
                   _repository.HasCharacterUse(ObjectUsageKeyUtility.NormalizeKey(usageKey), characterId);
        }

        public int GetCharacterUseCountForClient(ulong clientId, string usageKey)
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return 0;
            }

            return GetCharacterUseCount(characterId, usageKey);
        }

        public int GetCharacterUseCount(string characterId, string usageKey)
        {
            ResolveRepository();
            return _repository != null &&
                   !string.IsNullOrWhiteSpace(characterId) &&
                   ObjectUsageKeyUtility.IsValidKey(usageKey)
                ? _repository.GetCharacterUseCount(ObjectUsageKeyUtility.NormalizeKey(usageKey), characterId)
                : 0;
        }

        public int GetGlobalUseCount(string usageKey)
        {
            ResolveRepository();
            return _repository != null && ObjectUsageKeyUtility.IsValidKey(usageKey)
                ? _repository.GetGlobalUseCount(ObjectUsageKeyUtility.NormalizeKey(usageKey))
                : 0;
        }

        public bool IsGlobalCapacityReached(string usageKey, int globalUseCapacity)
        {
            if (globalUseCapacity <= 0)
            {
                return false;
            }

            return GetGlobalUseCount(usageKey) >= globalUseCapacity;
        }

        public ServerActionResult RecordUseForClient(
            ulong clientId,
            string usageKey,
            string sceneId,
            string instanceId,
            string stableObjectId,
            string cleanupGroup,
            bool recordCharacterUse,
            bool incrementGlobalUse,
            string source)
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot record object usage because the client has no selected character.");
            }

            return RecordUse(
                characterId,
                usageKey,
                sceneId,
                instanceId,
                stableObjectId,
                cleanupGroup,
                recordCharacterUse,
                incrementGlobalUse,
                source);
        }

        public ServerActionResult RecordUse(
            string characterId,
            string usageKey,
            string sceneId,
            string instanceId,
            string stableObjectId,
            string cleanupGroup,
            bool recordCharacterUse,
            bool incrementGlobalUse,
            string source)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            ResolveRepository();
            if (_repository == null)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.PersistenceUnavailable,
                    "No object usage repository is available.");
            }

            string normalizedKey = ObjectUsageKeyUtility.NormalizeKey(usageKey);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidRequest,
                    "Cannot record object usage because the usage key is empty or invalid.");
            }

            bool saved = _repository.RecordUse(
                normalizedKey,
                sceneId ?? string.Empty,
                instanceId ?? string.Empty,
                stableObjectId ?? string.Empty,
                ObjectUsageKeyUtility.NormalizePart(cleanupGroup),
                characterId ?? string.Empty,
                recordCharacterUse,
                incrementGlobalUse,
                source ?? string.Empty);

            if (!saved)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.PersistenceFailed,
                    $"Failed to record object usage: {normalizedKey}");
            }

            PublishChanged(normalizedKey);
            return ServerActionResult.Ok();
        }

        public ServerActionResult ResetCharacterUses(string usageKey)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            ResolveRepository();
            if (_repository == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.PersistenceUnavailable, "No object usage repository is available.");
            }

            string normalizedKey = ObjectUsageKeyUtility.NormalizeKey(usageKey);
            bool reset = _repository.ResetCharacterUses(normalizedKey);
            if (reset)
            {
                PublishChanged(normalizedKey);
            }

            return reset ? ServerActionResult.Ok() : ServerActionResult.Fail(ServerActionErrorCode.InvalidTarget, "Usage key was not found.");
        }

        public ServerActionResult ResetGlobalUses(string usageKey)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            ResolveRepository();
            if (_repository == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.PersistenceUnavailable, "No object usage repository is available.");
            }

            string normalizedKey = ObjectUsageKeyUtility.NormalizeKey(usageKey);
            bool reset = _repository.ResetGlobalUses(normalizedKey);
            if (reset)
            {
                PublishChanged(normalizedKey);
            }

            return reset ? ServerActionResult.Ok() : ServerActionResult.Fail(ServerActionErrorCode.InvalidTarget, "Usage key was not found.");
        }

        public ServerActionResult DeleteUsage(string usageKey)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            ResolveRepository();
            if (_repository == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.PersistenceUnavailable, "No object usage repository is available.");
            }

            string normalizedKey = ObjectUsageKeyUtility.NormalizeKey(usageKey);
            bool deleted = _repository.DeleteUsage(normalizedKey);
            if (deleted)
            {
                PublishChanged(normalizedKey);
            }

            return deleted ? ServerActionResult.Ok() : ServerActionResult.Fail(ServerActionErrorCode.InvalidTarget, "Usage key was not found.");
        }

        public ServerActionResult DeleteUsagesByCleanupGroup(string cleanupGroup)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            ResolveRepository();
            if (_repository == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.PersistenceUnavailable, "No object usage repository is available.");
            }

            string normalizedGroup = ObjectUsageKeyUtility.NormalizePart(cleanupGroup);
            int removed = _repository.DeleteUsagesByCleanupGroup(normalizedGroup);
            PublishChanged($"cleanup:{normalizedGroup}");
            return ServerActionResult.Ok($"Deleted {removed} object usage record(s).");
        }

        public ServerActionResult DeleteUsagesByInstance(string instanceId)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            ResolveRepository();
            if (_repository == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.PersistenceUnavailable, "No object usage repository is available.");
            }

            string normalizedInstanceId = ObjectUsageKeyUtility.NormalizePart(instanceId);
            int removed = _repository.DeleteUsagesByInstance(normalizedInstanceId);
            PublishChanged($"instance:{normalizedInstanceId}");
            return ServerActionResult.Ok($"Deleted {removed} object usage record(s).");
        }

        private void ResolveRepository()
        {
            if (_repository != null)
            {
                return;
            }

            if (repositoryBehaviour is IObjectUsageRepository assignedRepository)
            {
                _repository = assignedRepository;
                return;
            }

            if (LocalObjectUsageRepository.Instance != null)
            {
                _repository = LocalObjectUsageRepository.Instance;
            }
        }

        private bool TryGetCharacterId(ulong clientId, out string characterId)
        {
            characterId = string.Empty;
            PlayerSessionRegistry registry = PlayerSessionRegistry.Instance;
            return registry != null &&
                   registry.TryGetCharacterId(clientId, out characterId) &&
                   !string.IsNullOrWhiteSpace(characterId);
        }

        private static bool RequireServer(out ServerActionResult result)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening && !networkManager.IsServer)
            {
                result = ServerActionResult.Fail(
                    ServerActionErrorCode.PermissionDenied,
                    "Object usage can only be changed by the server.");
                return false;
            }

            result = ServerActionResult.Ok();
            return true;
        }

        private void PublishChanged(string usageKey)
        {
            UsageChanged?.Invoke(usageKey);
            if (verboseLogging)
            {
                Debug.Log($"[ObjectUsageService] Usage changed: {usageKey}");
            }
        }
    }
}
