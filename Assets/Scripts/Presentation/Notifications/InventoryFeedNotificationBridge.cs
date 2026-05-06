using System.Collections;
using System.Collections.Generic;
using ROC.Game.Inventory;
using ROC.Networking.Inventory;
using ROC.Presentation.Inventory;
using UnityEngine;

namespace ROC.Presentation.Notifications
{
    /// <summary>
    /// Owner-local bridge that turns ClientInventoryState snapshot deltas into lower-right feed notices.
    /// It suppresses inventory feed output while the inventory panel is visible and uses a short debounce
    /// so multi-item grants arrive as one batch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryFeedNotificationBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FeedNotificationController feedController;
        [SerializeField] private InventoryPanelController inventoryPanel;

        [Header("Display")]
        [SerializeField] private bool showItemGainNotices = true;
        [SerializeField] private bool showItemLossNotices = true;
        [SerializeField] private bool showEquipNotices = false;
        [SerializeField] private bool showUnequipNotices = false;

        [Header("Inventory Panel Suppression")]
        [Tooltip("If true, all inventory feed notices are suppressed while the Inventory tab is visible.")]
        [SerializeField] private bool suppressAllInventoryNoticesWhileInventoryPanelVisible = true;

        [Header("Timing")]
        [Tooltip("Short debounce lets multiple NetworkList changes from one server snapshot/grant settle before diffing.")]
        [SerializeField, Min(0f)] private float evaluationDelaySeconds = 0.08f;

        [Tooltip("Ignore the first non-empty inventory snapshot after binding so reconnect/snapshot restore does not look like item gain.")]
        [SerializeField] private bool suppressInitialSnapshot = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        private ClientInventoryState _inventory;
        private InventorySnapshot _lastSnapshot;
        private Coroutine _pendingEvaluationRoutine;
        private bool _hasBaseline;
        private bool _hasObservedSnapshotAfterBind;

        private void OnEnable()
        {
            ClientInventoryState.LocalInventoryReady += HandleLocalInventoryReady;
            TryBindDependencies();
        }

        private void OnDisable()
        {
            ClientInventoryState.LocalInventoryReady -= HandleLocalInventoryReady;
            UnbindInventory();

            if (_pendingEvaluationRoutine != null)
            {
                StopCoroutine(_pendingEvaluationRoutine);
                _pendingEvaluationRoutine = null;
            }
        }

        private void Update()
        {
            if (_inventory == null || feedController == null || inventoryPanel == null)
            {
                TryBindDependencies();
            }
        }

        private void HandleLocalInventoryReady(ClientInventoryState inventory)
        {
            BindInventory(inventory);
        }

        private void TryBindDependencies()
        {
            if (feedController == null)
            {
                feedController = FeedNotificationController.Local;
            }

            if (feedController == null)
            {
                feedController = FindFirstObjectByType<FeedNotificationController>();
            }

            if (inventoryPanel == null)
            {
                inventoryPanel = FindFirstObjectByType<InventoryPanelController>();
            }

            if (_inventory == null && ClientInventoryState.Local != null)
            {
                BindInventory(ClientInventoryState.Local);
            }
        }

        private void BindInventory(ClientInventoryState inventory)
        {
            if (inventory == null || _inventory == inventory)
            {
                return;
            }

            UnbindInventory();

            _inventory = inventory;
            _inventory.InventorySnapshotChanged += HandleInventorySnapshotChanged;
            _lastSnapshot = CaptureSnapshot(_inventory);
            _hasBaseline = !suppressInitialSnapshot;
            _hasObservedSnapshotAfterBind = false;

            if (!suppressInitialSnapshot)
            {
                _hasBaseline = true;
            }

            if (verboseLogging)
            {
                Debug.Log("[InventoryFeedNotificationBridge] Bound ClientInventoryState.", this);
            }
        }

        private void UnbindInventory()
        {
            if (_inventory != null)
            {
                _inventory.InventorySnapshotChanged -= HandleInventorySnapshotChanged;
                _inventory = null;
            }

            _lastSnapshot = null;
            _hasBaseline = false;
            _hasObservedSnapshotAfterBind = false;
        }

        private void HandleInventorySnapshotChanged()
        {
            if (_pendingEvaluationRoutine != null)
            {
                StopCoroutine(_pendingEvaluationRoutine);
            }

            _pendingEvaluationRoutine = StartCoroutine(EvaluateAfterDelayRoutine());
        }

        private IEnumerator EvaluateAfterDelayRoutine()
        {
            float delay = Mathf.Max(0f, evaluationDelaySeconds);

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
            else
            {
                yield return null;
            }

            _pendingEvaluationRoutine = null;
            EvaluateInventoryChanges();
        }

