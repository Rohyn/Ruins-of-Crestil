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

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private GameplayMenuTab _lastViewedTab;
        private bool _hasOpenedBefore;
        private Coroutine _openRoutine;

        private void Awake()
        {
            _lastViewedTab = defaultTab;

            ResolveReferences();

            inventoryButton?.onClick.AddListener(ShowInventoryTab);
            journalButton?.onClick.AddListener(ShowJournalTab);
            mapButton?.onClick.AddListener(ShowMapTab);
            socialButton?.onClick.AddListener(ShowSocialTab);
            closeButton?.onClick.AddListener(RequestCloseMenu);

            CloseMenuVisualOnly();
        }

        private void OnEnable()
        {
            PlayerLookController.LocalGameplayMenuOpened += HandleGameplayMenuOpened;
            PlayerLookController.LocalGameplayMenuClosed += HandleGameplayMenuClosed;
        }

        private void OnDisable()
        {
            PlayerLookController.LocalGameplayMenuOpened -= HandleGameplayMenuOpened;
            PlayerLookController.LocalGameplayMenuClosed -= HandleGameplayMenuClosed;

            if (_openRoutine != null)
            {
                StopCoroutine(_openRoutine);
                _openRoutine = null;
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

        private void HandleGameplayMenuOpened()
        {
            ResolveReferences();

            GameplayMenuTab tabToShow = _hasOpenedBefore ? _lastViewedTab : defaultTab;
            _hasOpenedBefore = true;

            if (menuRoot != null)
            {
                menuRoot.SetActive(true);
            }

            if (_openRoutine != null)
            {
                StopCoroutine(_openRoutine);
            }

            _openRoutine = StartCoroutine(OpenMenuRoutine(tabToShow));
        }

        private IEnumerator OpenMenuRoutine(GameplayMenuTab tabToShow)
        {
            // One-frame defer lets inactive child panel components finish Awake/OnEnable
            // after GameplayMenuRoot is activated.
            yield return null;

            ShowTab(tabToShow);
            _openRoutine = null;
        }

        private void HandleGameplayMenuClosed()
        {
            CloseMenuVisualOnly();
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

            if (verboseLogging)
            {
                Debug.Log($"[GameplayMenuController] Showing tab: {tab}");
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

        private void CloseMenuVisualOnly()
        {
            if (_openRoutine != null)
            {
                StopCoroutine(_openRoutine);
                _openRoutine = null;
            }

            HideAllPanels();

            if (menuRoot != null)
            {
                menuRoot.SetActive(false);
            }
        }

        private static void RequestCloseMenu()
        {
            if (PlayerLookController.Local != null)
            {
                PlayerLookController.Local.SetCursorMode(PlayerLookController.CursorModeState.GameplayLocked);
            }
        }
    }
}