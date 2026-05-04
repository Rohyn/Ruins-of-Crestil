using System.Collections.Generic;
using ROC.Game.Characters;
using UnityEngine;

namespace ROC.Networking.World
{
    public enum SpawnPointKind : byte
    {
        IntroStart = 0,
        SharedWorldStart = 1
    }

    [DisallowMultipleComponent]
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        private static readonly List<PlayerSpawnPoint> Points = new();

        [SerializeField] private SpawnPointKind kind = SpawnPointKind.SharedWorldStart;

        public SpawnPointKind Kind => kind;

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

        public static PlayerSpawnPoint FindBest(CharacterRouteKind route)
        {
            SpawnPointKind desiredKind = route == CharacterRouteKind.Intro
                ? SpawnPointKind.IntroStart
                : SpawnPointKind.SharedWorldStart;

            for (int i = 0; i < Points.Count; i++)
            {
                PlayerSpawnPoint point = Points[i];
                if (point != null && point.isActiveAndEnabled && point.Kind == desiredKind)
                {
                    return point;
                }
            }

            return null;
        }
    }
}