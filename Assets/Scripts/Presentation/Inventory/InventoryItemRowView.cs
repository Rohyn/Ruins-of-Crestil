using System;
using ROC.Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ROC.Presentation.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryItemRowView : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text itemNameLabel;
        [SerializeField] private TMP_Text quantityLabel;

        private string _itemInstanceId;
        private InventoryLocationKind _location;
        private bool _isEquippable;
        private Action<string, InventoryLocationKind, bool> _onRightClick;

        public void Bind(
            string itemInstanceId,
            string displayName,
            int quantity,
            bool isStackable,
            bool isEquippable,
            InventoryLocationKind location,
            Action<string, InventoryLocationKind, bool> onRightClick)
        {
            _itemInstanceId = itemInstanceId;
            _location = location;
            _isEquippable = isEquippable;
            _onRightClick = onRightClick;

            if (itemNameLabel != null)
            {
                itemNameLabel.text = displayName;
            }

            if (quantityLabel != null)
            {
                quantityLabel.text = isStackable && quantity > 1
                    ? $"x{quantity}"
                    : string.Empty;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                _onRightClick?.Invoke(_itemInstanceId, _location, _isEquippable);
            }
        }
    }
}