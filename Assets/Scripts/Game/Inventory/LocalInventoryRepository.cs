using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ROC.Game.Inventory
{
    [DisallowMultipleComponent]
    public sealed class LocalInventoryRepository : MonoBehaviour, IInventoryRepository
    {
        public static LocalInventoryRepository Instance { get; private set; }

        [Header("Storage")]
        [SerializeField] private string relativeFolder = "LocalServer";
        [SerializeField] private string fileName = "inventories.v1.json";
        [SerializeField] private bool prettyJson = true;

        private InventoryDatabase _database;
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

        public CharacterInventoryRecord GetOrCreateInventory(string characterId)
        {
            LoadIfNeeded();

            string resolvedCharacterId = string.IsNullOrWhiteSpace(characterId)
                ? "unknown"
                : characterId;

            for (int i = 0; i < _database.Characters.Count; i++)
            {
                if (_database.Characters[i].CharacterId == resolvedCharacterId)
                {
                    return _database.Characters[i];
                }
            }

            var record = new CharacterInventoryRecord
            {
                CharacterId = resolvedCharacterId,
                Items = new List<InventoryItemInstanceRecord>()
            };

            _database.Characters.Add(record);
            Save();

            return record;
        }

        public bool SaveInventory(CharacterInventoryRecord inventory)
        {
            LoadIfNeeded();

            if (inventory == null || string.IsNullOrWhiteSpace(inventory.CharacterId))
            {
                return false;
            }

            for (int i = 0; i < _database.Characters.Count; i++)
            {
                if (_database.Characters[i].CharacterId != inventory.CharacterId)
                {
                    continue;
                }

                _database.Characters[i] = inventory;
                Save();
                return true;
            }

            _database.Characters.Add(inventory);
            Save();
            return true;
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
                _database = new InventoryDatabase();
                Save();
                return;
            }

            string json = File.ReadAllText(StoragePath);
            _database = JsonUtility.FromJson<InventoryDatabase>(json) ?? new InventoryDatabase();

            if (_database.Characters == null)
            {
                _database.Characters = new List<CharacterInventoryRecord>();
            }

            for (int i = 0; i < _database.Characters.Count; i++)
            {
                _database.Characters[i].Items ??= new List<InventoryItemInstanceRecord>();
            }

            Debug.Log($"[LocalInventoryRepository] Loaded inventories from {StoragePath}");
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
        private sealed class InventoryDatabase
        {
            public List<CharacterInventoryRecord> Characters = new();
        }
    }
}