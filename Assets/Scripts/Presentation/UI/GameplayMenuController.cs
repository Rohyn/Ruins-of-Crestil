using System.Collections;
using ROC.Networking.Characters;
using ROC.Presentation.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace ROC.Presentation.UI
{
    [DisallowMultipleComponent]
    public sealed class GameplayMenuController : MonoBehaviour
    {
        private enum GameplayMenuTab
        {
            Inventory = 0,
            Journal = 1,
            Map = 2,
            Social = 3
        }

        [Header("Root")]
        [SerializeField] private GameObject menuRoot;

        [Header("Panels")]
        [SerializeField] private InventoryPanelController inventoryPanel;
        [SerializeField] private GameObject journalPanel;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject socialPanel;

        [Header("Ribbon Buttons")]
        [SerializeField] private Button inventoryButton;
        [SerializeField] private Button journalButton;
        [SerializeField] private Button mapButton;
        [SerializeField] private Button socialButton;

        [Header("Close")]
        [SerializeField] private Button closeButton;

        [Header("Startup")]
        [SerializeField] private GameplayMenuTab defaultTab = GameplayMenuTab.Inventory;

        private GameplayMenuTab _lastViewedTab;
        private bool _hasOpenedBefore;
        private Coroutine _deferredShowCoroutine;

        private void Awake()
        {
            _lastViewedTab = defaultTab;
            ResolveReferences();

            inventoryButton?.onClick.AddListener(ShowInventoryTab);
            journalButton?.onClick.AddListener(ShowJournalTab);
            mapButton?.onClick.AddListener(ShowMapTab);
            socialButton?.onClick.AddListener(ShowSocialTab);
            closeButton?.onClick.AddListener(CloseMenu);

            ApplyMode(PlayerLookController.Local != null
                ? PlayerLookController.Local.CurrentCursorMode
                : PlayerLookController.CursorModeState.TemporaryFreeCursor);
        }

        private void OnEnable()
        {
            PlayerLookController.LocalCursorModeChanged += ApplyMode;
        }

        private void OnDisable()
        {
            PlayerLookController.LocalCursorModeChanged -= ApplyMode;

            if (_deferredShowCoroutine != null)
            {
                StopCoroutine(_deferredShowCoroutine);
                _deferredShowCoroutine = null;
            }
        }

        private void ResolveReferences()
        {
            if (menuRoot == null)
            {
                menuRoot = gameObject;
            }

            if (inventoryPanel == null)
            {
                inventoryPanel = GetComponentInChildren<InventoryPanelController>(true);
            }
        }

        private void ApplyMode(PlayerLookController.CursorModeState mode)
        {
            ResolveReferences();

            bool shouldShow = mode == PlayerLookController.CursorModeState.MenuCursor;

            if (menuRoot != null)
            {
                menuRoot.SetActive(shouldShow);
            }

            if (_deferredShowCoroutine != null)
            {
                StopCoroutine(_deferredShowCoroutine);
                _deferredShowCoroutine = null;
            }

            if (shouldShow)
            {
                GameplayMenuTab tabToShow = _hasOpenedBefore ? _lastViewedTab : defaultTab;
                _hasOpenedBefore = true;

                _deferredShowCoroutine = StartCoroutine(ShowTabDeferred(tabToShow));
            }
            else
            {
                HideAllPanels();
            }
        }

        private IEnumerator ShowTabDeferred(GameplayMenuTab tab)
        {
            yield return null;

            ResolveReferences();
            ShowTab(tab);

            _deferredShowCoroutine = null;
        }

        public void ShowInventoryTab()
        {
            ShowTab(GameplayMenuTab.Inventory);
        }

        public void ShowJournalTab()
        {
            ShowTab(GameplayMenuTab.Journal);
        }

        public void ShowMapTab()
        {
            ShowTab(GameplayMenuTab.Map);
        }

        public void ShowSocialTab()
        {
            ShowTab(GameplayMenuTab.Social);
        }

        private void ShowTab(GameplayMenuTab tab)
        {
            ResolveReferences();

            _lastViewedTab = tab;

            if (menuRoot != null && !menuRoot.activeSelf)
            {
                menuRoot.SetActive(true);
            }

            HideAllPanels();

            switch (tab)
            {
                case GameplayMenuTab.Inventory:
                    inventoryPanel?.Show();
                    break;

                case GameplayMenuTab.Journal:
                    if (journalPanel != null)
                    {
                        journalPanel.SetActive(true);
                    }

                    break;

                case GameplayMenuTab.Map:
                    if (mapPanel != null)
                    {
                        mapPanel.SetActive(true);
                    }

                    break;

                case GameplayMenuTab.Social:
                    if (socialPanel != null)
                    {
                        socialPanel.SetActive(true);
                    }

                    break;
            }
        }

        private void HideAllPanels()
        {
            inventoryPanel?.Hide();

            if (journalPanel != null)
            {
                journalPanel.SetActive(false);
            }

            if (mapPanel != null)
            {
                mapPanel.SetActive(false);
            }

            if (socialPanel != null)
            {
                socialPanel.SetActive(false);
            }
        }

        private static void CloseMenu()
        {
            if (PlayerLookController.Local != null)
            {
                PlayerLookController.Local.SetCursorMode(PlayerLookController.CursorModeState.GameplayLocked);
            }
        }
    }
}