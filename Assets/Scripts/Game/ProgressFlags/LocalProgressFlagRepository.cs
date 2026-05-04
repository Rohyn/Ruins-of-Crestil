using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ROC.Game.ProgressFlags
{
    [DisallowMultipleComponent]
    public sealed class LocalProgressFlagRepository : MonoBehaviour, IProgressFlagRepository
    {
        public static LocalProgressFlagRepository Instance { get; private set; }

        [Header("Storage")]
        [SerializeField] private string relativeFolder = "LocalServer";
        [SerializeField] private string fileName = "progress_flags.v1.json";
        [SerializeField] private bool prettyJson = true;

        private ProgressFlagDatabase _database;
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

        public IReadOnlyList<ProgressFlagRecord> GetFlags(string characterId)
        {
            LoadIfNeeded();

            CharacterProgressFlags entry = GetOrCreateCharacterEntry(characterId);
            return entry.Flags;
        }

        public bool HasFlag(string characterId, string flagId)
        {
            LoadIfNeeded();

            if (!ProgressFlagIdUtility.IsValidFlagId(flagId))
            {
                return false;
            }

            string normalizedFlagId = ProgressFlagIdUtility.NormalizeFlagId(flagId);
            CharacterProgressFlags entry = GetOrCreateCharacterEntry(characterId);

            for (int i = 0; i < entry.Flags.Count; i++)
            {
                if (entry.Flags[i].FlagId == normalizedFlagId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool SetFlag(
            string characterId,
            string flagId,
            ProgressFlagLifetime lifetime,
            string source)
        {
            LoadIfNeeded();

            if (string.IsNullOrWhiteSpace(characterId) ||
                !ProgressFlagIdUtility.IsValidFlagId(flagId))
            {
                return false;
            }

            string normalizedFlagId = ProgressFlagIdUtility.NormalizeFlagId(flagId);
            CharacterProgressFlags entry = GetOrCreateCharacterEntry(characterId);

            for (int i = 0; i < entry.Flags.Count; i++)
            {
                ProgressFlagRecord existing = entry.Flags[i];

                if (existing.FlagId != normalizedFlagId)
                {
                    continue;
                }

                existing.Lifetime = lifetime;
                existing.Source = source ?? string.Empty;
                existing.SetUtc = DateTime.UtcNow.ToString("O");

                Save();
                return true;
            }

            entry.Flags.Add(new ProgressFlagRecord
            {
                FlagId = normalizedFlagId,
                Lifetime = lifetime,
                Source = source ?? string.Empty,
                SetUtc = DateTime.UtcNow.ToString("O")
            });

            Save();
            return true;
        }

        public bool RemoveFlag(string characterId, string flagId)
        {
            LoadIfNeeded();

            if (string.IsNullOrWhiteSpace(characterId) ||
                !ProgressFlagIdUtility.IsValidFlagId(flagId))
            {
                return false;
            }

            string normalizedFlagId = ProgressFlagIdUtility.NormalizeFlagId(flagId);
            CharacterProgressFlags entry = GetOrCreateCharacterEntry(characterId);

            for (int i = entry.Flags.Count - 1; i >= 0; i--)
            {
                if (entry.Flags[i].FlagId != normalizedFlagId)
                {
                    continue;
                }

                entry.Flags.RemoveAt(i);
                Save();
                return true;
            }

            return false;
        }

        public int ClearFlagsWithPrefix(string characterId, string prefix)
        {
            LoadIfNeeded();

            if (string.IsNullOrWhiteSpace(characterId) ||
                !ProgressFlagIdUtility.IsValidPrefix(prefix))
            {
                return 0;
            }

            string normalizedPrefix = ProgressFlagIdUtility.NormalizePrefix(prefix);
            CharacterProgressFlags entry = GetOrCreateCharacterEntry(characterId);

            int removed = 0;

            for (int i = entry.Flags.Count - 1; i >= 0; i--)
            {
                if (!entry.Flags[i].FlagId.StartsWith(normalizedPrefix))
                {
                    continue;
                }

                entry.Flags.RemoveAt(i);
                removed++;
            }

            if (removed > 0)
            {
                Save();
            }

            return removed;
        }

        private CharacterProgressFlags GetOrCreateCharacterEntry(string characterId)
        {
            string normalizedCharacterId = string.IsNullOrWhiteSpace(characterId)
                ? "unknown"
                : characterId;

            for (int i = 0; i < _database.Characters.Count; i++)
            {
                if (_database.Characters[i].CharacterId == normalizedCharacterId)
                {
                    return _database.Characters[i];
                }
            }

            var entry = new CharacterProgressFlags
            {
                CharacterId = normalizedCharacterId,
                Flags = new List<ProgressFlagRecord>()
            };

            _database.Characters.Add(entry);
            Save();

            return entry;
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
                _database = new ProgressFlagDatabase();
                Save();
                return;
            }

            string json = File.ReadAllText(StoragePath);
            _database = JsonUtility.FromJson<ProgressFlagDatabase>(json) ?? new ProgressFlagDatabase();

            if (_database.Characters == null)
            {
                _database.Characters = new List<CharacterProgressFlags>();
            }

            for (int i = 0; i < _database.Characters.Count; i++)
            {
                if (_database.Characters[i].Flags == null)
                {
                    _database.Characters[i].Flags = new List<ProgressFlagRecord>();
                }
            }

            Debug.Log($"[LocalProgressFlagRepository] Loaded progress flags from {StoragePath}");
        }

        private void Save()
        {
            if (!Directory.Exists(StorageDirectory))
            {
                Directory.CreateDirectory(StorageDirectory);
            }

            string json = JsonUtility.ToJson(_database, prettyJson);
            File.WriteAllText(StoragePath, json);
        }

        [Serializable]
        private sealed class ProgressFlagDatabase
        {
            public List<CharacterProgressFlags> Characters = new();
        }

        [Serializable]
        private sealed class CharacterProgressFlags
        {
            public string CharacterId;
            public List<ProgressFlagRecord> Flags = new();
        }
    }
}