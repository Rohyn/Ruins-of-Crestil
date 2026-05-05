using System.Collections.Generic;
using UnityEngine;

namespace ROC.Game.Inventory
{
    [CreateAssetMenu(
        fileName = "ItemCatalog",
        menuName = "ROC/Inventory/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> definitions = new();

        private readonly Dictionary<string, ItemDefinition> _byId = new();

        public IReadOnlyList<ItemDefinition> Definitions => definitions;

        private void OnEnable()
        {
            RebuildLookup();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildLookup();
        }
#endif

        public bool TryGetDefinition(string itemId, out ItemDefinition definition)
        {
            if (_byId.Count == 0)
            {
                RebuildLookup();
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                definition = null;
                return false;
            }

            return _byId.TryGetValue(itemId, out definition);
        }

        private void RebuildLookup()
        {
            _byId.Clear();

            for (int i = 0; i < definitions.Count; i++)
            {
                ItemDefinition definition = definitions[i];

                if (definition == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.ItemId))
                {
                    Debug.LogWarning($"[ItemCatalog] Item '{definition.name}' has an empty ItemId.", this);
                    continue;
                }

                if (_byId.ContainsKey(definition.ItemId))
                {
                    Debug.LogWarning($"[ItemCatalog] Duplicate ItemId '{definition.ItemId}' ignored.", this);
                    continue;
                }

                _byId.Add(definition.ItemId, definition);
            }
        }
    }
}