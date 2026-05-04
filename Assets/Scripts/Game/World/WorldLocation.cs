using System;
using UnityEngine;

namespace ROC.Game.World
{
    [Serializable]
    public struct WorldLocation
    {
        public string SceneId;
        public string InstanceId;
        public string SpawnPointId;

        public Vector3 Position;
        public Quaternion Rotation;

        public bool UseExplicitTransform;

        public bool HasSceneId => !string.IsNullOrWhiteSpace(SceneId);
        public bool HasInstanceId => !string.IsNullOrWhiteSpace(InstanceId);
        public bool HasSpawnPointId => !string.IsNullOrWhiteSpace(SpawnPointId);

        public static WorldLocation AtSpawnPoint(string sceneId, string instanceId, string spawnPointId)
        {
            return new WorldLocation
            {
                SceneId = sceneId,
                InstanceId = instanceId,
                SpawnPointId = spawnPointId,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                UseExplicitTransform = false
            };
        }

        public static WorldLocation AtTransform(string sceneId, string instanceId, Vector3 position, Quaternion rotation)
        {
            return new WorldLocation
            {
                SceneId = sceneId,
                InstanceId = instanceId,
                SpawnPointId = string.Empty,
                Position = position,
                Rotation = rotation,
                UseExplicitTransform = true
            };
        }

        public WorldLocation WithInstanceId(string instanceId)
        {
            InstanceId = instanceId;
            return this;
        }

        public WorldLocation WithSpawnPointId(string spawnPointId)
        {
            SpawnPointId = spawnPointId;
            UseExplicitTransform = false;
            return this;
        }
    }
}