        private void EvaluateInventoryChanges()
        {
            if (_inventory == null)
            {
                return;
            }

            InventorySnapshot newSnapshot = CaptureSnapshot(_inventory);

            if (!_hasBaseline || _lastSnapshot == null)
            {
                _lastSnapshot = newSnapshot;
                _hasBaseline = true;
                _hasObservedSnapshotAfterBind = true;

                if (verboseLogging && suppressInitialSnapshot)
                {
                    Debug.Log("[InventoryFeedNotificationBridge] Captured baseline inventory snapshot without emitting notices.", this);
                }

                return;
            }

            bool suppressNow = IsInventoryNoticeSuppressed();

            if (suppressNow)
            {
                if (verboseLogging && HasAnyDifference(_lastSnapshot, newSnapshot))
                {
                    Debug.Log("[InventoryFeedNotificationBridge] Suppressed inventory feed diff because inventory panel is visible.", this);
                }

                _lastSnapshot = newSnapshot;
                _hasObservedSnapshotAfterBind = true;
                return;
            }

            EmitDiffNotices(_lastSnapshot, newSnapshot);
            _lastSnapshot = newSnapshot;
            _hasObservedSnapshotAfterBind = true;
        }

        private bool IsInventoryNoticeSuppressed()
        {
            if (!suppressAllInventoryNoticesWhileInventoryPanelVisible)
            {
                return false;
            }

            if (inventoryPanel == null)
            {
                inventoryPanel = FindFirstObjectByType<InventoryPanelController>();
            }

            return inventoryPanel != null && inventoryPanel.IsVisible();
        }

        private void EmitDiffNotices(InventorySnapshot oldSnapshot, InventorySnapshot newSnapshot)
        {
            if (feedController == null)
            {
                feedController = FeedNotificationController.Local;
            }

            if (feedController == null || oldSnapshot == null || newSnapshot == null)
            {
                return;
            }

            Dictionary<string, ItemDeltaAccumulator> deltas = BuildDeltas(oldSnapshot, newSnapshot);

            foreach (ItemDeltaAccumulator delta in deltas.Values)
            {
                if (delta == null)
                {
                    continue;
                }

                if (showItemGainNotices && delta.GainedQuantity > 0)
                {
                    feedController.EnqueueInventoryNotice(
                        FeedNotificationKind.ItemGained,
                        $"+{delta.GainedQuantity}",
                        delta.DisplayName,
                        "inventory.item_gain",
                        priority: 250);
                }

                if (showItemLossNotices && delta.LostQuantity > 0)
                {
                    feedController.EnqueueInventoryNotice(
                        FeedNotificationKind.ItemLost,
                        $"-{delta.LostQuantity}",
                        delta.DisplayName,
                        "inventory.item_loss",
                        priority: 225);
                }

                if (showEquipNotices && delta.EquippedQuantity > 0)
                {
                    feedController.EnqueueInventoryNotice(
                        FeedNotificationKind.ItemEquipped,
                        "Equipped",
                        FormatItem(delta.DisplayName, delta.EquippedQuantity),
                        "inventory.equip",
                        priority: 220);
                }

                if (showUnequipNotices && delta.UnequippedQuantity > 0)
                {
                    feedController.EnqueueInventoryNotice(
                        FeedNotificationKind.ItemUnequipped,
                        "Unequipped",
                        FormatItem(delta.DisplayName, delta.UnequippedQuantity),
                        "inventory.unequip",
                        priority: 210);
                }
            }
        }

        private static Dictionary<string, ItemDeltaAccumulator> BuildDeltas(
            InventorySnapshot oldSnapshot,
            InventorySnapshot newSnapshot)
        {
            Dictionary<string, ItemDeltaAccumulator> deltas = new();
            HashSet<string> instanceIds = new();

            foreach (string instanceId in oldSnapshot.InstanceIds)
            {
                instanceIds.Add(instanceId);
            }

            foreach (string instanceId in newSnapshot.InstanceIds)
            {
                instanceIds.Add(instanceId);
            }

            foreach (string instanceId in instanceIds)
            {
                InventoryObservedItem oldItem = oldSnapshot.Get(instanceId);
                InventoryObservedItem newItem = newSnapshot.Get(instanceId);

                if (oldItem == null && newItem == null)
                {
                    continue;
                }

                InventoryObservedItem reference = newItem ?? oldItem;
                string definitionId = string.IsNullOrWhiteSpace(reference.DefinitionId)
                    ? reference.ItemInstanceId
                    : reference.DefinitionId;

                ItemDeltaAccumulator delta = GetOrCreateDelta(deltas, definitionId, reference.DisplayName);

                if (oldItem == null && newItem != null)
                {
                    delta.GainedQuantity += Mathf.Max(0, newItem.Quantity);
                    continue;
                }

                if (oldItem != null && newItem == null)
                {
                    delta.LostQuantity += Mathf.Max(0, oldItem.Quantity);
                    continue;
                }

                if (oldItem == null || newItem == null)
                {
                    continue;
                }

                if (oldItem.Location != newItem.Location)
                {
                    int movedQuantity = Mathf.Max(0, Mathf.Min(oldItem.Quantity, newItem.Quantity));

                    if (oldItem.Location == InventoryLocationKind.Bag && newItem.Location == InventoryLocationKind.Equipped)
                    {
                        delta.EquippedQuantity += movedQuantity;
                    }
                    else if (oldItem.Location == InventoryLocationKind.Equipped && newItem.Location == InventoryLocationKind.Bag)
                    {
                        delta.UnequippedQuantity += movedQuantity;
                    }
                }

                int quantityDelta = newItem.Quantity - oldItem.Quantity;

                if (quantityDelta > 0)
                {
                    delta.GainedQuantity += quantityDelta;
                }
                else if (quantityDelta < 0)
                {
                    delta.LostQuantity += -quantityDelta;
                }
            }

            return deltas;
        }

