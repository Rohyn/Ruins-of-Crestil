using System;
using System.Collections.Generic;
using ROC.Game.Common;
using ROC.Networking.Conditions;
using ROC.Networking.Sessions;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Game.Conditions
{
    [DisallowMultipleComponent]
    public sealed class ConditionService : MonoBehaviour
    {
        public static ConditionService Instance { get; private set; }

        [Header("Catalog")]
        [SerializeField] private ConditionCatalog conditionCatalog;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private readonly Dictionary<string, Dictionary<string, ActiveCondition>> _conditionsByCharacterId = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool HasConditionForClient(ulong clientId, string conditionId)
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return false;
            }

            return HasCondition(characterId, conditionId);
        }

        public bool HasCondition(string characterId, string conditionId)
        {
            if (string.IsNullOrWhiteSpace(characterId) ||
                string.IsNullOrWhiteSpace(conditionId))
            {
                return false;
            }

            return _conditionsByCharacterId.TryGetValue(characterId, out Dictionary<string, ActiveCondition> set)
                   && set.ContainsKey(conditionId);
        }

        public ServerActionResult ApplyConditionForClient(
            ulong clientId,
            string conditionId,
            ConditionSourceType sourceType,
            string sourceId)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot apply condition because no character is selected.");
            }

            return ApplyCondition(clientId, characterId, conditionId, sourceType, sourceId);
        }

        public ServerActionResult RemoveConditionForCharacter(
            string characterId,
            string conditionId)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (string.IsNullOrWhiteSpace(characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidRequest,
                    "Cannot remove condition because characterId is empty.");
            }

            return RemoveCondition(ulong.MaxValue, characterId, conditionId);
        }

        public ServerActionResult RemoveConditionForClient(
            ulong clientId,
            string conditionId)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot remove condition because no character is selected.");
            }

            return RemoveCondition(clientId, characterId, conditionId);
        }

        public ServerActionResult RemoveConditionFromSourceForClient(
            ulong clientId,
            string conditionId,
            ConditionSourceType sourceType,
            string sourceId)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot remove condition because no character is selected.");
            }

            return RemoveConditionFromSource(
                clientId,
                characterId,
                conditionId,
                sourceType,
                sourceId);
        }

        public ServerActionResult RemoveAllConditionsFromSourceForClient(
            ulong clientId,
            ConditionSourceType sourceType,
            string sourceId)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot remove conditions because no character is selected.");
            }

            return RemoveAllConditionsFromSource(
                clientId,
                characterId,
                sourceType,
                sourceId);
        }

        public PlayerControlBlockFlags GetControlBlocksForClient(ulong clientId)
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return PlayerControlBlockFlags.None;
            }

            return ComputeControlBlocks(characterId);
        }

        public void RefreshConditionStateForClient(ulong clientId)
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return;
            }

            RefreshAvatarConditionState(clientId, characterId);
        }

        private ServerActionResult ApplyCondition(
            ulong clientId,
            string characterId,
            string conditionId,
            ConditionSourceType sourceType,
            string sourceId)
        {
            if (conditionCatalog == null ||
                !conditionCatalog.TryGet(conditionId, out ConditionDefinition definition))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidRequest,
                    $"Unknown condition: {conditionId}");
            }

            Dictionary<string, ActiveCondition> set = GetOrCreateConditionSet(characterId);

            // First-pass model: one active instance per condition ID.
            // Later, this can become condition ID + source ID if we need simultaneous source instances.
            set[conditionId] = new ActiveCondition
            {
                ConditionId = conditionId,
                Definition = definition,
                SourceType = sourceType,
                SourceId = sourceId ?? string.Empty,
                AppliedUtc = DateTime.UtcNow.ToString("O")
            };

            RefreshAvatarConditionState(clientId, characterId);

            if (verboseLogging)
            {
                Debug.Log($"[ConditionService] Applied {conditionId} to {characterId} from {sourceType}:{sourceId}");
            }

            return ServerActionResult.Ok();
        }

        private ServerActionResult RemoveCondition(
            ulong clientId,
            string characterId,
            string conditionId)
        {
            if (_conditionsByCharacterId.TryGetValue(characterId, out Dictionary<string, ActiveCondition> set))
            {
                set.Remove(conditionId);
            }

            RefreshAvatarConditionState(clientId, characterId);

            if (verboseLogging)
            {
                Debug.Log($"[ConditionService] Removed {conditionId} from {characterId}");
            }

            return ServerActionResult.Ok();
        }

        private ServerActionResult RemoveConditionFromSource(
            ulong clientId,
            string characterId,
            string conditionId,
            ConditionSourceType sourceType,
            string sourceId)
        {
            if (_conditionsByCharacterId.TryGetValue(characterId, out Dictionary<string, ActiveCondition> set) &&
                set.TryGetValue(conditionId, out ActiveCondition active) &&
                active.SourceType == sourceType &&
                active.SourceId == (sourceId ?? string.Empty))
            {
                set.Remove(conditionId);
            }

            RefreshAvatarConditionState(clientId, characterId);

            if (verboseLogging)
            {
                Debug.Log($"[ConditionService] Removed {conditionId} from {characterId} for source {sourceType}:{sourceId}");
            }

            return ServerActionResult.Ok();
        }

        private ServerActionResult RemoveAllConditionsFromSource(
            ulong clientId,
            string characterId,
            ConditionSourceType sourceType,
            string sourceId)
        {
            int removed = 0;
            string normalizedSourceId = sourceId ?? string.Empty;

            if (_conditionsByCharacterId.TryGetValue(characterId, out Dictionary<string, ActiveCondition> set))
            {
                List<string> toRemove = null;

                foreach (KeyValuePair<string, ActiveCondition> pair in set)
                {
                    ActiveCondition active = pair.Value;

                    if (active.SourceType != sourceType || active.SourceId != normalizedSourceId)
                    {
                        continue;
                    }

                    toRemove ??= new List<string>();
                    toRemove.Add(pair.Key);
                }

                if (toRemove != null)
                {
                    for (int i = 0; i < toRemove.Count; i++)
                    {
                        set.Remove(toRemove[i]);
                        removed++;
                    }
                }
            }

            RefreshAvatarConditionState(clientId, characterId);

            if (verboseLogging)
            {
                Debug.Log(
                    $"[ConditionService] Removed {removed} condition(s) from {characterId} " +
                    $"for source {sourceType}:{sourceId}");
            }

            return ServerActionResult.Ok($"Removed {removed} condition(s).");
        }

        private Dictionary<string, ActiveCondition> GetOrCreateConditionSet(string characterId)
        {
            if (_conditionsByCharacterId.TryGetValue(characterId, out Dictionary<string, ActiveCondition> set))
            {
                return set;
            }

            set = new Dictionary<string, ActiveCondition>(StringComparer.Ordinal);
            _conditionsByCharacterId.Add(characterId, set);
            return set;
        }

        private PlayerControlBlockFlags ComputeControlBlocks(string characterId)
        {
            PlayerControlBlockFlags flags = PlayerControlBlockFlags.None;

            if (!_conditionsByCharacterId.TryGetValue(characterId, out Dictionary<string, ActiveCondition> set))
            {
                return flags;
            }

            foreach (ActiveCondition active in set.Values)
            {
                IReadOnlyList<ConditionEffectDefinition> effects = active.Definition.Effects;

                for (int i = 0; i < effects.Count; i++)
                {
                    switch (effects[i].EffectType)
                    {
                        case ConditionEffectType.BlockMovement:
                            flags |= PlayerControlBlockFlags.Movement;
                            break;

                        case ConditionEffectType.BlockJump:
                            flags |= PlayerControlBlockFlags.Jump;
                            break;

                        case ConditionEffectType.BlockSprint:
                            flags |= PlayerControlBlockFlags.Sprint;
                            break;

                        case ConditionEffectType.BlockGravity:
                            flags |= PlayerControlBlockFlags.Gravity;
                            break;
                    }
                }
            }

            return flags;
        }

        private void RefreshAvatarConditionState(ulong clientId, string characterId)
        {
            if (clientId == ulong.MaxValue)
            {
                return;
            }

            PlayerSessionRegistry registry = PlayerSessionRegistry.Instance;

            if (registry == null ||
                !registry.TryGetAvatarObject(clientId, out NetworkObject avatarObject) ||
                avatarObject == null)
            {
                return;
            }

            if (!avatarObject.TryGetComponent(out NetworkPlayerConditionState state))
            {
                return;
            }

            PlayerControlBlockFlags blocks = ComputeControlBlocks(characterId);
            state.SetControlBlocksServer(blocks);

            // Intentionally no anchor/rest presentation here.
            // AnchoringService owns IsAnchored / AnchorExitPrompt.
            // ConditionService only owns condition-derived control effects.
        }

        private static bool TryGetCharacterId(ulong clientId, out string characterId)
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

            if (networkManager != null &&
                networkManager.IsListening &&
                !networkManager.IsServer)
            {
                result = ServerActionResult.Fail(
                    ServerActionErrorCode.PermissionDenied,
                    "Conditions can only be changed by the server.");
                return false;
            }

            result = ServerActionResult.Ok();
            return true;
        }

        private sealed class ActiveCondition
        {
            public string ConditionId;
            public ConditionDefinition Definition;
            public ConditionSourceType SourceType;
            public string SourceId;
            public string AppliedUtc;
        }
    }
}