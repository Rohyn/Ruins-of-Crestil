using System.Collections;
using System.Collections.Generic;
using System.Text;
using ROC.Presentation.Inventory;
using UnityEngine;

namespace ROC.Presentation.Notifications
{
    /// <summary>
    /// Owner-local lower-right notification feed for small deltas such as item gains/losses.
    /// This is presentation-only. Bridges observe replicated client state and enqueue entries here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FeedNotificationController : MonoBehaviour
    {
        public static FeedNotificationController Local { get; private set; }

        [Header("Views")]
        [SerializeField] private FeedNotificationToastView inventoryToastView;
        [SerializeField] private FeedNotificationToastView generalToastView;

        [Header("Defaults")]
        [SerializeField, Min(0.25f)] private float defaultInventoryDisplaySeconds = 2.25f;
        [SerializeField, Min(0.25f)] private float defaultGeneralDisplaySeconds = 2.5f;
        [SerializeField, Min(0f)] private float secondsBetweenNotifications = 0.15f;

        [Header("Inventory Batching")]
        [SerializeField] private bool batchInventoryNotices = true;
        [SerializeField, Min(0f)] private float inventoryBatchWindowSeconds = 0.18f;
        [SerializeField, Min(1)] private int maxInventoryLinesPerToast = 6;
        [SerializeField] private string inventoryBatchTitle = "Inventory";
        [SerializeField] private bool preserveSingleInventoryNoticeFormatting = false;

        [Header("Inventory Suppression")]
        [Tooltip("If true, inventory feed notices are discarded while the inventory panel is visible.")]
        [SerializeField] private bool suppressInventoryFeedWhileInventoryPanelOpen = true;
        [SerializeField] private InventoryPanelController inventoryPanel;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        private readonly Dictionary<FeedNotificationChannel, List<FeedNotificationEntry>> _queuesByChannel = new();
        private readonly Dictionary<FeedNotificationChannel, Coroutine> _routinesByChannel = new();

        private void Awake()
        {
            if (Local != null && Local != this)
            {
                Debug.LogWarning("[FeedNotificationController] Multiple local feed notification controllers found. Using the most recently enabled instance.", this);
            }

            Local = this;
            ResolveReferences();
        }

        private void OnEnable()
        {
            Local = this;
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (Local == this)
            {
                Local = null;
            }
        }

        private void ResolveReferences()
        {
            if (inventoryToastView == null)
            {
                inventoryToastView = GetComponentInChildren<FeedNotificationToastView>(true);
            }

            if (generalToastView == null)
            {
                generalToastView = inventoryToastView;
            }

            if (inventoryPanel == null)
            {
                inventoryPanel = FindFirstObjectByType<InventoryPanelController>();
            }
        }

        public void EnqueueInventoryNotice(
            FeedNotificationKind kind,
            string title,
            string body,
            string batchKey = "inventory",
            int priority = 250,
            float displaySeconds = -1f)
        {
            Enqueue(
                FeedNotificationChannel.Inventory,
                kind,
                title,
                body,
                string.IsNullOrWhiteSpace(batchKey) ? "inventory" : batchKey,
                priority,
                displaySeconds);
        }

        public void EnqueueGeneralNotice(
            string title,
            string body,
            int priority = 100,
            float displaySeconds = -1f)
        {
            Enqueue(
                FeedNotificationChannel.General,
                FeedNotificationKind.Generic,
                title,
                body,
                "general",
                priority,
                displaySeconds);
        }

        public void Enqueue(
            FeedNotificationChannel channel,
            FeedNotificationKind kind,
            string title,
            string body,
            string batchKey,
            int priority = 100,
            float displaySeconds = -1f)
        {
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            ResolveReferences();

            if (channel == FeedNotificationChannel.Inventory && IsInventoryFeedSuppressed())
            {
                ClearQueue(FeedNotificationChannel.Inventory);

                if (verboseLogging)
                {
                    Debug.Log($"[FeedNotificationController] Suppressed inventory feed notice while inventory panel is open: {title} {body}", this);
                }

                return;
            }

            FeedNotificationToastView view = GetView(channel);

            if (view == null)
            {
                if (verboseLogging)
                {
                    Debug.LogWarning($"[FeedNotificationController] No feed toast view assigned for channel '{channel}'.", this);
                }

                return;
            }

            float resolvedDisplaySeconds = displaySeconds > 0f
                ? displaySeconds
                : GetDefaultDisplaySeconds(channel);

            List<FeedNotificationEntry> queue = GetOrCreateQueue(channel);
            queue.Add(new FeedNotificationEntry(
                channel,
                kind,
                title,
                body,
                batchKey,
                priority,
                resolvedDisplaySeconds,
                Time.realtimeSinceStartup));

            queue.Sort(CompareEntries);

            if (!_routinesByChannel.TryGetValue(channel, out Coroutine routine) || routine == null)
            {
                _routinesByChannel[channel] = channel == FeedNotificationChannel.Inventory && batchInventoryNotices
                    ? StartCoroutine(DisplayBatchedInventoryRoutine(channel))
                    : StartCoroutine(DisplayRoutine(channel));
            }

            if (verboseLogging)
            {
                Debug.Log($"[FeedNotificationController] Queued {channel} notice: {title} {body}", this);
            }
        }

        public bool IsInventoryFeedSuppressed()
        {
            if (!suppressInventoryFeedWhileInventoryPanelOpen)
            {
                return false;
            }

            if (inventoryPanel == null)
            {
                inventoryPanel = FindFirstObjectByType<InventoryPanelController>();
            }

            return inventoryPanel != null && inventoryPanel.IsVisible();
        }

        public void ClearQueue(FeedNotificationChannel channel)
        {
            if (_queuesByChannel.TryGetValue(channel, out List<FeedNotificationEntry> queue))
            {
                queue.Clear();
            }
        }

        private IEnumerator DisplayRoutine(FeedNotificationChannel channel)
        {
            FeedNotificationToastView view = GetView(channel);

            if (view == null)
            {
                _routinesByChannel[channel] = null;
                yield break;
            }

            while (_queuesByChannel.TryGetValue(channel, out List<FeedNotificationEntry> queue) &&
                   queue != null &&
                   queue.Count > 0)
            {
                if (channel == FeedNotificationChannel.Inventory && IsInventoryFeedSuppressed())
                {
                    queue.Clear();
                    view.Hide();
                    break;
                }

                FeedNotificationEntry entry = queue[0];
                queue.RemoveAt(0);

                view.Show(entry.Title, entry.Body);

                yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, entry.DisplaySeconds));

                view.Hide();

                if (secondsBetweenNotifications > 0f)
                {
                    yield return new WaitForSecondsRealtime(secondsBetweenNotifications);
                }
            }

