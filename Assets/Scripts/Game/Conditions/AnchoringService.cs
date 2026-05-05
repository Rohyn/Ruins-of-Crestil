using System.Collections.Generic;
using ROC.Game.Common;
using ROC.Networking.Characters;
using ROC.Networking.Conditions;
using ROC.Networking.Sessions;
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
        [SerializeField] private bool enforceAnchorEveryFixedUpdate = true;
        [SerializeField, Min(0f)] private float anchorSnapToleranceSqr = 0.0001f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private readonly Dictionary<ulong, ActiveAnchorState> _activeAnchorsByClient = new();
        private readonly List<ulong> _cleanupBuffer = new();

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

            _cleanupBuffer.Clear();

            foreach (KeyValuePair<ulong, ActiveAnchorState> pair in _activeAnchorsByClient)
            {
                ulong clientId = pair.Key;
                ActiveAnchorState state = pair.Value;

                if (state.ActorObject == null || !state.ActorObject.IsSpawned)
                {
                    _cleanupBuffer.Add(clientId);
                    continue;
                }

                if (state.Anchor == null)
                {
                    _cleanupBuffer.Add(clientId);
                    continue;
                }

                float sqrDistance =
                    (state.ActorObject.transform.position - state.Anchor.position).sqrMagnitude;

                if (sqrDistance > anchorSnapToleranceSqr)
                {
                    SnapActorTo(state.ActorObject, state.Anchor, resetMotor: true);
                }
            }

            for (int i = 0; i < _cleanupBuffer.Count; i++)
            {
                ForceReleaseAnchorWithoutExitSnap(_cleanupBuffer[i]);
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

            string characterId = string.Empty;
            PlayerSessionRegistry.Instance?.TryGetCharacterId(clientId, out characterId);

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
                ClientId = clientId,
                CharacterId = characterId ?? string.Empty,
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
            return ReleaseAnchorInternal(
                clientId,
                snapToExitAnchor: true,
                clearPresentation: true,
                reason: "release");
        }

        public ServerActionResult ForceReleaseAnchorWithoutExitSnap(ulong clientId)
        {
            return ReleaseAnchorInternal(
                clientId,
                snapToExitAnchor: false,
                clearPresentation: true,
                reason: "force-release");
        }

        public ServerActionResult CleanupClientForDisconnect(ulong clientId)
        {
            // For logout/disconnect, snap to the exit anchor so the saved location is safe.
            return ReleaseAnchorInternal(
                clientId,
                snapToExitAnchor: true,
                clearPresentation: true,
                reason: "disconnect");
        }

        public ServerActionResult CleanupClientForWorldTransfer(ulong clientId)
        {
            // For scene transfer, do not snap to an old-scene exit anchor. The transfer spawn wins.
            return ReleaseAnchorInternal(
                clientId,
                snapToExitAnchor: false,
                clearPresentation: true,
                reason: "world-transfer");
        }

        private ServerActionResult ReleaseAnchorInternal(
            ulong clientId,
            bool snapToExitAnchor,
            bool clearPresentation,
            string reason)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (!_activeAnchorsByClient.TryGetValue(clientId, out ActiveAnchorState state))
            {
                return ServerActionResult.Ok("Client is not anchored.");
            }

            NetworkObject actor = state.ActorObject;

            if (actor != null && actor.IsSpawned)
            {
                if (snapToExitAnchor)
                {
                    Transform exitAnchor = state.ExitAnchor != null ? state.ExitAnchor : actor.transform;
                    SnapActorTo(actor, exitAnchor, resetMotor: true);
                }
                else if (actor.TryGetComponent(out NetworkPlayerMotor motor))
                {
                    motor.ClearServerMotionState();
                }

                if (clearPresentation &&
                    actor.TryGetComponent(out NetworkPlayerConditionState conditionState))
                {
                    conditionState.SetAnchorPresentationServer(false, string.Empty);
                }
            }

            RemoveAppliedCondition(clientId, state);

            _activeAnchorsByClient.Remove(clientId);

            if (verboseLogging)
            {
                Debug.Log($"[AnchoringService] Client {clientId} anchor cleanup completed. Reason={reason}");
            }

            return ServerActionResult.Ok();
        }

        private static void RemoveAppliedCondition(ulong clientId, ActiveAnchorState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.AppliedConditionId))
            {
                return;
            }

            ConditionService conditionService = ConditionService.Instance;

            if (conditionService == null)
            {
                return;
            }

            ServerActionResult result = conditionService.RemoveConditionForClient(
                clientId,
                state.AppliedConditionId);

            if (result.Success)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(state.CharacterId))
            {
                conditionService.RemoveConditionForCharacter(
                    state.CharacterId,
                    state.AppliedConditionId);
            }
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
            public ulong ClientId;
            public string CharacterId;
            public NetworkObject ActorObject;
            public Transform Anchor;
            public Transform ExitAnchor;
            public string SourceId;
            public string ExitPrompt;
            public string AppliedConditionId;
        }
    }
}