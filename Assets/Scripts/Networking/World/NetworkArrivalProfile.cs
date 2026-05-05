using System.Collections.Generic;
using ROC.Game.Common;
using ROC.Game.Conditions;
using ROC.Game.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.World
{
    [DisallowMultipleComponent]
    public sealed class NetworkArrivalProfile : MonoBehaviour
    {
        [Header("Eligibility")]
        [Tooltip("Arrival reasons that are allowed to use this profile. For the intro bed, select Initial Character Spawn only.")]
        [SerializeField] private WorldArrivalReasonFlags allowedArrivalReasons = WorldArrivalReasonFlags.InitialCharacterSpawn;

        [Header("Anchor Resolution")]
        [Tooltip("Direct Transforms uses scene-assigned Transform references. Spawned Instance Object resolves named anchors from a spawned network prefab by stable object ID.")]
        [SerializeField] private ArrivalAnchorResolutionMode anchorResolutionMode = ArrivalAnchorResolutionMode.DirectTransforms;

        [Tooltip("StableObjectId assigned on the InstanceNetworkObjectSpawnMarker for the spawned network object that owns the anchors.")]
        [SerializeField] private string targetStableObjectId;

        [Tooltip("Named anchor ID on the target object's NetworkArrivalAnchorProvider used for initial placement.")]
        [SerializeField] private string placementAnchorId = "rest";

        [Tooltip("Named anchor ID on the target object's NetworkArrivalAnchorProvider used by AnchoringService. If empty, Placement Anchor Id is used.")]
        [SerializeField] private string anchorId = "rest";

        [Tooltip("Optional named anchor ID on the target object's NetworkArrivalAnchorProvider used when the actor releases the anchor.")]
        [SerializeField] private string exitAnchorId = "exit";

        [Header("Direct Transform References")]
        [Tooltip("Direct Transform mode only. Transform used for initial placement.")]
        [SerializeField] private Transform placementAnchor;

        [Tooltip("Direct Transform mode only. Anchor transform used by AnchoringService. If empty, Placement Anchor is used.")]
        [SerializeField] private Transform anchor;

        [Tooltip("Direct Transform mode only. Optional exit transform used when the player releases the anchor.")]
        [SerializeField] private Transform exitAnchor;

        [Header("Initial Placement")]
        [Tooltip("If true, the avatar is initially instantiated at the resolved placement anchor instead of the spawn point transform.")]
        [SerializeField] private bool overrideSpawnTransformWithPlacementAnchor = true;

        [Tooltip("If true, the avatar initially uses the resolved placement anchor's rotation.")]
        [SerializeField] private bool usePlacementAnchorRotation = true;

        [Header("Anchor On Arrival")]
        [Tooltip("If true, AnchoringService.BeginAnchor is called after the avatar is spawned and registered in the session registry.")]
        [SerializeField] private bool beginAnchorOnArrival = true;

        [Tooltip("Source ID recorded by the anchor and any applied condition.")]
        [SerializeField] private string anchorSourceId = "arrival";

        [Tooltip("Prompt text mirrored by NetworkPlayerConditionState while anchored.")]
        [SerializeField] private string anchorExitPrompt = "Press E to get up";

        [Header("Condition On Arrival")]
        [Tooltip("If true and Begin Anchor On Arrival is true, AnchoringService applies the condition and removes it when the anchor is released.")]
        [SerializeField] private bool applyConditionThroughAnchor = true;

        [Tooltip("If true and Begin Anchor On Arrival is false, ConditionService applies the condition directly.")]
        [SerializeField] private bool applyConditionWithoutAnchor = false;

        [Tooltip("Stable condition ID. For the intro bed, use condition.resting.")]
        [SerializeField] private string conditionIdToApply = "condition.resting";

        [SerializeField] private ConditionSourceType conditionSourceType = ConditionSourceType.System;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private readonly List<NetworkArrivalAnchorProvider> _providerBuffer = new();

        public WorldArrivalReasonFlags AllowedArrivalReasons => allowedArrivalReasons;
        public ArrivalAnchorResolutionMode AnchorResolutionMode => anchorResolutionMode;
        public string TargetStableObjectId => targetStableObjectId;
        public string PlacementAnchorId => placementAnchorId;
        public string AnchorId => anchorId;
        public string ExitAnchorId => exitAnchorId;

        public bool ShouldApply(WorldArrivalReason reason)
        {
            WorldArrivalReasonFlags flag = reason.ToFlag();
            return flag != WorldArrivalReasonFlags.None && (allowedArrivalReasons & flag) != 0;
        }

        public bool TryResolveSpawnPose(
            WorldInstance instance,
            Vector3 defaultPosition,
            Quaternion defaultRotation,
            WorldArrivalReason reason,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = defaultPosition;
            rotation = defaultRotation;

            if (!ShouldApply(reason))
            {
                return false;
            }

            if (!overrideSpawnTransformWithPlacementAnchor)
            {
                return false;
            }

            if (!TryResolvePlacementAnchor(instance, out Transform resolvedPlacementAnchor, out string error))
            {
                if (verboseLogging)
                {
                    Debug.LogWarning($"[NetworkArrivalProfile] Could not resolve placement anchor for '{name}': {error}");
                }

                return false;
            }

            position = resolvedPlacementAnchor.position;

            if (usePlacementAnchorRotation)
            {
                rotation = resolvedPlacementAnchor.rotation;
            }

            return true;
        }

        public bool TryResolveSpawnPose(
            Vector3 defaultPosition,
            Quaternion defaultRotation,
            WorldArrivalReason reason,
            out Vector3 position,
            out Quaternion rotation)
        {
            return TryResolveSpawnPose(null, defaultPosition, defaultRotation, reason, out position, out rotation);
        }

        public ServerActionResult ApplyPostSpawn(
            ulong clientId,
            NetworkObject actor,
            WorldInstance instance,
            WorldArrivalReason reason)
        {
            if (!ShouldApply(reason))
            {
                return ServerActionResult.Ok("Arrival profile does not apply for this arrival reason.");
            }

            if (actor == null)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidTarget,
                    "Cannot apply arrival profile because the actor is missing.");
            }

            ServerActionResult result = ServerActionResult.Ok();

            if (beginAnchorOnArrival)
            {
                if (!TryResolveAnchor(instance, out Transform resolvedAnchor, out string anchorError))
                {
                    return ServerActionResult.Fail(
                        ServerActionErrorCode.InvalidTarget,
                        $"Cannot anchor on arrival because no anchor transform could be resolved. {anchorError}");
                }

                if (!TryResolveExitAnchor(instance, out Transform resolvedExitAnchor, out string exitError))
                {
                    return ServerActionResult.Fail(
                        ServerActionErrorCode.InvalidTarget,
                        $"Cannot anchor on arrival because the configured exit anchor could not be resolved. {exitError}");
                }

                if (AnchoringService.Instance == null)
                {
                    return ServerActionResult.Fail(
                        ServerActionErrorCode.InvalidState,
                        "Cannot anchor on arrival because AnchoringService is unavailable.");
                }

                string conditionForAnchor =
                    applyConditionThroughAnchor
                        ? conditionIdToApply
                        : string.Empty;

                result = AnchoringService.Instance.BeginAnchor(
                    clientId,
                    actor,
                    resolvedAnchor,
                    resolvedExitAnchor,
                    anchorSourceId,
                    anchorExitPrompt,
                    conditionForAnchor,
                    conditionSourceType);

                if (!result.Success)
                {
                    return result;
                }
            }
            else if (applyConditionWithoutAnchor && !string.IsNullOrWhiteSpace(conditionIdToApply))
            {
                if (ConditionService.Instance == null)
                {
                    return ServerActionResult.Fail(
                        ServerActionErrorCode.InvalidState,
                        "Cannot apply arrival condition because ConditionService is unavailable.");
                }

                result = ConditionService.Instance.ApplyConditionForClient(
                    clientId,
                    conditionIdToApply,
                    conditionSourceType,
                    anchorSourceId);

                if (!result.Success)
                {
                    return result;
                }
            }

            if (verboseLogging)
            {
                Debug.Log(
                    $"[NetworkArrivalProfile] Applied profile '{name}' to client {clientId}. " +
                    $"Reason={reason}, Mode={anchorResolutionMode}, Target={targetStableObjectId}, " +
                    $"Anchored={beginAnchorOnArrival}, Condition={conditionIdToApply}");
            }

            return ServerActionResult.Ok();
        }

        public ServerActionResult ApplyPostSpawn(
            ulong clientId,
            NetworkObject actor,
            WorldArrivalReason reason)
        {
            return ApplyPostSpawn(clientId, actor, null, reason);
        }

        private bool TryResolvePlacementAnchor(
            WorldInstance instance,
            out Transform resolvedAnchor,
            out string error)
        {
            if (anchorResolutionMode == ArrivalAnchorResolutionMode.DirectTransforms)
            {
                resolvedAnchor = placementAnchor;
                error = resolvedAnchor == null ? "Direct placement anchor is not assigned." : string.Empty;
                return resolvedAnchor != null;
            }

            return TryResolveSpawnedAnchor(
                instance,
                placementAnchorId,
                required: true,
                out resolvedAnchor,
                out error);
        }

        private bool TryResolveAnchor(
            WorldInstance instance,
            out Transform resolvedAnchor,
            out string error)
        {
            if (anchorResolutionMode == ArrivalAnchorResolutionMode.DirectTransforms)
            {
                resolvedAnchor = anchor != null ? anchor : placementAnchor;
                error = resolvedAnchor == null ? "Direct anchor is not assigned." : string.Empty;
                return resolvedAnchor != null;
            }

            string resolvedAnchorId = string.IsNullOrWhiteSpace(anchorId) ? placementAnchorId : anchorId;

            return TryResolveSpawnedAnchor(
                instance,
                resolvedAnchorId,
                required: true,
                out resolvedAnchor,
                out error);
        }

        private bool TryResolveExitAnchor(
            WorldInstance instance,
            out Transform resolvedAnchor,
            out string error)
        {
            if (anchorResolutionMode == ArrivalAnchorResolutionMode.DirectTransforms)
            {
                resolvedAnchor = exitAnchor;
                error = string.Empty;
                return true;
            }

            if (string.IsNullOrWhiteSpace(exitAnchorId))
            {
                resolvedAnchor = null;
                error = string.Empty;
                return true;
            }

            return TryResolveSpawnedAnchor(
                instance,
                exitAnchorId,
                required: true,
                out resolvedAnchor,
                out error);
        }

        private bool TryResolveSpawnedAnchor(
            WorldInstance instance,
            string requestedAnchorId,
            bool required,
            out Transform resolvedAnchor,
            out string error)
        {
            resolvedAnchor = null;
            error = string.Empty;

            if (instance == null)
            {
                error = "WorldInstance was not provided.";
                return !required;
            }

            if (string.IsNullOrWhiteSpace(targetStableObjectId))
            {
                error = "Target Stable Object Id is empty.";
                return !required;
            }

            if (string.IsNullOrWhiteSpace(requestedAnchorId))
            {
                error = "Requested anchor ID is empty.";
                return !required;
            }

            NetworkObject targetObject = FindSpawnedNetworkObject(instance, targetStableObjectId);

            if (targetObject == null)
            {
                error = $"No spawned object with StableObjectId '{targetStableObjectId}' was found in instance '{instance.InstanceId}'.";
                return !required;
            }

            targetObject.GetComponentsInChildren(true, _providerBuffer);

            for (int i = 0; i < _providerBuffer.Count; i++)
            {
                NetworkArrivalAnchorProvider provider = _providerBuffer[i];

                if (provider != null && provider.TryGetAnchor(requestedAnchorId, out resolvedAnchor))
                {
                    _providerBuffer.Clear();
                    return true;
                }
            }

            _providerBuffer.Clear();
            error =
                $"Spawned object '{targetStableObjectId}' has no NetworkArrivalAnchorProvider anchor named '{requestedAnchorId}'.";

            return !required;
        }

        private static NetworkObject FindSpawnedNetworkObject(
            WorldInstance instance,
            string stableObjectId)
        {
            if (instance == null || string.IsNullOrWhiteSpace(stableObjectId))
            {
                return null;
            }

            for (int i = 0; i < instance.SpawnedSceneObjects.Count; i++)
            {
                NetworkObject networkObject = instance.SpawnedSceneObjects[i];

                if (networkObject == null)
                {
                    continue;
                }

                if (!networkObject.TryGetComponent(out NetworkInstanceObject instanceObject))
                {
                    continue;
                }

                string objectStableId = instanceObject.StableObjectId.Value.ToString();

                if (string.Equals(objectStableId, stableObjectId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return networkObject;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(anchorSourceId))
            {
                anchorSourceId = "arrival";
            }

            if (string.IsNullOrWhiteSpace(anchorExitPrompt))
            {
                anchorExitPrompt = "Release";
            }

            if (anchorResolutionMode == ArrivalAnchorResolutionMode.DirectTransforms && anchor == null && placementAnchor != null)
            {
                anchor = placementAnchor;
            }

            if (string.IsNullOrWhiteSpace(placementAnchorId))
            {
                placementAnchorId = "rest";
            }

            if (string.IsNullOrWhiteSpace(anchorId))
            {
                anchorId = placementAnchorId;
            }
        }
    }

    public enum ArrivalAnchorResolutionMode : byte
    {
        DirectTransforms = 0,
        SpawnedInstanceObject = 1
    }
}