            _routinesByChannel[channel] = null;
        }

        private IEnumerator DisplayBatchedInventoryRoutine(FeedNotificationChannel channel)
        {
            FeedNotificationToastView view = GetView(channel);

            if (view == null)
            {
                _routinesByChannel[channel] = null;
                yield break;
            }

            while (_queuesByChannel.TryGetValue(channel, out List<FeedNotificationEntry> queue) &&
                   queue != null &&
                   queue.Count > 0)
            {
                float batchDelay = Mathf.Max(0f, inventoryBatchWindowSeconds);

                if (batchDelay > 0f)
                {
                    yield return new WaitForSecondsRealtime(batchDelay);
                }
                else
                {
                    yield return null;
                }

                if (queue.Count <= 0)
                {
                    continue;
                }

                if (IsInventoryFeedSuppressed())
                {
                    queue.Clear();
                    view.Hide();
                    break;
                }

                int maxLines = Mathf.Max(1, maxInventoryLinesPerToast);
                int batchCount = Mathf.Min(maxLines, queue.Count);
                List<FeedNotificationEntry> batch = new(batchCount);

                for (int i = 0; i < batchCount; i++)
                {
                    FeedNotificationEntry entry = queue[0];
                    queue.RemoveAt(0);
                    batch.Add(entry);
                }

                if (batch.Count == 1 && preserveSingleInventoryNoticeFormatting)
                {
                    FeedNotificationEntry single = batch[0];
                    view.Show(single.Title, single.Body);
                    yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, single.DisplaySeconds));
                }
                else
                {
                    view.Show(
                        string.IsNullOrWhiteSpace(inventoryBatchTitle) ? "Inventory" : inventoryBatchTitle,
                        BuildBatchBody(batch, queue.Count));

                    yield return new WaitForSecondsRealtime(GetBatchDisplaySeconds(batch));
                }

                view.Hide();

                if (secondsBetweenNotifications > 0f)
                {
                    yield return new WaitForSecondsRealtime(secondsBetweenNotifications);
                }
            }

            _routinesByChannel[channel] = null;
        }

        private static string BuildBatchBody(List<FeedNotificationEntry> batch, int remainingQueuedCount)
        {
            StringBuilder builder = new();

            for (int i = 0; i < batch.Count; i++)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(FormatLine(batch[i]));
            }

            if (remainingQueuedCount > 0)
            {
                builder.AppendLine();
                builder.Append($"+{remainingQueuedCount} more...");
            }

            return builder.ToString();
        }

        private static string FormatLine(FeedNotificationEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(entry.Title))
            {
                return entry.Body;
            }

            if (string.IsNullOrWhiteSpace(entry.Body))
            {
                return entry.Title;
            }

            if (entry.Title.StartsWith("+") || entry.Title.StartsWith("-") || entry.Title.StartsWith("×"))
            {
                return $"{entry.Title} {entry.Body}";
            }

            return $"{entry.Title}: {entry.Body}";
        }

        private static float GetBatchDisplaySeconds(List<FeedNotificationEntry> batch)
        {
            if (batch == null || batch.Count <= 0)
            {
                return 0.25f;
            }

            float longest = 0.25f;

            for (int i = 0; i < batch.Count; i++)
            {
                if (batch[i] != null)
                {
                    longest = Mathf.Max(longest, batch[i].DisplaySeconds);
                }
            }

            return Mathf.Max(0.25f, longest + ((batch.Count - 1) * 0.25f));
        }

        private List<FeedNotificationEntry> GetOrCreateQueue(FeedNotificationChannel channel)
        {
            if (!_queuesByChannel.TryGetValue(channel, out List<FeedNotificationEntry> queue) || queue == null)
            {
                queue = new List<FeedNotificationEntry>();
                _queuesByChannel[channel] = queue;
            }

            return queue;
        }

        private FeedNotificationToastView GetView(FeedNotificationChannel channel)
        {
            switch (channel)
            {
                case FeedNotificationChannel.Inventory:
                    return inventoryToastView != null ? inventoryToastView : generalToastView;

                default:
                    return generalToastView != null ? generalToastView : inventoryToastView;
            }
        }

        private float GetDefaultDisplaySeconds(FeedNotificationChannel channel)
        {
            return channel == FeedNotificationChannel.Inventory
                ? Mathf.Max(0.25f, defaultInventoryDisplaySeconds)
                : Mathf.Max(0.25f, defaultGeneralDisplaySeconds);
        }

        private static int CompareEntries(FeedNotificationEntry a, FeedNotificationEntry b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int priorityCompare = b.Priority.CompareTo(a.Priority);

            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            return a.QueuedRealtime.CompareTo(b.QueuedRealtime);
        }
    }
}
