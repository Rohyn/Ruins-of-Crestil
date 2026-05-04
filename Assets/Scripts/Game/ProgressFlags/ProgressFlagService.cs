using System;
using ROC.Game.Common;
using ROC.Networking.ProgressFlags;
using ROC.Networking.Sessions;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Game.ProgressFlags
{
    [DisallowMultipleComponent]
    public sealed class ProgressFlagService : MonoBehaviour
    {
        public static ProgressFlagService Instance { get; private set; }

        public event Action<ProgressFlagChange> FlagChanged;

        [Header("Repository")]
        [Tooltip("Assign LocalProgressFlagRepository for now. Later this can be replaced by a database-backed repository.")]
        [SerializeField] private MonoBehaviour repositoryBehaviour;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private IProgressFlagRepository _repository;

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

        public bool HasFlagForClient(ulong clientId, string flagId)
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return false;
            }

            return HasFlag(characterId, flagId);
        }

        public bool HasFlag(string characterId, string flagId)
        {
            ResolveRepository();

            return _repository != null &&
                   ProgressFlagIdUtility.IsValidFlagId(flagId) &&
                   _repository.HasFlag(characterId, ProgressFlagIdUtility.NormalizeFlagId(flagId));
        }

        public ServerActionResult SetFlagForClient(
            ulong clientId,
            string flagId,
            ProgressFlagLifetime lifetime,
            string source = "")
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot set progress flag because the client has no selected character.");
            }

            return SetFlag(clientId, characterId, flagId, lifetime, source);
        }

        public ServerActionResult SetFlag(
            string characterId,
            string flagId,
            ProgressFlagLifetime lifetime,
            string source = "")
        {
            return SetFlag(ulong.MaxValue, characterId, flagId, lifetime, source);
        }

        public ServerActionResult RemoveFlagForClient(ulong clientId, string flagId)
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot remove progress flag because the client has no selected character.");
            }

            return RemoveFlag(clientId, characterId, flagId);
        }

        public ServerActionResult RemoveFlag(string characterId, string flagId)
        {
            return RemoveFlag(ulong.MaxValue, characterId, flagId);
        }

        public ServerActionResult ClearFlagsWithPrefixForClient(ulong clientId, string prefix)
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot clear progress flags because the client has no selected character.");
            }

            return ClearFlagsWithPrefix(clientId, characterId, prefix);
        }

        public ServerActionResult ClearFlagsWithPrefix(string characterId, string prefix)
        {
            return ClearFlagsWithPrefix(ulong.MaxValue, characterId, prefix);
        }

        public ServerActionResult EvaluateRequirementsForClient(
            ulong clientId,
            ProgressFlagRequirement[] requirements)
        {
            if (requirements == null || requirements.Length == 0)
            {
                return ServerActionResult.Ok();
            }

            if (!TryGetCharacterId(clientId, out _))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot evaluate progress flags because the client has no selected character.");
            }

            for (int i = 0; i < requirements.Length; i++)
            {
                ProgressFlagRequirement requirement = requirements[i];

                if (!ProgressFlagIdUtility.IsValidFlagId(requirement.FlagId))
                {
                    return ServerActionResult.Fail(
                        ServerActionErrorCode.InvalidRequest,
                        $"Invalid progress flag requirement: {requirement.FlagId}");
                }

                bool hasFlag = HasFlagForClient(clientId, requirement.FlagId);

                switch (requirement.RequirementKind)
                {
                    case ProgressFlagRequirementKind.MustHaveFlag:
                        if (!hasFlag)
                        {
                            return ServerActionResult.Fail(
                                ServerActionErrorCode.InvalidState,
                                string.IsNullOrWhiteSpace(requirement.FailureMessage)
                                    ? $"Missing required flag: {requirement.FlagId}"
                                    : requirement.FailureMessage);
                        }

                        break;

                    case ProgressFlagRequirementKind.MustNotHaveFlag:
                        if (hasFlag)
                        {
                            return ServerActionResult.Fail(
                                ServerActionErrorCode.InvalidState,
                                string.IsNullOrWhiteSpace(requirement.FailureMessage)
                                    ? $"Forbidden flag present: {requirement.FlagId}"
                                    : requirement.FailureMessage);
                        }

                        break;
                }
            }

            return ServerActionResult.Ok();
        }

        public ServerActionResult ApplyMutationsForClient(
            ulong clientId,
            ProgressFlagMutation[] mutations)
        {
            if (mutations == null || mutations.Length == 0)
            {
                return ServerActionResult.Ok();
            }

            ServerActionResult lastResult = ServerActionResult.Ok();

            for (int i = 0; i < mutations.Length; i++)
            {
                ProgressFlagMutation mutation = mutations[i];

                switch (mutation.MutationKind)
                {
                    case ProgressFlagMutationKind.SetFlag:
                        lastResult = SetFlagForClient(
                            clientId,
                            mutation.FlagIdOrPrefix,
                            mutation.Lifetime,
                            mutation.Source);
                        break;

                    case ProgressFlagMutationKind.RemoveFlag:
                        lastResult = RemoveFlagForClient(
                            clientId,
                            mutation.FlagIdOrPrefix);
                        break;

                    case ProgressFlagMutationKind.ClearPrefix:
                        lastResult = ClearFlagsWithPrefixForClient(
                            clientId,
                            mutation.FlagIdOrPrefix);
                        break;

                    default:
                        lastResult = ServerActionResult.Fail(
                            ServerActionErrorCode.InvalidRequest,
                            $"Unsupported progress flag mutation: {mutation.MutationKind}");
                        break;
                }

                if (!lastResult.Success)
                {
                    return lastResult;
                }
            }

            return lastResult;
        }

        private ServerActionResult SetFlag(
            ulong clientId,
            string characterId,
            string flagId,
            ProgressFlagLifetime lifetime,
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
                    "No progress flag repository is available.");
            }

            if (!ProgressFlagIdUtility.IsValidFlagId(flagId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidRequest,
                    $"Invalid progress flag ID: {flagId}");
            }

            string normalizedFlagId = ProgressFlagIdUtility.NormalizeFlagId(flagId);

            bool saved = _repository.SetFlag(
                characterId,
                normalizedFlagId,
                lifetime,
                source);

            if (!saved)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.PersistenceFailed,
                    $"Failed to set progress flag: {normalizedFlagId}");
            }

            var change = new ProgressFlagChange(
                clientId,
                characterId,
                normalizedFlagId,
                ProgressFlagChangeKind.Set,
                lifetime,
                source);

            PublishChange(change);
            NotifyClientFlagSet(clientId, normalizedFlagId, lifetime);

            return ServerActionResult.Ok();
        }

        private ServerActionResult RemoveFlag(
            ulong clientId,
            string characterId,
            string flagId)
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
                    "No progress flag repository is available.");
            }

            if (!ProgressFlagIdUtility.IsValidFlagId(flagId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidRequest,
                    $"Invalid progress flag ID: {flagId}");
            }

            string normalizedFlagId = ProgressFlagIdUtility.NormalizeFlagId(flagId);
            _repository.RemoveFlag(characterId, normalizedFlagId);

            var change = new ProgressFlagChange(
                clientId,
                characterId,
                normalizedFlagId,
                ProgressFlagChangeKind.Removed,
                ProgressFlagLifetime.TemporaryContext,
                string.Empty);

            PublishChange(change);
            NotifyClientFlagRemoved(clientId, normalizedFlagId);

            return ServerActionResult.Ok();
        }

        private ServerActionResult ClearFlagsWithPrefix(
            ulong clientId,
            string characterId,
            string prefix)
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
                    "No progress flag repository is available.");
            }

            if (!ProgressFlagIdUtility.IsValidPrefix(prefix))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidRequest,
                    $"Invalid progress flag prefix: {prefix}");
            }

            string normalizedPrefix = ProgressFlagIdUtility.NormalizePrefix(prefix);
            int removed = _repository.ClearFlagsWithPrefix(characterId, normalizedPrefix);

            var change = new ProgressFlagChange(
                clientId,
                characterId,
                normalizedPrefix,
                ProgressFlagChangeKind.ClearedByPrefix,
                ProgressFlagLifetime.TemporaryContext,
                string.Empty);

            PublishChange(change);
            NotifyClientPrefixCleared(clientId, normalizedPrefix);

            return ServerActionResult.Ok($"Cleared {removed} flag(s) with prefix {normalizedPrefix}.");
        }

        private bool TryGetCharacterId(ulong clientId, out string characterId)
        {
            characterId = string.Empty;

            PlayerSessionRegistry registry = PlayerSessionRegistry.Instance;

            return registry != null &&
                   registry.TryGetCharacterId(clientId, out characterId) &&
                   !string.IsNullOrWhiteSpace(characterId);
        }

        private void ResolveRepository()
        {
            if (_repository != null)
            {
                return;
            }

            if (repositoryBehaviour is IProgressFlagRepository assignedRepository)
            {
                _repository = assignedRepository;
                return;
            }

            if (LocalProgressFlagRepository.Instance != null)
            {
                _repository = LocalProgressFlagRepository.Instance;
            }
        }

        private bool RequireServer(out ServerActionResult result)
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager != null &&
                networkManager.IsListening &&
                !networkManager.IsServer)
            {
                result = ServerActionResult.Fail(
                    ServerActionErrorCode.PermissionDenied,
                    "Progress flags can only be changed by the server.");
                return false;
            }

            result = ServerActionResult.Ok();
            return true;
        }

        private void PublishChange(ProgressFlagChange change)
        {
            FlagChanged?.Invoke(change);

            if (verboseLogging)
            {
                Debug.Log(
                    $"[ProgressFlagService] {change.ChangeKind}: Character={change.CharacterId}, " +
                    $"Flag/Prefix={change.FlagIdOrPrefix}, Lifetime={change.Lifetime}, Source={change.Source}");
            }
        }

        private static void NotifyClientFlagSet(
            ulong clientId,
            string flagId,
            ProgressFlagLifetime lifetime)
        {
            if (clientId == ulong.MaxValue)
            {
                return;
            }

            if (!TryGetClientProgressState(clientId, out ClientProgressFlagState clientState))
            {
                return;
            }

            clientState.SendFlagSet(clientId, flagId, lifetime);
        }

        private static void NotifyClientFlagRemoved(ulong clientId, string flagId)
        {
            if (clientId == ulong.MaxValue)
            {
                return;
            }

            if (!TryGetClientProgressState(clientId, out ClientProgressFlagState clientState))
            {
                return;
            }

            clientState.SendFlagRemoved(clientId, flagId);
        }

        private static void NotifyClientPrefixCleared(ulong clientId, string prefix)
        {
            if (clientId == ulong.MaxValue)
            {
                return;
            }

            if (!TryGetClientProgressState(clientId, out ClientProgressFlagState clientState))
            {
                return;
            }

            clientState.SendPrefixCleared(clientId, prefix);
        }

        private static bool TryGetClientProgressState(
            ulong clientId,
            out ClientProgressFlagState clientState)
        {
            clientState = null;

            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null ||
                !networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return false;
            }

            return client.PlayerObject.TryGetComponent(out clientState);
        }
    }
}