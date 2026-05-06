using System;

namespace ROC.Presentation.Notifications
{
    [Serializable]
    public sealed class FeedNotificationEntry
    {
        public FeedNotificationChannel Channel { get; }
        public FeedNotificationKind Kind { get; }
        public string Title { get; }
        public string Body { get; }
        public string BatchKey { get; }
        public int Priority { get; }
        public float DisplaySeconds { get; }
        public float QueuedRealtime { get; }

        public FeedNotificationEntry(
            FeedNotificationChannel channel,
            FeedNotificationKind kind,
            string title,
            string body,
            string batchKey,
            int priority,
            float displaySeconds,
            float queuedRealtime)
        {
            Channel = channel;
            Kind = kind;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            BatchKey = batchKey ?? string.Empty;
            Priority = priority;
            DisplaySeconds = displaySeconds;
            QueuedRealtime = queuedRealtime;
        }
    }
}
