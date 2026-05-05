using ROC.Networking.Inventory;
using UnityEngine;

namespace ROC.Presentation.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryPanelView panelView;

        [Header("Binding")]
        [SerializeField, Min(0.05f)] private float searchIntervalSeconds = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private ClientInventoryState _boundInventory;
        private float _nextSearchTime;
        private bool _wantsVisible;

        private void Awake()
        {
            ResolveReferences();

            // Do not hide here.
            // If this object starts inactive, Awake may run during the first Show() call.
            // Hiding here causes the "second click required" behavior.
        }

        private void OnEnable()
        {
            ClientInventoryState.LocalInventoryReady += BindInventory;

            ResolveReferences();

            if (ClientInventoryState.Local != null)
            {
                BindInventory(ClientInventoryState.Local);
            }

            if (_wantsVisible && _boundInventory != null)
            {
                panelView?.RenderInventory(_boundInventory);
            }
        }

        private void OnDisable()
        {
            ClientInventoryState.LocalInventoryReady -= BindInventory;
            UnbindInventory();
        }

        private void Update()
        {
            if (_boundInventory == null && Time.time >= _nextSearchTime)
            {
                _nextSearchTime = Time.time + searchIntervalSeconds;

                if (ClientInventoryState.Local != null)
                {
                    BindInventory(ClientInventoryState.Local);
                }
            }
        }

        public void Show()
        {
            _wantsVisible = true;

            ResolveReferences();

            if (_boundInventory == null && ClientInventoryState.Local != null)
            {
                BindInventory(ClientInventoryState.Local);
            }

            panelView?.Show();

            if (_boundInventory != null)
            {
                _boundInventory.RequestInventorySnapshot();
            }

            panelView?.RenderInventory(_boundInventory);

            if (verboseLogging)
            {
                Debug.Log("[InventoryPanelController] Inventory panel shown.");
            }
        }

        public void Hide()
        {
            _wantsVisible = false;
            panelView?.Hide();
        }

        public bool IsVisible()
        {
            return panelView != null && panelView.IsVisible();
        }

        private void ResolveReferences()
        {
            if (panelView == null)
            {
                panelView = GetComponent<InventoryPanelView>();
            }

            if (panelView == null)
            {
                panelView = GetComponentInChildren<InventoryPanelView>(true);
            }
        }

        private void BindInventory(ClientInventoryState inventory)
        {
            if (inventory == null || inventory == _boundInventory)
            {
                return;
            }

            UnbindInventory();

            _boundInventory = inventory;
            _boundInventory.InventorySnapshotChanged += HandleSnapshotChanged;
            _boundInventory.RequestInventorySnapshot();

            if (verboseLogging)
            {
                Debug.Log("[InventoryPanelController] Bound local ClientInventoryState.");
            }

            if (_wantsVisible)
            {
                panelView?.RenderInventory(_boundInventory);
            }
        }

        private void UnbindInventory()
        {
            if (_boundInventory != null)
            {
                _boundInventory.InventorySnapshotChanged -= HandleSnapshotChanged;
                _boundInventory = null;
            }
        }

        private void HandleSnapshotChanged()
        {
            if (_wantsVisible)
            {
                panelView?.RenderInventory(_boundInventory);
            }
        }
    }
}