using System.Collections.Generic;
using UnityEngine;

namespace ROC.Game.Inventory
{
    [CreateAssetMenu(
        fileName = "ItemDefinition",
        menuName = "ROC/Inventory/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string itemId = "new_item";
        [SerializeField] private string displayName = "New Item";

        [TextArea]
        [SerializeField] private string description;

        [Header("Economy")]
        [SerializeField, Min(0)] private int baseValue;

        [Header("Stacking")]
        [SerializeField] private bool isStackable;
        [SerializeField, Min(1)] private int maxStack = 1;

        [Header("Equipment")]
        [SerializeField] private bool isEquippable;

        [Header("Sorting / Rules")]
        [SerializeField] private List<string> tags = new();

        [Header("Future Appearance")]
        [Tooltip("Reserved for later vanity/appearance override systems.")]
        [SerializeField] private string defaultAppearanceId;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public int BaseValue => baseValue;
        public bool IsStackable => isStackable;
        public int MaxStack => isStackable ? Mathf.Max(1, maxStack) : 1;
        public bool IsEquippable => isEquippable;
        public IReadOnlyList<string> Tags => tags;
        public string DefaultAppearanceId => defaultAppearanceId;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemId = "new_item";
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }

            maxStack = Mathf.Max(1, maxStack);

            if (!isStackable)
            {
                maxStack = 1;
            }

            baseValue = Mathf.Max(0, baseValue);
        }
#endif
    }
}