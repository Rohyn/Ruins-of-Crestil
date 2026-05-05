using System;
using System.Text;
using ROC.Game.World;
using UnityEngine;

namespace ROC.Game.Sessions
{
    [DisallowMultipleComponent]
    public sealed class CharacterRoutingService : MonoBehaviour
    {
        public static CharacterRoutingService Instance { get; private set; }

        [Header("Catalog")]
        [SerializeField] private WorldSceneCatalog sceneCatalog;

        [Header("Scene IDs")]
        [SerializeField] private string introSceneId = "intro_arrival";
        [SerializeField] private string defaultSharedSceneId = "rolling_plains";

        [Header("Entry Behavior")]
        [Tooltip("Auto-created local test characters have CreatedUtc and LastOnlineUtc set together. While those timestamps are still effectively equal, treat an incomplete-intro character as entering for the first time.")]
        [SerializeField, Min(0f)] private float firstEntryTimestampToleranceSeconds = 1f;

        public WorldSceneCatalog SceneCatalog => sceneCatalog;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public bool TryResolveEntryLocation(
            PersistentCharacterRecord character,
            ulong clientId,
            out WorldLocation location,
            out string error)
        {
            return TryResolveEntryLocation(
                character,
                clientId,
                out location,
                out _,
                out error);
        }

        public bool TryResolveEntryLocation(
            PersistentCharacterRecord character,
            ulong clientId,
            out WorldLocation location,
            out WorldArrivalReason arrivalReason,
            out string error)
        {
            location = default;
            arrivalReason = WorldArrivalReason.Unknown;
            error = string.Empty;

            if (character == null)
            {
                error = "Cannot route null character.";
                return false;
            }

            if (sceneCatalog == null)
            {
                error = "Missing WorldSceneCatalog.";
                return false;
            }

            if (!character.HasCompletedIntro && IsLikelyFirstEntry(character))
            {
                arrivalReason = WorldArrivalReason.InitialCharacterSpawn;
                return TryResolveIntroLocation(character, clientId, out location, out error);
            }

            if (TryResolveSavedReturnLocation(character, out location))
            {
                arrivalReason = WorldArrivalReason.SavedLocationResume;
                return true;
            }

            if (!character.HasCompletedIntro)
            {
                arrivalReason = WorldArrivalReason.InitialCharacterSpawn;
                return TryResolveIntroLocation(character, clientId, out location, out error);
            }

            arrivalReason = WorldArrivalReason.DefaultWorldFallback;
            return TryResolveDefaultSharedLocation(out location, out error);
        }

        public bool TryResolveIntroCompletionLocation(
            PersistentCharacterRecord character,
            out WorldLocation location,
            out string error)
        {
            return TryResolveDefaultSharedLocation(out location, out error);
        }

        private bool TryResolveSavedReturnLocation(
            PersistentCharacterRecord character,
            out WorldLocation location)
        {
            location = default;

            if (character == null || !character.CurrentLocation.HasSceneId)
            {
                return false;
            }

            if (!sceneCatalog.TryGetScene(character.CurrentLocation.SceneId, out WorldSceneDefinition savedScene))
            {
                return false;
            }

            if (!savedScene.AllowsSavedReturnLocation)
            {
                return false;
            }

            location = character.CurrentLocation;

            if (!location.HasInstanceId)
            {
                location.InstanceId = savedScene.DefaultInstanceId;
            }

            if (!location.HasSpawnPointId && !location.UseExplicitTransform)
            {
                location.SpawnPointId = savedScene.DefaultSpawnPointId;
            }

            return true;
        }

        private bool TryResolveIntroLocation(
            PersistentCharacterRecord character,
            ulong clientId,
            out WorldLocation location,
            out string error)
        {
            location = default;
            error = string.Empty;

            if (!sceneCatalog.TryGetScene(introSceneId, out WorldSceneDefinition introScene))
            {
                error = $"Unknown intro SceneId: {introSceneId}";
                return false;
            }

            string privateInstanceId = $"intro-{SanitizeId(character.CharacterId)}-{clientId}";

            location = WorldLocation.AtSpawnPoint(
                introScene.SceneId,
                privateInstanceId,
                introScene.DefaultSpawnPointId);

            return true;
        }

        private bool TryResolveDefaultSharedLocation(
            out WorldLocation location,
            out string error)
        {
            location = default;
            error = string.Empty;

            if (!sceneCatalog.TryGetScene(defaultSharedSceneId, out WorldSceneDefinition sharedScene))
            {
                error = $"Unknown default shared SceneId: {defaultSharedSceneId}";
                return false;
            }

            location = sharedScene.CreateDefaultLocation();
            return true;
        }

        private bool IsLikelyFirstEntry(PersistentCharacterRecord character)
        {
            if (character == null || character.HasCompletedIntro)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(character.CreatedUtc) ||
                string.IsNullOrWhiteSpace(character.LastOnlineUtc))
            {
                return false;
            }

            if (!DateTime.TryParse(character.CreatedUtc, out DateTime createdUtc) ||
                !DateTime.TryParse(character.LastOnlineUtc, out DateTime lastOnlineUtc))
            {
                return false;
            }

            double deltaSeconds = Math.Abs((lastOnlineUtc.ToUniversalTime() - createdUtc.ToUniversalTime()).TotalSeconds);
            return deltaSeconds <= firstEntryTimestampToleranceSeconds;
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var builder = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);

                if ((c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '-' ||
                    c == '_')
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('-');
                }
            }

            return builder.ToString();
        }
    }
}
