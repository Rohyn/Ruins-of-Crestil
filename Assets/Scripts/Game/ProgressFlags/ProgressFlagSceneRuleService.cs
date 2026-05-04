using System.Collections.Generic;
using ROC.Game.Common;
using ROC.Networking.Sessions;
using UnityEngine;

namespace ROC.Game.ProgressFlags
{
    [DisallowMultipleComponent]
    public sealed class ProgressFlagSceneRuleService : MonoBehaviour
    {
        [SerializeField] private ProgressFlagSceneRule[] rules;

        private readonly Dictionary<ulong, string> _lastSceneByClient = new();

        private void OnEnable()
        {
            if (PlayerSessionRegistry.Instance != null)
            {
                PlayerSessionRegistry.Instance.SessionChanged += HandleSessionChanged;
                PlayerSessionRegistry.Instance.SessionRemoved += HandleSessionRemoved;
            }
        }

        private void OnDisable()
        {
            if (PlayerSessionRegistry.Instance != null)
            {
                PlayerSessionRegistry.Instance.SessionChanged -= HandleSessionChanged;
                PlayerSessionRegistry.Instance.SessionRemoved -= HandleSessionRemoved;
            }
        }

        private void HandleSessionChanged(PlayerSessionData session)
        {
            if (session == null)
            {
                return;
            }

            if (session.State != PlayerSessionState.InWorld)
            {
                return;
            }

            string currentSceneId = session.SceneId ?? string.Empty;

            if (string.IsNullOrWhiteSpace(currentSceneId))
            {
                return;
            }

            _lastSceneByClient.TryGetValue(session.ClientId, out string previousSceneId);

            if (previousSceneId == currentSceneId)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(previousSceneId))
            {
                ApplyRules(session.ClientId, previousSceneId, ProgressFlagSceneEventType.LeaveScene);
            }

            ApplyRules(session.ClientId, currentSceneId, ProgressFlagSceneEventType.EnterScene);

            _lastSceneByClient[session.ClientId] = currentSceneId;
        }

        private void HandleSessionRemoved(ulong clientId)
        {
            if (_lastSceneByClient.TryGetValue(clientId, out string previousSceneId))
            {
                ApplyRules(clientId, previousSceneId, ProgressFlagSceneEventType.LeaveScene);
            }

            _lastSceneByClient.Remove(clientId);
        }

        private void ApplyRules(
            ulong clientId,
            string sceneId,
            ProgressFlagSceneEventType eventType)
        {
            ProgressFlagService service = ProgressFlagService.Instance;

            if (service == null || rules == null)
            {
                return;
            }

            for (int i = 0; i < rules.Length; i++)
            {
                ProgressFlagSceneRule rule = rules[i];

                if (rule.EventType != eventType || rule.SceneId != sceneId)
                {
                    continue;
                }

                ServerActionResult requirements =
                    service.EvaluateRequirementsForClient(clientId, rule.Requirements);

                if (!requirements.Success)
                {
                    continue;
                }

                ServerActionResult mutations =
                    service.ApplyMutationsForClient(clientId, rule.Mutations);

                if (!mutations.Success)
                {
                    Debug.LogWarning($"[ProgressFlagSceneRuleService] Rule failed: {mutations}", this);
                }
            }
        }
    }
}