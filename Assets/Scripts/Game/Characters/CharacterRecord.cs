using UnityEngine;

namespace ROC.Game.Characters
{
    public sealed class CharacterRecord
    {
        public string CharacterId;
        public string DisplayName;
        public bool HasCompletedIntro;
        public CharacterRouteKind Route;
        public Vector3 LastPosition;
        public Quaternion LastRotation;

        public CharacterRecord(
            string characterId,
            string displayName,
            bool hasCompletedIntro,
            CharacterRouteKind route,
            Vector3 lastPosition,
            Quaternion lastRotation)
        {
            CharacterId = characterId;
            DisplayName = displayName;
            HasCompletedIntro = hasCompletedIntro;
            Route = route;
            LastPosition = lastPosition;
            LastRotation = lastRotation;
        }
    }
}