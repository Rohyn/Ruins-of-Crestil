using System;
using UnityEngine;

namespace ROC.Networking.World
{
    /// <summary>
    /// Owner-local cache of the currently streamed logical world scene/instance.
    /// This is presentation/client-preview state only; server session state remains authoritative.
    /// </summary>
    public static class ClientWorldSceneState
    {
        public static event Action StateChanged;

        public static string CurrentSceneId { get; private set; } = string.Empty;
        public static string CurrentInstanceId { get; private set; } = string.Empty;
        public static bool HasCurrentWorldScene => !string.IsNullOrWhiteSpace(CurrentSceneId);

        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            CurrentSceneId = string.Empty;
            CurrentInstanceId = string.Empty;
            StateChanged = null;
            _subscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SubscribeToStreamerEvents()
        {
            EnsureSubscribed();
        }

        public static void EnsureSubscribed()
        {
            if (_subscribed)
            {
                return;
            }

            ClientWorldSceneStreamer.LocalWorldSceneStreamStarted += HandleLocalWorldSceneStreamStarted;
            ClientWorldSceneStreamer.LocalWorldSceneReady += HandleLocalWorldSceneReady;
            ClientWorldSceneStreamer.LocalWorldSceneCleared += HandleLocalWorldSceneCleared;
            _subscribed = true;
        }

        public static void SetLocalWorldScene(string sceneId, string instanceId)
        {
            string normalizedSceneId = NormalizeId(sceneId);
            string normalizedInstanceId = NormalizeId(instanceId);

            if (string.Equals(CurrentSceneId, normalizedSceneId, StringComparison.Ordinal) &&
                string.Equals(CurrentInstanceId, normalizedInstanceId, StringComparison.Ordinal))
            {
                return;
            }

            CurrentSceneId = normalizedSceneId;
            CurrentInstanceId = normalizedInstanceId;
            StateChanged?.Invoke();
        }

        public static void ClearLocalWorldScene()
        {
            if (string.IsNullOrWhiteSpace(CurrentSceneId) && string.IsNullOrWhiteSpace(CurrentInstanceId))
            {
                return;
            }

            CurrentSceneId = string.Empty;
            CurrentInstanceId = string.Empty;
            StateChanged?.Invoke();
        }

        private static void HandleLocalWorldSceneStreamStarted(string sceneId, string instanceId)
        {
            // Treat the previous world scene as no longer safe for prompt evaluation while a transfer is in progress.
            ClearLocalWorldScene();
        }

        private static void HandleLocalWorldSceneReady(string sceneId, string instanceId)
        {
            SetLocalWorldScene(sceneId, instanceId);
        }

        private static void HandleLocalWorldSceneCleared()
        {
            ClearLocalWorldScene();
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
