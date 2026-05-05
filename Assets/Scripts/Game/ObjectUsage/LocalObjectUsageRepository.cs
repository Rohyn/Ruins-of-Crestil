using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ROC.Game.ObjectUsage
{
    /// <summary>
    /// Local JSON implementation of object-scoped interaction usage state.
    /// This is a development persistence layer; the service/repository boundary is intended
    /// to be replaced by a database-backed implementation later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalObjectUsageRepository : MonoBehaviour, IObjectUsageRepository
    {
        public static LocalObjectUsageRepository Instance { get; private set; }

        [Header("Storage")]
        [SerializeField] private string relativeFolder = "LocalServer";
        [SerializeField] private string fileName = "object_usage.v1.json";
        [SerializeField] private bool prettyJson = true;

        private ObjectUsageDatabase _database;
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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool TryGetRecord(string usageKey, out ObjectUsageRecord record)
        {
            LoadIfNeeded();
            string normalizedKey = ObjectUsageKeyUtility.NormalizeKey(usageKey);
            for (int i = 0; i < _database.Records.Count; i++)
            {
                ObjectUsageRecord candidate = _database.Records[i];
                if (candidate != null && candidate.UsageKey == normalizedKey)
                {
                    record = candidate;
                    EnsureRecordLists(record);
                    return true;
                }
            }

            record = null;
            return false;
        }

        public bool HasCharacterUse(string usageKey, string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            if (!TryGetRecord(usageKey, out ObjectUsageRecord record))
            {
                return false;
            }

            return FindCharacterUse(record, characterId) != null;
        }

        public int GetCharacterUseCount(string usageKey, string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return 0;
            }

            if (!TryGetRecord(usageKey, out ObjectUsageRecord record))
            {
                return 0;
            }

            ObjectUsageCharacterUseRecord use = FindCharacterUse(record, characterId);
            return use != null ? Mathf.Max(0, use.UseCount) : 0;
        }

        public int GetGlobalUseCount(string usageKey)
        {
            if (!TryGetRecord(usageKey, out ObjectUsageRecord record))
            {
                return 0;
            }

            return Mathf.Max(0, record.GlobalUseCount);
        }

        public bool RecordUse(
            string usageKey,
            string sceneId,
            string instanceId,
            string stableObjectId,
            string cleanupGroup,
            string characterId,
            bool recordCharacterUse,
            bool incrementGlobalUse,
            string source)
        {
            LoadIfNeeded();

            string normalizedKey = ObjectUsageKeyUtility.NormalizeKey(usageKey);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return false;
            }

            ObjectUsageRecord record = GetOrCreateRecord(
                normalizedKey,
                sceneId,
                instanceId,
                stableObjectId,
                cleanupGroup);

            string now = DateTime.UtcNow.ToString("O");
            record.UpdatedUtc = now;
            record.SceneId = sceneId ?? string.Empty;
            record.InstanceId = instanceId ?? string.Empty;
            record.StableObjectId = stableObjectId ?? string.Empty;
            record.CleanupGroup = cleanupGroup ?? string.Empty;

            if (incrementGlobalUse)
            {
                record.GlobalUseCount = Mathf.Max(0, record.GlobalUseCount) + 1;
            }

            if (recordCharacterUse && !string.IsNullOrWhiteSpace(characterId))
            {
                ObjectUsageCharacterUseRecord characterUse = FindCharacterUse(record, characterId);
                if (characterUse == null)
                {
                    characterUse = new ObjectUsageCharacterUseRecord
                    {
                        CharacterId = characterId,
                        UseCount = 0,
                        FirstUsedUtc = now,
                        Source = source ?? string.Empty
                    };
                    record.CharacterUses.Add(characterUse);
                }

                characterUse.UseCount = Mathf.Max(0, characterUse.UseCount) + 1;
                characterUse.LastUsedUtc = now;
                characterUse.Source = source ?? string.Empty;
            }

            Save();
            return true;
        }

        public bool ResetCharacterUses(string usageKey)
        {
            if (!TryGetRecord(usageKey, out ObjectUsageRecord record))
            {
                return false;
            }

            record.CharacterUses.Clear();
            record.UpdatedUtc = DateTime.UtcNow.ToString("O");
            Save();
            return true;
        }

        public bool ResetGlobalUses(string usageKey)
        {
            if (!TryGetRecord(usageKey, out ObjectUsageRecord record))
            {
                return false;
            }

            record.GlobalUseCount = 0;
            record.UpdatedUtc = DateTime.UtcNow.ToString("O");
            Save();
            return true;
        }

        public bool DeleteUsage(string usageKey)
        {
            LoadIfNeeded();
            string normalizedKey = ObjectUsageKeyUtility.NormalizeKey(usageKey);
            for (int i = _database.Records.Count - 1; i >= 0; i--)
            {
                ObjectUsageRecord record = _database.Records[i];
                if (record == null || record.UsageKey != normalizedKey)
                {
                    continue;
                }

                _database.Records.RemoveAt(i);
                Save();
                return true;
            }

            return false;
        }

        public int DeleteUsagesByCleanupGroup(string cleanupGroup)
        {
            LoadIfNeeded();
            string normalizedGroup = ObjectUsageKeyUtility.NormalizePart(cleanupGroup);
            if (string.IsNullOrWhiteSpace(normalizedGroup))
            {
                return 0;
            }

            int removed = 0;
            for (int i = _database.Records.Count - 1; i >= 0; i--)
            {
                ObjectUsageRecord record = _database.Records[i];
                if (record == null || ObjectUsageKeyUtility.NormalizePart(record.CleanupGroup) != normalizedGroup)
                {
                    continue;
                }

                _database.Records.RemoveAt(i);
                removed++;
            }

            if (removed > 0)
            {
                Save();
            }

            return removed;
        }

        public int DeleteUsagesByInstance(string instanceId)
        {
            LoadIfNeeded();
            string normalizedInstanceId = ObjectUsageKeyUtility.NormalizePart(instanceId);
            if (string.IsNullOrWhiteSpace(normalizedInstanceId))
            {
                return 0;
            }

            int removed = 0;
            for (int i = _database.Records.Count - 1; i >= 0; i--)
            {
                ObjectUsageRecord record = _database.Records[i];
                if (record == null || ObjectUsageKeyUtility.NormalizePart(record.InstanceId) != normalizedInstanceId)
                {
                    continue;
                }

                _database.Records.RemoveAt(i);
                removed++;
            }

            if (removed > 0)
            {
                Save();
            }

            return removed;
        }

        private ObjectUsageRecord GetOrCreateRecord(
            string usageKey,
            string sceneId,
            string instanceId,
            string stableObjectId,
            string cleanupGroup)
        {
            if (TryGetRecord(usageKey, out ObjectUsageRecord existing))
            {
                return existing;
            }

            string now = DateTime.UtcNow.ToString("O");
            var record = new ObjectUsageRecord
            {
                UsageKey = usageKey,
                SceneId = sceneId ?? string.Empty,
                InstanceId = instanceId ?? string.Empty,
                StableObjectId = stableObjectId ?? string.Empty,
                CleanupGroup = cleanupGroup ?? string.Empty,
                CreatedUtc = now,
                UpdatedUtc = now,
                GlobalUseCount = 0,
                CharacterUses = new List<ObjectUsageCharacterUseRecord>()
            };

            _database.Records.Add(record);
            return record;
        }

        private static ObjectUsageCharacterUseRecord FindCharacterUse(
            ObjectUsageRecord record,
            string characterId)
        {
            if (record == null || record.CharacterUses == null || string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            for (int i = 0; i < record.CharacterUses.Count; i++)
            {
                ObjectUsageCharacterUseRecord use = record.CharacterUses[i];
                if (use != null && use.CharacterId == characterId)
                {
                    return use;
                }
            }

            return null;
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
                _database = new ObjectUsageDatabase();
                Save();
                return;
            }

            string json = File.ReadAllText(StoragePath);
            _database = JsonUtility.FromJson<ObjectUsageDatabase>(json) ?? new ObjectUsageDatabase();
            if (_database.Records == null)
            {
                _database.Records = new List<ObjectUsageRecord>();
            }

            for (int i = 0; i < _database.Records.Count; i++)
            {
                EnsureRecordLists(_database.Records[i]);
            }

            Debug.Log($"[LocalObjectUsageRepository] Loaded object usage from {StoragePath}");
        }

        private static void EnsureRecordLists(ObjectUsageRecord record)
        {
            if (record == null)
            {
                return;
            }

            if (record.CharacterUses == null)
            {
                record.CharacterUses = new List<ObjectUsageCharacterUseRecord>();
            }
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
        private sealed class ObjectUsageDatabase
        {
            public List<ObjectUsageRecord> Records = new();
        }
    }
}
