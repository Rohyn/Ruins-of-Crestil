using System.Collections.Generic;
using ROC.Game.Inventory;
using ROC.Networking.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ROC.Presentation.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanelView : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Labels")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private string titleText = "Inventory";

        [Header("List Containers")]
        [SerializeField] private Transform equippedListContainer;
        [SerializeField] private Transform bagListContainer;

        [Header("Row Prefab")]
        [SerializeField] private InventoryItemRowView rowPrefab;

        [Header("Empty Labels")]
        [SerializeField] private TMP_Text equippedEmptyLabel;
        [SerializeField] private TMP_Text bagEmptyLabel;
        [SerializeField] private string emptyEquippedText = "Nothing equipped.";
        [SerializeField] private string emptyBagText = "Bags empty.";

        private readonly List<GameObject> _spawnedRows = new();

        private ClientInventoryState _boundInventory;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (titleLabel != null)
            {
                titleLabel.text = titleText;
            }

            Hide();
        }

        public void Show()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        public bool IsVisible()
        {
            return panelRoot != null && panelRoot.activeSelf;
        }

        public void RenderInventory(ClientInventoryState inventory)
        {
            _boundInventory = inventory;

            ClearSpawnedRows();

            if (_boundInventory == null)
            {
                SetEmptyLabels(true, true);
                ForceRebuild();
                return;
            }

            int equippedRows = BuildList(
                InventoryLocationKind.Equipped,
                equippedListContainer,
                equippedEmptyLabel,
                emptyEquippedText);

            int bagRows = BuildList(
                InventoryLocationKind.Bag,
                bagListContainer,
                bagEmptyLabel,
                emptyBagText);

            SetEmptyLabel(equippedEmptyLabel, equippedRows <= 0, emptyEquippedText);
            SetEmptyLabel(bagEmptyLabel, bagRows <= 0, emptyBagText);

            ForceRebuild();
        }

        private int BuildList(
            InventoryLocationKind location,
            Transform container,
            TMP_Text emptyLabel,
            string emptyText)
        {
            if (container == null || rowPrefab == null || _boundInventory == null)
            {
                SetEmptyLabel(emptyLabel, true, emptyText);
                return 0;
            }

            int entryCount = _boundInventory.GetEntryCount(location);
            int rowsBuilt = 0;

            for (int i = 0; i < entryCount; i++)
            {
                if (!_boundInventory.TryGetDisplayInfoAt(
                        i,
                        location,
                        out string itemInstanceId,
                        out string displayName,
                        out int quantity,
                        out bool isStackable,
                        out bool isEquippable))
                {
                    continue;
                }

                InventoryItemRowView row = Instantiate(rowPrefab, container);
                row.gameObject.SetActive(true);

                row.Bind(
                    itemInstanceId,
                    displayName,
                    quantity,
                    isStackable,
                    isEquippable,
                    location,
                    HandleRowRightClick);

                _spawnedRows.Add(row.gameObject);
                rowsBuilt++;
            }

            return rowsBuilt;
        }

        private void HandleRowRightClick(
            string itemInstanceId,
            InventoryLocationKind location,
            bool isEquippable)
        {
            if (_boundInventory == null || string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return;
            }

            switch (location)
            {
                case InventoryLocationKind.Bag:
                    if (isEquippable)
                    {
                        _boundInventory.RequestEquipItem(itemInstanceId);
                    }

                    break;

                case InventoryLocationKind.Equipped:
                    _boundInventory.RequestUnequipItem(itemInstanceId);
                    break;
            }
        }

        private void ClearSpawnedRows()
        {
            for (int i = _spawnedRows.Count - 1; i >= 0; i--)
            {
                if (_spawnedRows[i] != null)
                {
                    Destroy(_spawnedRows[i]);
                }
            }

            _spawnedRows.Clear();
        }

        private void SetEmptyLabels(bool equippedEmpty, bool bagEmpty)
        {
            SetEmptyLabel(equippedEmptyLabel, equippedEmpty, emptyEquippedText);
            SetEmptyLabel(bagEmptyLabel, bagEmpty, emptyBagText);
        }

        private static void SetEmptyLabel(TMP_Text label, bool visible, string text)
        {
            if (label == null)
            {
                return;
            }

            label.gameObject.SetActive(visible);

            if (visible)
            {
                label.text = text;
            }
        }

        private void ForceRebuild()
        {
            RebuildTransform(equippedListContainer);
            RebuildTransform(bagListContainer);

            if (panelRoot != null)
            {
                RectTransform rootRect = panelRoot.transform as RectTransform;
                if (rootRect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                }
            }

            Canvas.ForceUpdateCanvases();
        }

        private static void RebuildTransform(Transform target)
        {
            RectTransform rect = target as RectTransform;

            if (rect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }
    }
}