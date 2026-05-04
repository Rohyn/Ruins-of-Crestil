using System.Collections.Generic;
using ROC.Game.Common;
using ROC.Networking.Characters;
using ROC.Networking.Conditions;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Game.Conditions
{
    [DisallowMultipleComponent]
    public sealed class AnchoringService : MonoBehaviour
    {
        public static AnchoringService Instance { get; private set; }

        [Header("Defaults")]
        [SerializeField] private string defaultExitPrompt = "Release";

        [Header("Behavior")]
        [Tooltip("If true, the server continuously keeps anchored players snapped to their anchor.")]
        [SerializeField] private bool enforceAnchorEveryFixedUpdate = true;

        [Tooltip("If the anchored actor drifts farther than this squared distance, it is snapped back.")]
        [SerializeField, Min(0f)] private float anchorSnapToleranceSqr = 0.0001f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private readonly Dictionary<ulong, ActiveAnchorState> _activeAnchorsByClient = new();

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

        private void FixedUpdate()
        {
            if (!enforceAnchorEveryFixedUpdate)
            {
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            foreach (ActiveAnchorState state in _activeAnchorsByClient.Values)
            {
                if (state.ActorObject == null || state.Anchor == null)
                {
                    continue;
                }

                float sqrDistance =
                    (state.ActorObject.transform.position - state.Anchor.position).sqrMagnitude;

                if (sqrDistance > anchorSnapToleranceSqr)
                {
                    SnapActorTo(state.ActorObject, state.Anchor, resetMotor: true);
                }
            }
        }

        public bool IsAnchored(ulong clientId)
        {
            return _activeAnchorsByClient.ContainsKey(clientId);
        }

        public ServerActionResult BeginAnchor(
            ulong clientId,
            NetworkObject actor,
            Transform anchor,
            Transform exitAnchor,
            string sourceId,
            string exitPrompt,
            string conditionIdToApply = "",
            ConditionSourceType conditionSourceType = ConditionSourceType.Interaction)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (actor == null)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidTarget,
                    "Cannot anchor because the actor is missing.");
            }

            if (anchor == null)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidTarget,
                    "Cannot anchor because the anchor transform is missing.");
            }

            if (_activeAnchorsByClient.ContainsKey(clientId))
            {
                return ServerActionResult.Ok("Client is already anchored.");
            }

            if (!string.IsNullOrWhiteSpace(conditionIdToApply))
            {
                if (ConditionService.Instance == null)
                {
                    return ServerActionResult.Fail(
                        ServerActionErrorCode.InvalidState,
                        "ConditionService is unavailable.");
                }

                ServerActionResult conditionResult =
                    ConditionService.Instance.ApplyConditionForClient(
                        clientId,
                        conditionIdToApply,
                        conditionSourceType,
                        sourceId);

                if (!conditionResult.Success)
                {
                    return conditionResult;
                }
            }

            Transform resolvedExitAnchor = exitAnchor != null ? exitAnchor : anchor;

            SnapActorTo(actor, anchor, resetMotor: true);

            var state = new ActiveAnchorState
            {
                ActorObject = actor,
                Anchor = anchor,
                ExitAnchor = resolvedExitAnchor,
                SourceId = sourceId ?? string.Empty,
                ExitPrompt = string.IsNullOrWhiteSpace(exitPrompt) ? defaultExitPrompt : exitPrompt,
                AppliedConditionId = conditionIdToApply ?? string.Empty
            };

            _activeAnchorsByClient[clientId] = state;

            if (actor.TryGetComponent(out NetworkPlayerConditionState conditionState))
            {
                conditionState.SetAnchorPresentationServer(true, state.ExitPrompt);
            }

            if (verboseLogging)
            {
                Debug.Log(
                    $"[AnchoringService] Client {clientId} anchored at '{anchor.name}'. " +
                    $"Source={state.SourceId}, Condition={state.AppliedConditionId}");
            }

            return ServerActionResult.Ok();
        }

        public ServerActionResult ReleaseAnchor(ulong clientId)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (!_activeAnchorsByClient.TryGetValue(clientId, out ActiveAnchorState state))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    "Client is not anchored.");
            }

            NetworkObject actor = state.ActorObject;

            if (actor == null)
            {
                _activeAnchorsByClient.Remove(clientId);

                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    "Anchored actor is missing.");
            }

            Transform exitAnchor = state.ExitAnchor != null ? state.ExitAnchor : actor.transform;

            SnapActorTo(actor, exitAnchor, resetMotor: true);

            if (!string.IsNullOrWhiteSpace(state.AppliedConditionId))
            {
                ConditionService.Instance?.RemoveConditionForClient(
                    clientId,
                    state.AppliedConditionId);
            }

            if (actor.TryGetComponent(out NetworkPlayerConditionState conditionState))
            {
                conditionState.SetAnchorPresentationServer(false, string.Empty);
            }

            _activeAnchorsByClient.Remove(clientId);

            if (verboseLogging)
            {
                Debug.Log($"[AnchoringService] Client {clientId} released anchor.");
            }

            return ServerActionResult.Ok();
        }

        public ServerActionResult ForceReleaseAnchorWithoutExitSnap(ulong clientId)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (!_activeAnchorsByClient.TryGetValue(clientId, out ActiveAnchorState state))
            {
                return ServerActionResult.Ok();
            }

            NetworkObject actor = state.ActorObject;

            if (!string.IsNullOrWhiteSpace(state.AppliedConditionId))
            {
                ConditionService.Instance?.RemoveConditionForClient(
                    clientId,
                    state.AppliedConditionId);
            }

            if (actor != null && actor.TryGetComponent(out NetworkPlayerConditionState conditionState))
            {
                conditionState.SetAnchorPresentationServer(false, string.Empty);
            }

            _activeAnchorsByClient.Remove(clientId);

            if (verboseLogging)
            {
                Debug.Log($"[AnchoringService] Client {clientId} force-released anchor.");
            }

            return ServerActionResult.Ok();
        }

        private static void SnapActorTo(
            NetworkObject actor,
            Transform target,
            bool resetMotor)
        {
            if (actor == null || target == null)
            {
                return;
            }

            CharacterController controller = actor.GetComponent<CharacterController>();

            if (controller != null)
            {
                controller.enabled = false;
            }

            actor.transform.SetPositionAndRotation(target.position, target.rotation);

            if (resetMotor && actor.TryGetComponent(out NetworkPlayerMotor motor))
            {
                motor.ClearServerMotionState();
            }

            if (controller != null)
            {
                controller.enabled = true;
            }
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
                    "Anchoring can only be changed by the server.");
                return false;
            }

            result = ServerActionResult.Ok();
            return true;
        }

        private sealed class ActiveAnchorState
        {
            public NetworkObject ActorObject;
            public Transform Anchor;
            public Transform ExitAnchor;
            public string SourceId;
            public string ExitPrompt;
            public string AppliedConditionId;
        }
    }
}