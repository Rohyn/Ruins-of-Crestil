using UnityEngine;

namespace ROC.Game.World
{
    [CreateAssetMenu(
        fileName = "WorldSceneDefinition",
        menuName = "ROC/World/World Scene Definition")]
    public sealed class WorldSceneDefinition : ScriptableObject
    {
        [Header("Stable IDs")]
        [SerializeField] private string sceneId;
        [SerializeField] private string unitySceneName;

        [Header("World")]
        [SerializeField] private WorldSceneKind sceneKind = WorldSceneKind.SharedOutdoor;
        [SerializeField] private WorldInstanceKind defaultInstanceKind = WorldInstanceKind.SharedShard;

        [Header("Defaults")]
        [SerializeField] private string defaultInstanceId;
        [SerializeField] private string defaultSpawnPointId;

        [Header("Persistence")]
        [SerializeField] private bool allowsSavedReturnLocation = true;

        [Header("Server Lifecycle")]
        [SerializeField] private bool unloadServerInstanceWhenEmpty = true;

        public string SceneId => sceneId;
        public string UnitySceneName => unitySceneName;
        public WorldSceneKind SceneKind => sceneKind;
        public WorldInstanceKind DefaultInstanceKind => defaultInstanceKind;
        public string DefaultInstanceId => defaultInstanceId;
        public string DefaultSpawnPointId => defaultSpawnPointId;
        public bool AllowsSavedReturnLocation => allowsSavedReturnLocation;
        public bool UnloadServerInstanceWhenEmpty => unloadServerInstanceWhenEmpty;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(sceneId)
                   && !string.IsNullOrWhiteSpace(unitySceneName);
        }

        public WorldLocation CreateDefaultLocation(string instanceIdOverride = null)
        {
            string resolvedInstanceId = string.IsNullOrWhiteSpace(instanceIdOverride)
                ? defaultInstanceId
                : instanceIdOverride;

            return WorldLocation.AtSpawnPoint(
                sceneId,
                resolvedInstanceId,
                defaultSpawnPointId);
        }
    }
}