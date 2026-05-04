using System;
using System.Collections.Generic;
using System.IO;
using ROC.Game.World;
using UnityEngine;

namespace ROC.Game.Sessions
{
    [DisallowMultipleComponent]
    public sealed class LocalCharacterRepository : MonoBehaviour, ICharacterRepository, IPlayerLocationRepository
    {
        public static LocalCharacterRepository Instance { get; private set; }

        [Header("Storage")]
        [SerializeField] private string relativeFolder = "LocalServer";
        [SerializeField] private string fileName = "characters.v1.json";
        [SerializeField] private bool prettyJson = true;

        [Header("Default Locations")]
        [SerializeField] private string introSceneId = "intro_arrival";
        [SerializeField] private string introSpawnPointId = "intro_start";

        [SerializeField] private string defaultSharedSceneId = "rolling_plains";
        [SerializeField] private string defaultSharedInstanceId = "shared-rolling-plains-001";
        [SerializeField] private string defaultSharedSpawnPointId = "shared_start";

        private CharacterDatabase _database;
        private bool _loaded;

        private string StorageDirectory => Path.Combine(Application.persistentDataPath, relativeFolder);
        private string StoragePath => Path.Combine(StorageDirectory, fileName);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            LoadIfNeeded();
        }

        public IReadOnlyList<PersistentCharacterRecord> GetCharactersForAccount(string accountId)
        {
            LoadIfNeeded();
            EnsureAccount(accountId);

            List<PersistentCharacterRecord> results = new();

            for (int i = 0; i < _database.Characters.Count; i++)
            {
                PersistentCharacterRecord character = _database.Characters[i];

                if (character.AccountId == accountId)
                {
                    results.Add(character);
                }
            }

            return results;
        }

        public bool TryGetCharacter(string accountId, string characterId, out PersistentCharacterRecord character)
        {
            LoadIfNeeded();
            EnsureAccount(accountId);

            for (int i = 0; i < _database.Characters.Count; i++)
            {
                PersistentCharacterRecord candidate = _database.Characters[i];

                if (candidate.AccountId == accountId && candidate.CharacterId == characterId)
                {
                    character = candidate;
                    return true;
                }
            }

            character = null;
            return false;
        }

        public bool MarkIntroComplete(string accountId, string characterId, WorldLocation sharedWorldLocation)
        {
            if (!TryGetCharacter(accountId, characterId, out PersistentCharacterRecord character))
            {
                return false;
            }

            character.HasCompletedIntro = true;
            character.CurrentLocation = sharedWorldLocation;
            character.LastOnlineUtc = DateTime.UtcNow.ToString("O");

            Save();
            return true;
        }

        public bool UpdateCharacterLocation(string accountId, string characterId, WorldLocation location)
        {
            if (!TryGetCharacter(accountId, characterId, out PersistentCharacterRecord character))
            {
                return false;
            }

            character.CurrentLocation = location;
            character.LastOnlineUtc = DateTime.UtcNow.ToString("O");

            Save();
            return true;
        }

        public void Save()
        {
            LoadIfNeeded();

            if (!Directory.Exists(StorageDirectory))
            {
                Directory.CreateDirectory(StorageDirectory);
            }

            string json = JsonUtility.ToJson(_database, prettyJson);
            File.WriteAllText(StoragePath, json);

            Debug.Log($"[LocalCharacterRepository] Saved characters to {StoragePath}");
        }

        private void LoadIfNeeded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            if (!File.Exists(StoragePath))
            {
                _database = new CharacterDatabase();
                Save();
                return;
            }

            string json = File.ReadAllText(StoragePath);
            _database = JsonUtility.FromJson<CharacterDatabase>(json) ?? new CharacterDatabase();

            if (_database.Characters == null)
            {
                _database.Characters = new List<PersistentCharacterRecord>();
            }

            Debug.Log($"[LocalCharacterRepository] Loaded characters from {StoragePath}");
        }

        private void EnsureAccount(string accountId)
        {
            for (int i = 0; i < _database.Characters.Count; i++)
            {
                if (_database.Characters[i].AccountId == accountId)
                {
                    return;
                }
            }

            string now = DateTime.UtcNow.ToString("O");

            string newCharacterId = $"{accountId}-new";
            string returningCharacterId = $"{accountId}-returning";

            _database.Characters.Add(new PersistentCharacterRecord
            {
                AccountId = accountId,
                CharacterId = newCharacterId,
                DisplayName = "New Arrival",
                HasCompletedIntro = false,
                CurrentLocation = WorldLocation.AtSpawnPoint(
                    introSceneId,
                    $"intro-{SanitizeId(newCharacterId)}",
                    introSpawnPointId),
                CreatedUtc = now,
                LastOnlineUtc = now
            });

            _database.Characters.Add(new PersistentCharacterRecord
            {
                AccountId = accountId,
                CharacterId = returningCharacterId,
                DisplayName = "Returning Guardian",
                HasCompletedIntro = true,
                CurrentLocation = WorldLocation.AtSpawnPoint(
                    defaultSharedSceneId,
                    defaultSharedInstanceId,
                    defaultSharedSpawnPointId),
                CreatedUtc = now,
                LastOnlineUtc = now
            });

            Save();
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            char[] chars = value.ToLowerInvariant().ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];

                if ((c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '-' ||
                    c == '_')
                {
                    continue;
                }

                chars[i] = '-';
            }

            return new string(chars);
        }

        [Serializable]
        private sealed class CharacterDatabase
        {
            public List<PersistentCharacterRecord> Characters = new();
        }
    }
}