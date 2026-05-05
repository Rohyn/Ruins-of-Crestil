using System;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    [Serializable]
    public struct InventoryGrantEntry
    {
        [SerializeField] private string itemDefinitionId;
        [SerializeField, Min(1)] private int quantity;

        public string ItemDefinitionId => itemDefinitionId;
        public int Quantity => Mathf.Max(1, quantity);
    }
}