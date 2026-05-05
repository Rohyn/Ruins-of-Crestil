using System.Collections.Generic;
using ROC.Game.ProgressFlags;
using ROC.Networking.Conditions;
using ROC.Networking.Interactions.Data;
using ROC.Networking.Inventory;
using ROC.Networking.ProgressFlags;
using ROC.Networking.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkInstanceObject))]
    public sealed class NetworkInteractableTarget : NetworkBehaviour
    {
        [Header("Display")]
        [SerializeField] private string interactionPrompt = "Interact";

        [Header("Selection")]
        [Tooltip("Higher values make this object more likely to be selected when competing with nearby interactables.")]
        [SerializeField] private float selectionPriorityBonus;

        [Tooltip("Maximum client-side selection distance. Set to 0 or less to ignore this client-side limit.")]
        [SerializeField] private float maxSelectionDistance = 0f;

        [Header("Interaction Geometry")]
        [Tooltip("Optional colliders used for selection/evaluation. If empty, child colliders are discovered at runtime.")]
        [SerializeField] private List<Collider> interactionColliders = new();

        [Tooltip("Optional focus points used for facing/LOS/prompt positioning. If empty, this object's transform is used.")]
        [SerializeField] private List<Transform> interactionFoci = new();

        [Header("Client Preview Cache")]
        [Tooltip("Caches client-side interaction rule preview until relevant mirrored gameplay state changes. Server validation is still authoritative.")]
        [SerializeField] private bool cacheClientPreviewRules = true;

        [Header("Debug")]
        [SerializeField] private bool verbosePreviewCacheLogging;

        private readonly List<Collider> _runtimeColliders = new();
        private readonly List<IInteractionAvailabilityRule> _availabilityRules = new();
        private NetworkObject _networkObject;

        private InteractionRuleDependencyFlags _localPreviewDependencies = InteractionRuleDependencyFlags.None;
        private bool _localPreviewDirty = true;
        private bool _hasLocalPreviewCache;
        private bool _cachedLocalPreviewSelectable = true;
        private string _cachedLocalPreviewReason = string.Empty;

        private ClientInventoryState _subscribedInventoryState;
        private ClientProgressFlagState _subscribedProgressFlagState;
        private NetworkPlayerConditionState _subscribedConditionState;

        public string InteractionPrompt => interactionPrompt;
        public float SelectionPriorityBonus => selectionPriorityBonus;
        public float MaxSelectionDistance => maxSelectionDistance;
        public InteractionRuleDependencyFlags LocalPreviewDependencies => _localPreviewDependencies;

        public ulong TargetNetworkObjectId =>
            _networkObject != null && _networkObject.IsSpawned ? _networkObject.NetworkObjectId : 0UL;

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
            RebuildRuntimeColliderCache();
            RebuildAvailabilityRuleCache();
            MarkLocalPreviewDirty(InteractionRuleDependencyFlags.All, forceSelectorRefresh: false);
        }

        private void OnEnable()
        {
            RebuildRuntimeColliderCache();
            RebuildAvailabilityRuleCache();
            MarkLocalPreviewDirty(InteractionRuleDependencyFlags.All, forceSelectorRefresh: false);
        }

        private void OnDisable()
        {
            UnsubscribeFromLocalStateMirrors();
            _hasLocalPreviewCache = false;
            _localPreviewDirty = true;
        }

        public override void OnNetworkSpawn()
        {
            MarkLocalPreviewDirty(InteractionRuleDependencyFlags.All, forceSelectorRefresh: false);
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromLocalStateMirrors();
            _hasLocalPreviewCache = false;
            _localPreviewDirty = true;
        }

        public bool IsSelectable()
        {
            if (!isActiveAndEnabled || _networkObject == null || !_networkObject.IsSpawned)
            {
                return false;
            }

            RebuildAvailabilityRuleCacheIfNeeded();
            EnsureLocalStateMirrorSubscriptions();

            if (!cacheClientPreviewRules)
            {
                return EvaluateLocalAvailability(out _);
            }

            if (!_hasLocalPreviewCache || _localPreviewDirty)
            {
                _cachedLocalPreviewSelectable = EvaluateLocalAvailability(out _cachedLocalPreviewReason);
                _hasLocalPreviewCache = true;
                _localPreviewDirty = false;

                if (verbosePreviewCacheLogging)
                {
                    string result = _cachedLocalPreviewSelectable ? "PASS" : "FAIL";
                    Debug.Log($"[NetworkInteractableTarget] Rebuilt local preview cache for '{name}': {result} ({_cachedLocalPreviewReason})", this);
                }
            }

            return _cachedLocalPreviewSelectable;
        }

        public void InvalidateLocalAvailability(InteractionRuleDependencyFlags dirtyDependencies = InteractionRuleDependencyFlags.All)
        {
            MarkLocalPreviewDirty(dirtyDependencies, forceSelectorRefresh: true);
        }

        public bool TryGetTargetNetworkObject(out NetworkObject networkObject)
        {
            networkObject = _networkObject;
            return networkObject != null && networkObject.IsSpawned;
        }

        public Vector3 GetInteractionEvaluationPoint(Vector3 referencePosition)
        {
            RebuildRuntimeColliderCacheIfNeeded();

            bool found = false;
            Vector3 bestPoint = transform.position;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < _runtimeColliders.Count; i++)
            {
                Collider candidate = _runtimeColliders[i];
                if (candidate == null || !candidate.enabled)
                {
                    continue;
                }

                Vector3 point = candidate.ClosestPoint(referencePosition);
                float sqrDistance = (point - referencePosition).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestPoint = point;
                    found = true;
                }
            }

            return found ? bestPoint : transform.position;
        }

        public Vector3 GetBestInteractionFocusPosition(Vector3 referencePosition)
        {
            bool found = false;
            Vector3 bestPoint = transform.position;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < interactionFoci.Count; i++)
            {
                Transform focus = interactionFoci[i];
                if (focus == null)
                {
                    continue;
                }

                float sqrDistance = (focus.position - referencePosition).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestPoint = focus.position;
                    found = true;
                }
            }

            return found ? bestPoint : transform.position;
        }

        private bool EvaluateLocalAvailability(out string reason)
        {
            reason = string.Empty;
            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;

            for (int i = 0; i < _availabilityRules.Count; i++)
            {
                IInteractionAvailabilityRule rule = _availabilityRules[i];
                if (rule == null)
                {
                    continue;
                }

                if (!rule.CanSelect(localClientId, out reason))
                {
                    if (string.IsNullOrWhiteSpace(reason))
                    {
                        reason = "Target is not currently selectable.";
                    }

                    return false;
                }
            }

            return true;
        }

        private void MarkLocalPreviewDirty(
            InteractionRuleDependencyFlags dirtyDependencies,
            bool forceSelectorRefresh)
        {
            if (dirtyDependencies == InteractionRuleDependencyFlags.None)
            {
                return;
            }

            bool affectsThisTarget = dirtyDependencies == InteractionRuleDependencyFlags.All ||
                                     _localPreviewDependencies == InteractionRuleDependencyFlags.None ||
                                     (_localPreviewDependencies & dirtyDependencies) != 0;

            if (!affectsThisTarget)
            {
                return;
            }

            _localPreviewDirty = true;

            if (verbosePreviewCacheLogging)
            {
                Debug.Log($"[NetworkInteractableTarget] Marked local preview dirty for '{name}'. Dirty={dirtyDependencies}, TargetDeps={_localPreviewDependencies}", this);
            }

            if (!forceSelectorRefresh || PlayerInteractionSelector.Local == null)
            {
                return;
            }

            if (PlayerInteractionSelector.Local.CurrentTarget == this)
            {
                PlayerInteractionSelector.Local.ForceRefresh();
            }
        }

        private void RebuildRuntimeColliderCacheIfNeeded()
        {
            if (_runtimeColliders.Count == 0)
            {
                RebuildRuntimeColliderCache();
            }
        }

        private void RebuildRuntimeColliderCache()
        {
            _runtimeColliders.Clear();

            for (int i = 0; i < interactionColliders.Count; i++)
            {
                Collider assigned = interactionColliders[i];
                if (assigned != null)
                {
                    _runtimeColliders.Add(assigned);
                }
            }

            if (_runtimeColliders.Count > 0)
            {
                return;
            }

            GetComponentsInChildren(includeInactive: false, _runtimeColliders);
        }

        private void RebuildAvailabilityRuleCacheIfNeeded()
        {
            if (_availabilityRules.Count == 0)
            {
                RebuildAvailabilityRuleCache();
            }
        }

        private void RebuildAvailabilityRuleCache()
        {
            _availabilityRules.Clear();
            _localPreviewDependencies = InteractionRuleDependencyFlags.None;

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IInteractionAvailabilityRule rule)
                {
                    _availabilityRules.Add(rule);

                    if (behaviours[i] is IInteractionAvailabilityDependencyProvider dependencyProvider)
                    {
                        _localPreviewDependencies |= dependencyProvider.LocalPreviewDependencies;
                    }
                    else
                    {
                        _localPreviewDependencies |= InteractionRuleDependencyFlags.Custom;
                    }
                }
            }

            EnsureLocalStateMirrorSubscriptions();
            MarkLocalPreviewDirty(InteractionRuleDependencyFlags.All, forceSelectorRefresh: false);
        }

        private void EnsureLocalStateMirrorSubscriptions()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            {
                UnsubscribeFromLocalStateMirrors();
                return;
            }

            if ((_localPreviewDependencies & (InteractionRuleDependencyFlags.Inventory | InteractionRuleDependencyFlags.Equipment)) != 0)
            {
                SubscribeInventoryState(ClientInventoryState.Local);
            }
            else
            {
                SubscribeInventoryState(null);
            }

            if ((_localPreviewDependencies & InteractionRuleDependencyFlags.ProgressFlags) != 0)
            {
                SubscribeProgressFlagState(ClientProgressFlagState.Local);
            }
            else
            {
                SubscribeProgressFlagState(null);
            }

            if ((_localPreviewDependencies & InteractionRuleDependencyFlags.Conditions) != 0)
            {
                SubscribeConditionState(NetworkPlayerConditionState.Local);
            }
            else
            {
                SubscribeConditionState(null);
            }
        }

        private void SubscribeInventoryState(ClientInventoryState state)
        {
            if (_subscribedInventoryState == state)
            {
                return;
            }

            if (_subscribedInventoryState != null)
            {
                _subscribedInventoryState.InventorySnapshotChanged -= HandleInventorySnapshotChanged;
            }

            _subscribedInventoryState = state;

            if (_subscribedInventoryState != null)
            {
                _subscribedInventoryState.InventorySnapshotChanged += HandleInventorySnapshotChanged;
                MarkLocalPreviewDirty(
                    InteractionRuleDependencyFlags.Inventory | InteractionRuleDependencyFlags.Equipment,
                    forceSelectorRefresh: false);
            }
        }

        private void SubscribeProgressFlagState(ClientProgressFlagState state)
        {
            if (_subscribedProgressFlagState == state)
            {
                return;
            }

            if (_subscribedProgressFlagState != null)
            {
                _subscribedProgressFlagState.FlagSet -= HandleProgressFlagSet;
                _subscribedProgressFlagState.FlagRemoved -= HandleProgressFlagRemoved;
                _subscribedProgressFlagState.PrefixCleared -= HandleProgressFlagPrefixCleared;
            }

            _subscribedProgressFlagState = state;

            if (_subscribedProgressFlagState != null)
            {
                _subscribedProgressFlagState.FlagSet += HandleProgressFlagSet;
                _subscribedProgressFlagState.FlagRemoved += HandleProgressFlagRemoved;
                _subscribedProgressFlagState.PrefixCleared += HandleProgressFlagPrefixCleared;
                MarkLocalPreviewDirty(InteractionRuleDependencyFlags.ProgressFlags, forceSelectorRefresh: false);
            }
        }

        private void SubscribeConditionState(NetworkPlayerConditionState state)
        {
            if (_subscribedConditionState == state)
            {
                return;
            }

            if (_subscribedConditionState != null)
            {
                _subscribedConditionState.ControlBlocks.OnValueChanged -= HandleControlBlocksChanged;
                _subscribedConditionState.IsAnchored.OnValueChanged -= HandleAnchoredChanged;
            }

            _subscribedConditionState = state;

            if (_subscribedConditionState != null)
            {
                _subscribedConditionState.ControlBlocks.OnValueChanged += HandleControlBlocksChanged;
                _subscribedConditionState.IsAnchored.OnValueChanged += HandleAnchoredChanged;
                MarkLocalPreviewDirty(InteractionRuleDependencyFlags.Conditions, forceSelectorRefresh: false);
            }
        }

        private void UnsubscribeFromLocalStateMirrors()
        {
            SubscribeInventoryState(null);
            SubscribeProgressFlagState(null);
            SubscribeConditionState(null);
        }

        private void HandleInventorySnapshotChanged()
        {
            MarkLocalPreviewDirty(
                InteractionRuleDependencyFlags.Inventory | InteractionRuleDependencyFlags.Equipment,
                forceSelectorRefresh: true);
        }

        private void HandleProgressFlagSet(string flagId, ProgressFlagLifetime lifetime)
        {
            MarkLocalPreviewDirty(InteractionRuleDependencyFlags.ProgressFlags, forceSelectorRefresh: true);
        }

        private void HandleProgressFlagRemoved(string flagId)
        {
            MarkLocalPreviewDirty(InteractionRuleDependencyFlags.ProgressFlags, forceSelectorRefresh: true);
        }

        private void HandleProgressFlagPrefixCleared(string prefix)
        {
            MarkLocalPreviewDirty(InteractionRuleDependencyFlags.ProgressFlags, forceSelectorRefresh: true);
        }

        private void HandleControlBlocksChanged(PlayerControlBlockFlags previousValue, PlayerControlBlockFlags newValue)
        {
            MarkLocalPreviewDirty(InteractionRuleDependencyFlags.Conditions, forceSelectorRefresh: true);
        }

        private void HandleAnchoredChanged(bool previousValue, bool newValue)
        {
            MarkLocalPreviewDirty(InteractionRuleDependencyFlags.Conditions, forceSelectorRefresh: true);
        }
    }
}