        private static ItemDeltaAccumulator GetOrCreateDelta(
            Dictionary<string, ItemDeltaAccumulator> deltas,
            string definitionId,
            string displayName)
        {
            string key = string.IsNullOrWhiteSpace(definitionId) ? displayName : definitionId.Trim();

            if (!deltas.TryGetValue(key, out ItemDeltaAccumulator delta) || delta == null)
            {
                delta = new ItemDeltaAccumulator(
                    key,
                    string.IsNullOrWhiteSpace(displayName) ? key : displayName.Trim());
                deltas[key] = delta;
            }
            else if (string.IsNullOrWhiteSpace(delta.DisplayName) && !string.IsNullOrWhiteSpace(displayName))
            {
                delta.DisplayName = displayName.Trim();
            }

            return delta;
        }

        private static InventorySnapshot CaptureSnapshot(ClientInventoryState inventory)
        {
            InventorySnapshot snapshot = new();

            if (inventory == null || inventory.Items == null)
            {
                return snapshot;
            }

            for (int i = 0; i < inventory.Items.Count; i++)
            {
                InventoryItemSnapshot item = inventory.Items[i];
                string instanceId = item.ItemInstanceId.ToString();

                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    continue;
                }

                snapshot.Set(new InventoryObservedItem(
                    instanceId.Trim(),
                    item.DefinitionId.ToString(),
                    item.DisplayName.ToString(),
                    item.Quantity,
                    item.Location));
            }

            return snapshot;
        }

        private static bool HasAnyDifference(InventorySnapshot oldSnapshot, InventorySnapshot newSnapshot)
        {
            if (oldSnapshot == null || newSnapshot == null)
            {
                return false;
            }

            if (oldSnapshot.Count != newSnapshot.Count)
            {
                return true;
            }

            foreach (string instanceId in oldSnapshot.InstanceIds)
            {
                InventoryObservedItem oldItem = oldSnapshot.Get(instanceId);
                InventoryObservedItem newItem = newSnapshot.Get(instanceId);

                if (oldItem == null || newItem == null || !oldItem.EqualsState(newItem))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatItem(string displayName, int quantity)
        {
            if (quantity <= 1)
            {
                return displayName;
            }

            return $"{displayName} x{quantity}";
        }

        private sealed class InventorySnapshot
        {
            private readonly Dictionary<string, InventoryObservedItem> _itemsByInstanceId = new();

            public IEnumerable<string> InstanceIds => _itemsByInstanceId.Keys;
            public int Count => _itemsByInstanceId.Count;

            public InventoryObservedItem Get(string instanceId)
            {
                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    return null;
                }

                _itemsByInstanceId.TryGetValue(instanceId.Trim(), out InventoryObservedItem item);
                return item;
            }

            public void Set(InventoryObservedItem item)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemInstanceId))
                {
                    return;
                }

                _itemsByInstanceId[item.ItemInstanceId.Trim()] = item;
            }
        }

        private sealed class InventoryObservedItem
        {
            public string ItemInstanceId { get; }
            public string DefinitionId { get; }
            public string DisplayName { get; }
            public int Quantity { get; }
            public InventoryLocationKind Location { get; }

            public InventoryObservedItem(
                string itemInstanceId,
                string definitionId,
                string displayName,
                int quantity,
                InventoryLocationKind location)
            {
                ItemInstanceId = itemInstanceId ?? string.Empty;
                DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? ItemInstanceId : definitionId.Trim();
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? DefinitionId : displayName.Trim();
                Quantity = Mathf.Max(0, quantity);
                Location = location;
            }

            public bool EqualsState(InventoryObservedItem other)
            {
                return other != null &&
                       ItemInstanceId == other.ItemInstanceId &&
                       DefinitionId == other.DefinitionId &&
                       DisplayName == other.DisplayName &&
                       Quantity == other.Quantity &&
                       Location == other.Location;
            }
        }

        private sealed class ItemDeltaAccumulator
        {
            public string DefinitionId { get; }
            public string DisplayName { get; set; }
            public int GainedQuantity { get; set; }
            public int LostQuantity { get; set; }
            public int EquippedQuantity { get; set; }
            public int UnequippedQuantity { get; set; }

            public ItemDeltaAccumulator(string definitionId, string displayName)
            {
                DefinitionId = definitionId ?? string.Empty;
                DisplayName = displayName ?? DefinitionId;
            }
        }
    }
}
