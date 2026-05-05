using System;
using System.Collections.Generic;
using ROC.Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ROC.Networking.World
{
    [DisallowMultipleComponent]
    public sealed class NetworkSpawnPoint : MonoBehaviour
    {
        private static readonly List<NetworkSpawnPoint> Points = new();

        [Header("Identity")]
        [SerializeField] private string sceneId;
        [SerializeField] private string spawnPointId = "default";

        [Header("Selection Metadata")]
        [Tooltip("Optional tags for future tag/direction-based arrival selection, such as west, road, from_tallwood, safe.")]
        [SerializeField] private string[] tags = Array.Empty<string>();

        [Tooltip("Higher priority wins when a fallback search finds multiple points in the same scene.")]
        [SerializeField] private int priority;

        [Header("Placement")]
        [Tooltip("If true, the spawn point rotation is used when this point is selected.")]
        [SerializeField] private bool useRotation = true;

        [Header("Arrival Profile")]
        [Tooltip("Optional profile that can alter placement and apply post-spawn state such as anchoring/resting.")]
        [SerializeField] private NetworkArrivalProfile arrivalProfile;

        public string SceneId => sceneId;
        public string SpawnPointId => spawnPointId;
        public IReadOnlyList<string> Tags => tags;
        public int Priority => priority;
        public bool UseRotation => useRotation;
        public NetworkArrivalProfile ArrivalProfile => arrivalProfile;

        private void OnEnable()
        {
            if (!Points.Contains(this))
            {
                Points.Add(this);
            }
        }

        private void OnDisable()
        {
            Points.Remove(this);
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tags == null)
            {
                return false;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void ResolveBasePose(
            Quaternion fallbackRotation,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = transform.position;
            rotation = useRotation ? transform.rotation : fallbackRotation;
        }

        public bool TryResolveArrivalPose(
            WorldInstance instance,
            WorldArrivalReason reason,
            Vector3 defaultPosition,
            Quaternion defaultRotation,
            out Vector3 position,
            out Quaternion rotation)
        {
            if (arrivalProfile != null &&
                arrivalProfile.TryResolveSpawnPose(
                    instance,
                    defaultPosition,
                    defaultRotation,
                    reason,
                    out position,
                    out rotation))
            {
                return true;
            }

            position = defaultPosition;
            rotation = defaultRotation;
            return false;
        }

        public bool TryResolveArrivalPose(
            WorldArrivalReason reason,
            Vector3 defaultPosition,
            Quaternion defaultRotation,
            out Vector3 position,
            out Quaternion rotation)
        {
            return TryResolveArrivalPose(null, reason, defaultPosition, defaultRotation, out position, out rotation);
        }

        public static bool TryFindInScene(
            Scene loadedScene,
            WorldLocation location,
            WorldSceneCatalog catalog,
            out NetworkSpawnPoint point)
        {
            point = null;

            if (location.HasSpawnPointId)
            {
                point = FindExact(loadedScene, location.SceneId, location.SpawnPointId);

                if (point != null)
                {
                    return true;
                }
            }

            if (catalog != null &&
                catalog.TryGetScene(location.SceneId, out WorldSceneDefinition definition) &&
                !string.IsNullOrWhiteSpace(definition.DefaultSpawnPointId))
            {
                point = FindExact(loadedScene, location.SceneId, definition.DefaultSpawnPointId);

                if (point != null)
                {
                    return true;
                }
            }

            point = FindFirstInScene(loadedScene, location.SceneId);
            return point != null;
        }

        public static bool TryFindTaggedInScene(
            Scene loadedScene,
            string targetSceneId,
            string requiredTag,
            out NetworkSpawnPoint point)
        {
            point = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < Points.Count; i++)
            {
                NetworkSpawnPoint candidate = Points[i];

                if (!IsUsableCandidate(candidate, loadedScene, targetSceneId))
                {
                    continue;
                }

                if (!candidate.HasTag(requiredTag))
                {
                    continue;
                }

                if (point == null || candidate.Priority > bestPriority)
                {
                    point = candidate;
                    bestPriority = candidate.Priority;
                }
            }

            return point != null;
        }

        private static NetworkSpawnPoint FindExact(
            Scene loadedScene,
            string targetSceneId,
            string targetSpawnPointId)
        {
            for (int i = 0; i < Points.Count; i++)
            {
                NetworkSpawnPoint point = Points[i];

                if (!IsUsableCandidate(point, loadedScene, targetSceneId))
                {
                    continue;
                }

                if (string.Equals(point.spawnPointId, targetSpawnPointId, StringComparison.OrdinalIgnoreCase))
                {
                    return point;
                }
            }

            return null;
        }

        private static NetworkSpawnPoint FindFirstInScene(
            Scene loadedScene,
            string targetSceneId)
        {
            NetworkSpawnPoint best = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < Points.Count; i++)
            {
                NetworkSpawnPoint point = Points[i];

                if (!IsUsableCandidate(point, loadedScene, targetSceneId))
                {
                    continue;
                }

                if (best == null || point.Priority > bestPriority)
                {
                    best = point;
                    bestPriority = point.Priority;
                }
            }

            return best;
        }

        private static bool IsUsableCandidate(
            NetworkSpawnPoint point,
            Scene loadedScene,
            string targetSceneId)
        {
            if (point == null || !point.isActiveAndEnabled)
            {
                return false;
            }

            if (point.gameObject.scene != loadedScene)
            {
                return false;
            }

            return string.Equals(point.sceneId, targetSceneId, StringComparison.OrdinalIgnoreCase);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(spawnPointId))
            {
                spawnPointId = "default";
            }

            if (arrivalProfile == null)
            {
                arrivalProfile = GetComponent<NetworkArrivalProfile>();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;

            Vector3 basePosition = transform.position;
            Vector3 topPosition = basePosition + Vector3.up * 1.8f;

            Gizmos.DrawWireSphere(basePosition + Vector3.up * 0.1f, 0.2f);
            Gizmos.DrawLine(basePosition, topPosition);
            Gizmos.DrawLine(topPosition, topPosition + transform.forward * 0.75f);
        }
    }
}
