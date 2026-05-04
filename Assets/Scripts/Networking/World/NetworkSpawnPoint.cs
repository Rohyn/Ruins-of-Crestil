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

        [SerializeField] private string sceneId;
        [SerializeField] private string spawnPointId;

        public string SceneId => sceneId;
        public string SpawnPointId => spawnPointId;

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

        private static NetworkSpawnPoint FindExact(Scene loadedScene, string targetSceneId, string targetSpawnPointId)
        {
            for (int i = 0; i < Points.Count; i++)
            {
                NetworkSpawnPoint point = Points[i];

                if (point == null || !point.isActiveAndEnabled)
                {
                    continue;
                }

                if (point.gameObject.scene != loadedScene)
                {
                    continue;
                }

                if (point.sceneId == targetSceneId && point.spawnPointId == targetSpawnPointId)
                {
                    return point;
                }
            }

            return null;
        }

        private static NetworkSpawnPoint FindFirstInScene(Scene loadedScene, string targetSceneId)
        {
            for (int i = 0; i < Points.Count; i++)
            {
                NetworkSpawnPoint point = Points[i];

                if (point == null || !point.isActiveAndEnabled)
                {
                    continue;
                }

                if (point.gameObject.scene != loadedScene)
                {
                    continue;
                }

                if (point.sceneId == targetSceneId)
                {
                    return point;
                }
            }

            return null;
        }
    }
}