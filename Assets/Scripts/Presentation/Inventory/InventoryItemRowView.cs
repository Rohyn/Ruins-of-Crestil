using System;
using ROC.Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ROC.Presentation.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class InventoryItemRowView : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text itemNameLabel;
        [SerializeField] private TMP_Text quantityLabel;

        [Header("Raycast")]
        [SerializeField] private bool ensureRootRaycastTarget = true;
        [SerializeField, Min(1f)] private float minimumRowHeight = 28f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private string _itemInstanceId;
        private InventoryLocationKind _location;
        private bool _isEquippable;
        private Action<string, InventoryLocationKind, bool> _onRightClick;

        private void Awake()
        {
            EnsureRowReceivesPointerEvents();
        }

        private void OnEnable()
        {
            EnsureRowReceivesPointerEvents();
        }

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

            EnsureRowReceivesPointerEvents();

            if (itemNameLabel != null)
            {
                itemNameLabel.text = displayName;
                itemNameLabel.raycastTarget = false;
            }

            if (quantityLabel != null)
            {
                quantityLabel.text = isStackable && quantity > 1
                    ? $"x{quantity}"
                    : string.Empty;

                quantityLabel.raycastTarget = false;
            }

            if (verboseLogging)
            {
                Debug.Log(
                    $"[InventoryItemRowView] Bound row. Instance={_itemInstanceId}, Location={_location}, Equippable={_isEquippable}",
                    this);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (verboseLogging)
            {
                Debug.Log(
                    $"[InventoryItemRowView] PointerDown. Button={eventData.button}, Instance={_itemInstanceId}, Location={_location}, Equippable={_isEquippable}",
                    this);
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                InvokeRightClick();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (verboseLogging)
            {
                Debug.Log(
                    $"[InventoryItemRowView] PointerClick. Button={eventData.button}, Instance={_itemInstanceId}, Location={_location}, Equippable={_isEquippable}",
                    this);
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                InvokeRightClick();
            }
        }

        private void InvokeRightClick()
        {
            if (string.IsNullOrWhiteSpace(_itemInstanceId))
            {
                if (verboseLogging)
                {
                    Debug.LogWarning("[InventoryItemRowView] Right-click ignored because item instance id is empty.", this);
                }

                return;
            }

            _onRightClick?.Invoke(_itemInstanceId, _location, _isEquippable);
        }

        private void EnsureRowReceivesPointerEvents()
        {
            if (!ensureRootRaycastTarget)
            {
                return;
            }

            RectTransform rectTransform = transform as RectTransform;

            if (rectTransform != null && rectTransform.rect.height <= 1f)
            {
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, minimumRowHeight);
            }

            LayoutElement layoutElement = GetComponent<LayoutElement>();

            if (layoutElement == null)
            {
                layoutElement = gameObject.AddComponent<LayoutElement>();
            }

            if (layoutElement.minHeight < minimumRowHeight)
            {
                layoutElement.minHeight = minimumRowHeight;
            }

            if (layoutElement.preferredHeight < minimumRowHeight)
            {
                layoutElement.preferredHeight = minimumRowHeight;
            }

            Graphic graphic = GetComponent<Graphic>();

            if (graphic == null)
            {
                Image image = gameObject.AddComponent<Image>();

                // Not fully transparent. Some UI/raycast setups are more reliable with a tiny alpha.
                image.color = new Color(1f, 1f, 1f, 0.001f);
                image.raycastTarget = true;
            }
            else
            {
                graphic.raycastTarget = true;
            }
        }
    }
}