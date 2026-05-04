using System.Collections.Generic;
using UnityEngine;

namespace ROC.Game.World
{
    [CreateAssetMenu(
        fileName = "WorldSceneCatalog",
        menuName = "ROC/World/World Scene Catalog")]
    public sealed class WorldSceneCatalog : ScriptableObject
    {
        [SerializeField] private List<WorldSceneDefinition> scenes = new();

        private readonly Dictionary<string, WorldSceneDefinition> _bySceneId = new();

        public IReadOnlyList<WorldSceneDefinition> Scenes => scenes;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public bool TryGetScene(string sceneId, out WorldSceneDefinition definition)
        {
            if (_bySceneId.Count == 0)
            {
                RebuildLookup();
            }

            if (string.IsNullOrWhiteSpace(sceneId))
            {
                definition = null;
                return false;
            }

            return _bySceneId.TryGetValue(sceneId, out definition);
        }

        public bool TryGetUnitySceneName(string sceneId, out string unitySceneName)
        {
            if (TryGetScene(sceneId, out WorldSceneDefinition definition))
            {
                unitySceneName = definition.UnitySceneName;
                return true;
            }

            unitySceneName = string.Empty;
            return false;
        }

        private void RebuildLookup()
        {
            _bySceneId.Clear();

            for (int i = 0; i < scenes.Count; i++)
            {
                WorldSceneDefinition scene = scenes[i];

                if (scene == null || !scene.IsValid())
                {
                    continue;
                }

                if (_bySceneId.ContainsKey(scene.SceneId))
                {
                    Debug.LogWarning($"[WorldSceneCatalog] Duplicate SceneId ignored: {scene.SceneId}");
                    continue;
                }

                _bySceneId.Add(scene.SceneId, scene);
            }
        }
    }
}