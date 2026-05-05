using System.Collections.Generic;
using ROC.Game.Common;
using ROC.Game.ObjectUsage;
using ROC.Game.ProgressFlags;
using ROC.Networking.Interactions.Data;
using ROC.Networking.Sessions;
using ROC.Networking.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    /// <summary>
    /// Reusable usage gate for interactables. This is not the action itself; attach it beside
    /// an InteractionExecutor or legacy IServerInteractable.
    ///
    /// Supports:
    /// - per-character use limits
    /// - global/shared capacity
    /// - per-character hiding after use
    /// - global hiding after depletion
    /// - runtime-only or object-scoped persistent usage state
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkInteractableTarget))]
    [RequireComponent(typeof(NetworkInstanceObject))]
    public class InteractionUsageGate : NetworkBehaviour, IInteractionAvailabilityRule, IInteractionUseObserver, IInstanceVisibilityRule, IInteractionAvailabilityDependencyProvider
    {
        [Header("Per-Character Use")]
        [SerializeField] private bool limitEachCharacterToOneUse = true;
        [SerializeField] private bool hideForCharacterAfterUse;

        [Header("Global Capacity")]
        [SerializeField] private bool useGlobalCapacity;
        [SerializeField, Min(1)] private int globalUseCapacity = 1;
        [SerializeField] private bool hideGloballyWhenDepleted;

        [Header("Persistence")]
        [SerializeField] private ObjectUsagePersistenceMode persistenceMode = ObjectUsagePersistenceMode.PersistentObjectUsage;
        [SerializeField] private ObjectUsageKeyScope usageKeyScope = ObjectUsageKeyScope.SceneAndStableObject;
        [Tooltip("Optional explicit usage key. If empty, the key is generated from scene/instance/stable object identity according to Usage Key Scope.")]
        [SerializeField] private string usageKeyOverride;
        [Tooltip("Optional group used for later cleanup, such as intro, event.spring_2026, construction.phase_3, or dungeon_run_123.")]
        [SerializeField] private string cleanupGroup;
        [Tooltip("Source label written into the object usage repository.")]
        [SerializeField] private string source = "interaction_usage_gate";

        [Header("Legacy Progress Flag Migration")]
        [Tooltip("If true, an old progress-flag usage record can be converted into object usage the first time this object checks that character.")]
        [SerializeField] private bool migrateLegacyProgressFlagUsage;
        [SerializeField] private string legacyProgressFlagIdOverride;
        [SerializeField] private string legacyProgressFlagPrefix = "interaction.used";

        [Header("Local Presentation Hide Fallback")]
        [Tooltip("When an object is hidden for the local client, disable renderers locally as a presentation fallback. This is especially useful for host mode, where network visibility can leave the server-owned object visible locally.")]
        [SerializeField] private bool hideRenderersLocallyWhenHidden = true;

        [Tooltip("Optional root whose child renderers should be hidden locally. If empty, this object is used.")]
        [SerializeField] private GameObject localPresentationRoot;

        [Tooltip("Optional explicit renderer list. If empty, renderers are discovered under Local Presentation Root or this object.")]
        [SerializeField] private List<Renderer> renderersToHide = new();

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private readonly HashSet<string> _runtimeUsedCharacterIds = new(System.StringComparer.Ordinal);
        private readonly NetworkList<ulong> _usedClientIds = new();
        private readonly List<Renderer> _runtimeRenderers = new();
        private readonly List<bool> _runtimeRendererInitialEnabled = new();
        private readonly NetworkVariable<int> _globalUseCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private NetworkObject _networkObject;
        private NetworkInstanceObject _instanceObject;

        private bool ShouldTrackCharacterUse => limitEachCharacterToOneUse || hideForCharacterAfterUse;
        private int MaxGlobalUses => Mathf.Max(1, globalUseCapacity);

        public InteractionRuleDependencyFlags LocalPreviewDependencies => InteractionRuleDependencyFlags.ObjectUsage;

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
            _instanceObject = GetComponent<NetworkInstanceObject>();
            RebuildLocalPresentationRendererCache();
        }

        public override void OnNetworkSpawn()
        {
            _usedClientIds.OnListChanged += HandleUsedClientIdsChanged;
            _globalUseCount.OnValueChanged += HandleGlobalUseCountChanged;

            if (IsServer)
            {
                RefreshAllConnectedClientMirrors();
                RefreshGlobalUseMirror();
                InstanceVisibilityService.Instance?.RefreshObject(_instanceObject);
            }

            ApplyLocalPresentationVisibility();
            InvalidateOwningTargetLocalAvailability();
        }

        public override void OnNetworkDespawn()
        {
            _usedClientIds.OnListChanged -= HandleUsedClientIdsChanged;
            _globalUseCount.OnValueChanged -= HandleGlobalUseCountChanged;
        }

        public bool CanSelect(ulong clientId, out string reason)
        {
            reason = string.Empty;

            if (!isActiveAndEnabled)
            {
                reason = "Target is inactive.";
                return false;
            }

            if (IsServer)
            {
                return CanInteractServer(clientId, out reason);
            }

            if (useGlobalCapacity && _globalUseCount.Value >= MaxGlobalUses)
            {
                reason = "Object is depleted.";
                return false;
            }

            if (ShouldTrackCharacterUse && ContainsClientId(clientId))
            {
                reason = "Already used.";
                return false;
            }

            return true;
        }

        public bool CanInteract(ulong clientId, NetworkObject actor, out string reason)
        {
            reason = string.Empty;

            if (!IsServer)
            {
                return CanSelect(clientId, out reason);
            }

            return CanInteractServer(clientId, out reason);
        }

        public void HandleInteractionSucceeded(ulong clientId, NetworkObject actor, IServerInteractable executedInteractable)
        {
            if (!IsServer)
            {
                return;
            }

            if (!ShouldTrackCharacterUse && !useGlobalCapacity)
            {
                return;
            }

            RecordUseServer(clientId);
        }

        public bool IsVisibleToClient(ulong clientId)
        {
            if (!IsServer)
            {
                if (hideGloballyWhenDepleted && useGlobalCapacity && _globalUseCount.Value >= MaxGlobalUses)
                {
                    return false;
                }

                if (hideForCharacterAfterUse && ContainsClientId(clientId))
                {
                    return false;
                }

                return true;
            }

            EnsureClientMirrorState(clientId);
            RefreshGlobalUseMirror();

            if (hideGloballyWhenDepleted && IsGlobalCapacityReachedServer())
            {
                return false;
            }

            if (hideForCharacterAfterUse && HasCharacterUsedServer(clientId))
            {
                return false;
            }

            return true;
        }

        public ServerActionResult ResetRuntimeUsageServer()
        {
            if (!IsServer)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.PermissionDenied, "Usage can only be reset by the server.");
            }

            _runtimeUsedCharacterIds.Clear();
            _usedClientIds.Clear();
            _globalUseCount.Value = 0;
            InstanceVisibilityService.Instance?.RefreshObject(_instanceObject);
            ApplyLocalPresentationVisibility();
            InvalidateOwningTargetLocalAvailability();
            return ServerActionResult.Ok();
        }

        public ServerActionResult DeletePersistentUsageServer()
        {
            if (!IsServer)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.PermissionDenied, "Usage can only be deleted by the server.");
            }

            if (!TryBuildUsageIdentity(ulong.MaxValue, out UsageIdentity identity, allowSessionFallback: false))
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidRequest, "Cannot delete usage because no valid usage key could be built.");
            }

            ServerActionResult result = ObjectUsageService.Instance != null
                ? ObjectUsageService.Instance.DeleteUsage(identity.UsageKey)
                : ServerActionResult.Fail(ServerActionErrorCode.PersistenceUnavailable, "ObjectUsageService is unavailable.");

            if (result.Success)
            {
                ResetRuntimeUsageServer();
            }

            return result;
        }

        private bool CanInteractServer(ulong clientId, out string reason)
        {
            reason = string.Empty;

            EnsureClientMirrorState(clientId);
            RefreshGlobalUseMirror();

            if (useGlobalCapacity && IsGlobalCapacityReachedServer())
            {
                reason = "Object is depleted.";
                return false;
            }

            if (limitEachCharacterToOneUse && HasCharacterUsedServer(clientId))
            {
                reason = "Already used.";
                return false;
            }

            return true;
        }

        private void RecordUseServer(ulong clientId)
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                if (verboseLogging)
                {
                    Debug.LogWarning("[InteractionUsageGate] Cannot record use; client has no selected character.", this);
                }
                return;
            }

            if (persistenceMode == ObjectUsagePersistenceMode.RuntimeOnly)
            {
                if (ShouldTrackCharacterUse)
                {
                    _runtimeUsedCharacterIds.Add(characterId);
                    AddClientMirror(clientId);
                }

                if (useGlobalCapacity)
                {
                    _globalUseCount.Value = Mathf.Max(0, _globalUseCount.Value) + 1;
                }

                InstanceVisibilityService.Instance?.RefreshObject(_instanceObject);
                ApplyLocalPresentationVisibility();
                InvalidateOwningTargetLocalAvailability();
                return;
            }

            if (!TryBuildUsageIdentity(clientId, out UsageIdentity identity, allowSessionFallback: true))
            {
                Debug.LogWarning("[InteractionUsageGate] Cannot record persistent use; no valid object usage key could be built.", this);
                return;
            }

            if (ObjectUsageService.Instance == null)
            {
                Debug.LogWarning("[InteractionUsageGate] Cannot record persistent use; ObjectUsageService is unavailable.", this);
                return;
            }

            ServerActionResult result = ObjectUsageService.Instance.RecordUseForClient(
                clientId,
                identity.UsageKey,
                identity.SceneId,
                identity.InstanceId,
                identity.StableObjectId,
                cleanupGroup,
                ShouldTrackCharacterUse,
                useGlobalCapacity,
                source);

            if (!result.Success)
            {
                Debug.LogWarning($"[InteractionUsageGate] Failed to record usage: {result}", this);
                return;
            }

            if (ShouldTrackCharacterUse)
            {
                AddClientMirror(clientId);
            }

            RefreshGlobalUseMirror();
            InstanceVisibilityService.Instance?.RefreshObject(_instanceObject);
            ApplyLocalPresentationVisibility();
            InvalidateOwningTargetLocalAvailability();

            if (verboseLogging)
            {
                Debug.Log($"[InteractionUsageGate] Recorded usage. Client={clientId}, Character={characterId}, Key={identity.UsageKey}", this);
            }
        }

        private bool HasCharacterUsedServer(ulong clientId)
        {
            if (!ShouldTrackCharacterUse)
            {
                return false;
            }

            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ContainsClientId(clientId);
            }

            if (persistenceMode == ObjectUsagePersistenceMode.RuntimeOnly)
            {
                return _runtimeUsedCharacterIds.Contains(characterId);
            }

            if (!TryBuildUsageIdentity(clientId, out UsageIdentity identity, allowSessionFallback: true))
            {
                return false;
            }

            bool used = ObjectUsageService.Instance != null &&
                        ObjectUsageService.Instance.HasCharacterUsedForClient(clientId, identity.UsageKey);

            if (!used && TryMigrateLegacyProgressFlag(clientId, identity))
            {
                used = true;
            }

            if (used)
            {
                AddClientMirror(clientId);
            }

            return used;
        }

        private bool IsGlobalCapacityReachedServer()
        {
            if (!useGlobalCapacity)
            {
                return false;
            }

            if (persistenceMode == ObjectUsagePersistenceMode.RuntimeOnly)
            {
                return _globalUseCount.Value >= MaxGlobalUses;
            }

            if (!TryBuildUsageIdentity(ulong.MaxValue, out UsageIdentity identity, allowSessionFallback: false))
            {
                return _globalUseCount.Value >= MaxGlobalUses;
            }

            return ObjectUsageService.Instance != null &&
                   ObjectUsageService.Instance.IsGlobalCapacityReached(identity.UsageKey, MaxGlobalUses);
        }

        private void RefreshGlobalUseMirror()
        {
            if (!IsServer || !useGlobalCapacity)
            {
                return;
            }

            if (persistenceMode == ObjectUsagePersistenceMode.RuntimeOnly)
            {
                return;
            }

            if (!TryBuildUsageIdentity(ulong.MaxValue, out UsageIdentity identity, allowSessionFallback: false))
            {
                return;
            }

            if (ObjectUsageService.Instance != null)
            {
                _globalUseCount.Value = ObjectUsageService.Instance.GetGlobalUseCount(identity.UsageKey);
            }
        }

        private void EnsureClientMirrorState(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            if (ShouldTrackCharacterUse && HasCharacterUsedServer(clientId))
            {
                AddClientMirror(clientId);
            }
        }

        private void RefreshAllConnectedClientMirrors()
        {
            if (!IsServer || NetworkManager.Singleton == null)
            {
                return;
            }

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                EnsureClientMirrorState(clientId);
            }
        }

        private bool TryMigrateLegacyProgressFlag(ulong clientId, UsageIdentity identity)
        {
            if (!migrateLegacyProgressFlagUsage || ProgressFlagService.Instance == null)
            {
                return false;
            }

            string legacyFlagId = BuildLegacyProgressFlagId(identity);
            if (string.IsNullOrWhiteSpace(legacyFlagId))
            {
                return false;
            }

            if (!ProgressFlagService.Instance.HasFlagForClient(clientId, legacyFlagId))
            {
                return false;
            }

            if (ObjectUsageService.Instance == null)
            {
                return true;
            }

            ServerActionResult result = ObjectUsageService.Instance.RecordUseForClient(
                clientId,
                identity.UsageKey,
                identity.SceneId,
                identity.InstanceId,
                identity.StableObjectId,
                cleanupGroup,
                recordCharacterUse: true,
                incrementGlobalUse: false,
                source: "legacy_progress_flag_migration");

            if (!result.Success)
            {
                Debug.LogWarning($"[InteractionUsageGate] Legacy usage migration failed: {result}", this);
            }

            return true;
        }

        private string BuildLegacyProgressFlagId(UsageIdentity identity)
        {
            if (!string.IsNullOrWhiteSpace(legacyProgressFlagIdOverride))
            {
                return legacyProgressFlagIdOverride;
            }

            string prefix = string.IsNullOrWhiteSpace(legacyProgressFlagPrefix)
                ? "interaction.used"
                : legacyProgressFlagPrefix.Trim().TrimEnd('.');

            string stableId = ObjectUsageKeyUtility.NormalizePart(identity.StableObjectId);
            return string.IsNullOrWhiteSpace(stableId) ? string.Empty : $"{prefix}.{stableId}";
        }

        private bool TryBuildUsageIdentity(
            ulong clientId,
            out UsageIdentity identity,
            bool allowSessionFallback)
        {
            identity = default;

            string sceneId = string.Empty;
            string instanceId = string.Empty;

            if (clientId != ulong.MaxValue && PlayerSessionRegistry.Instance != null && PlayerSessionRegistry.Instance.TryGet(clientId, out PlayerSessionData session))
            {
                sceneId = session.SceneId ?? string.Empty;
                instanceId = session.InstanceId ?? string.Empty;
            }

            if (allowSessionFallback || string.IsNullOrWhiteSpace(sceneId))
            {
                if (string.IsNullOrWhiteSpace(sceneId))
                {
                    sceneId = gameObject.scene.name;
                }
            }

            if (string.IsNullOrWhiteSpace(instanceId) && _instanceObject != null)
            {
                instanceId = _instanceObject.InstanceIdString;
            }

            string stableObjectId = GetStableObjectId();
            string usageKey = ObjectUsageKeyUtility.BuildKey(
                usageKeyScope,
                usageKeyOverride,
                sceneId,
                instanceId,
                stableObjectId);

            if (string.IsNullOrWhiteSpace(usageKey))
            {
                return false;
            }

            identity = new UsageIdentity
            {
                UsageKey = usageKey,
                SceneId = sceneId ?? string.Empty,
                InstanceId = instanceId ?? string.Empty,
                StableObjectId = stableObjectId ?? string.Empty
            };

            return true;
        }

        private string GetStableObjectId()
        {
            if (_instanceObject != null)
            {
                string stableId = _instanceObject.StableObjectId.Value.ToString();
                if (!string.IsNullOrWhiteSpace(stableId))
                {
                    return stableId;
                }
            }

            if (!string.IsNullOrWhiteSpace(usageKeyOverride))
            {
                return usageKeyOverride;
            }

            return gameObject.name;
        }

        private bool TryGetCharacterId(ulong clientId, out string characterId)
        {
            characterId = string.Empty;
            PlayerSessionRegistry registry = PlayerSessionRegistry.Instance;
            return registry != null &&
                   registry.TryGetCharacterId(clientId, out characterId) &&
                   !string.IsNullOrWhiteSpace(characterId);
        }

        private bool ContainsClientId(ulong clientId)
        {
            if (_usedClientIds == null)
            {
                return false;
            }

            for (int i = 0; i < _usedClientIds.Count; i++)
            {
                if (_usedClientIds[i] == clientId)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddClientMirror(ulong clientId)
        {
            if (!IsServer || _usedClientIds == null || ContainsClientId(clientId))
            {
                return;
            }

            _usedClientIds.Add(clientId);
        }



        private void HandleUsedClientIdsChanged(NetworkListEvent<ulong> changeEvent)
        {
            ApplyLocalPresentationVisibility();
            InvalidateOwningTargetLocalAvailability();
        }

        private void HandleGlobalUseCountChanged(int previousValue, int newValue)
        {
            ApplyLocalPresentationVisibility();
            InvalidateOwningTargetLocalAvailability();
        }

        private void RebuildLocalPresentationRendererCache()
        {
            _runtimeRenderers.Clear();
            _runtimeRendererInitialEnabled.Clear();

            if (renderersToHide != null && renderersToHide.Count > 0)
            {
                for (int i = 0; i < renderersToHide.Count; i++)
                {
                    Renderer assigned = renderersToHide[i];
                    if (assigned == null || _runtimeRenderers.Contains(assigned))
                    {
                        continue;
                    }

                    _runtimeRenderers.Add(assigned);
                    _runtimeRendererInitialEnabled.Add(assigned.enabled);
                }

                return;
            }

            GameObject root = localPresentationRoot != null ? localPresentationRoot : gameObject;
            Renderer[] discovered = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < discovered.Length; i++)
            {
                Renderer rendererToHide = discovered[i];
                if (rendererToHide == null || _runtimeRenderers.Contains(rendererToHide))
                {
                    continue;
                }

                _runtimeRenderers.Add(rendererToHide);
                _runtimeRendererInitialEnabled.Add(rendererToHide.enabled);
            }
        }

        private void ApplyLocalPresentationVisibility()
        {
            if (!hideRenderersLocallyWhenHidden)
            {
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            {
                return;
            }

            if (_runtimeRenderers.Count == 0)
            {
                RebuildLocalPresentationRendererCache();
            }

            bool shouldHide = ShouldHideForLocalClientPresentation();
            for (int i = 0; i < _runtimeRenderers.Count; i++)
            {
                Renderer rendererToHide = _runtimeRenderers[i];
                if (rendererToHide == null)
                {
                    continue;
                }

                bool originalEnabled = i < _runtimeRendererInitialEnabled.Count && _runtimeRendererInitialEnabled[i];
                rendererToHide.enabled = !shouldHide && originalEnabled;
            }
        }

        private void RestoreLocalPresentationVisibility()
        {
            if (_runtimeRenderers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _runtimeRenderers.Count; i++)
            {
                Renderer rendererToRestore = _runtimeRenderers[i];
                if (rendererToRestore == null)
                {
                    continue;
                }

                bool originalEnabled = i < _runtimeRendererInitialEnabled.Count && _runtimeRendererInitialEnabled[i];
                rendererToRestore.enabled = originalEnabled;
            }
        }

        private bool ShouldHideForLocalClientPresentation()
        {
            if (hideGloballyWhenDepleted && useGlobalCapacity && _globalUseCount.Value >= MaxGlobalUses)
            {
                return true;
            }

            if (!hideForCharacterAfterUse)
            {
                return false;
            }

            if (NetworkManager.Singleton == null)
            {
                return false;
            }

            return ContainsClientId(NetworkManager.Singleton.LocalClientId);
        }


        private void InvalidateOwningTargetLocalAvailability()
        {
            NetworkInteractableTarget target = GetComponent<NetworkInteractableTarget>();
            if (target != null)
            {
                target.InvalidateLocalAvailability(InteractionRuleDependencyFlags.ObjectUsage);
            }
        }

        private struct UsageIdentity
        {
            public string UsageKey;
            public string SceneId;
            public string InstanceId;
            public string StableObjectId;
        }
    }
}
