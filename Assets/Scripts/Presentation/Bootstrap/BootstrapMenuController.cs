using ROC.Infrastructure.Bootstrap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ROC.Networking.Sessions;
using ROC.Networking.World;

namespace ROC.Presentation.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class BootstrapMenuController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject bootstrapUiRoot;

        [Header("Panels")]
        [SerializeField] private GameObject brandingPanel;
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject connectingPanel;
        [SerializeField] private GameObject characterSelectPanel;

        [Header("Main Menu")]
        [SerializeField] private TMP_InputField addressInput;
        [SerializeField] private TMP_InputField portInput;
        [SerializeField] private Button startClientButton;
        [SerializeField] private Button startLocalServerButton;
        [SerializeField] private Button startHostButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        [Header("Options")]
        [SerializeField] private Button optionsBackButton;

        [Header("Character Select")]
        [SerializeField] private Button enterWorldButton;
        [SerializeField] private Button characterBackButton;

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float brandingSeconds = 1.5f;

        private AppRoot _appRoot;
        private bool _subscribed;

        private void Awake()
        {
            if (bootstrapUiRoot == null)
            {
                bootstrapUiRoot = gameObject;
            }
            WireButtons();
            ShowOnly(brandingPanel);
        }

        private void Start()
        {
            _appRoot = AppRoot.Instance;

            if (_appRoot == null)
            {
                SetStatus("Missing AppRoot in Bootstrap scene.");
                return;
            }

            SubscribeToNetworkEvents();
            SubscribeToSessionEvents();

            AppRuntimeConfig config = _appRoot.RuntimeConfig;

            if (addressInput != null)
            {
                addressInput.text = config.ConnectAddress;
            }

            if (portInput != null)
            {
                portInput.text = config.Port.ToString();
            }

            if (brandingSeconds <= 0f)
            {
                ShowMainMenu();
            }
            else
            {
                Invoke(nameof(ShowMainMenu), brandingSeconds);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
            UnsubscribeFromSessionEvents();
        }

        private void WireButtons()
        {
            if (startClientButton != null)
            {
                startClientButton.onClick.AddListener(StartClientFromMenu);
            }

            if (startLocalServerButton != null)
            {
                startLocalServerButton.onClick.AddListener(StartLocalServerFromMenu);
            }

            if (startHostButton != null)
            {
                startHostButton.onClick.AddListener(StartHostFromMenu);
            }

            if (optionsButton != null)
            {
                optionsButton.onClick.AddListener(ShowOptions);
            }

            if (optionsBackButton != null)
            {
                optionsBackButton.onClick.AddListener(ShowMainMenu);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(Quit);
            }

            if (enterWorldButton != null)
            {
                enterWorldButton.onClick.AddListener(EnterWorldPlaceholder);
            }

            if (characterBackButton != null)
            {
                characterBackButton.onClick.AddListener(DisconnectAndReturnToMenu);
            }
        }

        private void SubscribeToNetworkEvents()
        {
            if (_subscribed || _appRoot == null || _appRoot.Network == null)
            {
                return;
            }

            _appRoot.Network.StatusChanged += SetStatus;
            _appRoot.Network.ClientConnectedToServer += ShowCharacterSelect;
            _appRoot.Network.ClientDisconnectedFromServer += HandleDisconnected;

            _subscribed = true;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (!_subscribed || _appRoot == null || _appRoot.Network == null)
            {
                return;
            }

            _appRoot.Network.StatusChanged -= SetStatus;
            _appRoot.Network.ClientConnectedToServer -= ShowCharacterSelect;
            _appRoot.Network.ClientDisconnectedFromServer -= HandleDisconnected;

            _subscribed = false;
        }

        private void SubscribeToSessionEvents()
        {
            ClientSessionProxy.LocalSessionReady -= HandleLocalSessionReady;
            ClientSessionProxy.LocalSessionReady += HandleLocalSessionReady;

            ClientWorldSceneStreamer.LocalWorldSceneStreamStarted -= HandleWorldSceneStreamStarted;
            ClientWorldSceneStreamer.LocalWorldSceneStreamStarted += HandleWorldSceneStreamStarted;

            ClientWorldSceneStreamer.LocalWorldSceneCleared -= HandleWorldSceneCleared;
            ClientWorldSceneStreamer.LocalWorldSceneCleared += HandleWorldSceneCleared;

            if (ClientSessionProxy.Local != null)
            {
                HandleLocalSessionReady(ClientSessionProxy.Local);
            }
        }

        private void UnsubscribeFromSessionEvents()
        {
            ClientSessionProxy.LocalSessionReady -= HandleLocalSessionReady;
            ClientWorldSceneStreamer.LocalWorldSceneStreamStarted -= HandleWorldSceneStreamStarted;
            ClientWorldSceneStreamer.LocalWorldSceneCleared -= HandleWorldSceneCleared;
        }

        private void HandleLocalSessionReady(ClientSessionProxy session)
        {
            if (bootstrapUiRoot != null && !bootstrapUiRoot.activeSelf)
            {
                return;
            }

            ShowCharacterSelect();
        }

        private void HandleWorldSceneStreamStarted(string sceneId, string instanceId)
        {
            SetStatus($"Entering {sceneId}...");

            if (bootstrapUiRoot != null)
            {
                bootstrapUiRoot.SetActive(false);
            }
        }

        private void HandleWorldSceneCleared()
        {
            if (bootstrapUiRoot != null)
            {
                bootstrapUiRoot.SetActive(true);
            }

            ShowMainMenu();
        }

        private void StartClientFromMenu()
        {
            if (_appRoot == null)
            {
                SetStatus("Missing AppRoot.");
                return;
            }

            if (!TryReadEndpoint(out string address, out ushort port))
            {
                return;
            }

            ShowOnly(connectingPanel);
            SetStatus($"Connecting to {address}:{port}...");

            if (!_appRoot.StartClient(address, port, out string error))
            {
                SetStatus(error);
                ShowMainMenu();
            }
        }

        private void StartLocalServerFromMenu()
        {
            if (_appRoot == null)
            {
                SetStatus("Missing AppRoot.");
                return;
            }

            if (!TryReadEndpoint(out _, out ushort port))
            {
                return;
            }

            if (!_appRoot.StartServer(port, out string error))
            {
                SetStatus(error);
                return;
            }

            SetStatus($"Local server running on port {port}. Start a separate client and connect to 127.0.0.1:{port}.");
        }

        private void StartHostFromMenu()
        {
            if (_appRoot == null)
            {
                SetStatus("Missing AppRoot.");
                return;
            }

            if (!TryReadEndpoint(out string address, out ushort port))
            {
                return;
            }

            ShowOnly(connectingPanel);
            SetStatus($"Starting host on {address}:{port}...");

            if (!_appRoot.StartHost(address, port, out string error))
            {
                SetStatus(error);
                ShowMainMenu();
            }
        }

        private bool TryReadEndpoint(out string address, out ushort port)
        {
            address = addressInput != null ? addressInput.text.Trim() : "127.0.0.1";

            if (string.IsNullOrWhiteSpace(address))
            {
                address = "127.0.0.1";
            }

            string portText = portInput != null ? portInput.text.Trim() : "7777";

            if (!ushort.TryParse(portText, out port) || port == 0)
            {
                SetStatus("Invalid port. Use a number from 1 to 65535.");
                return false;
            }

            return true;
        }

        private void ShowMainMenu()
        {
            ShowOnly(mainMenuPanel);
            SetStatus("Ready.");
        }

        private void ShowOptions()
        {
            ShowOnly(optionsPanel);
            SetStatus("Options placeholder.");
        }

        private void ShowCharacterSelect()
        {
            ShowOnly(characterSelectPanel);
            SetStatus("Connected. Character select placeholder.");
        }

        private void EnterWorldPlaceholder()
        {
            SetStatus("Enter World placeholder. Next step: server-authoritative character selection and player spawning.");
        }

        private void DisconnectAndReturnToMenu()
        {
            if (_appRoot != null)
            {
                _appRoot.ShutdownNetwork();
            }

            if (bootstrapUiRoot != null)
            {
                bootstrapUiRoot.SetActive(true);
            }

            ShowMainMenu();
        }

        private void HandleDisconnected()
        {
            if (bootstrapUiRoot != null)
            {
                bootstrapUiRoot.SetActive(true);
            }

            ShowMainMenu();
            SetStatus("Disconnected from server.");
        }

        private void ShowOnly(GameObject panel)
        {
            SetActive(brandingPanel, brandingPanel == panel);
            SetActive(mainMenuPanel, mainMenuPanel == panel);
            SetActive(optionsPanel, optionsPanel == panel);
            SetActive(connectingPanel, connectingPanel == panel);
            SetActive(characterSelectPanel, characterSelectPanel == panel);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }

            Debug.Log($"[BootstrapMenu] {message}");
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}