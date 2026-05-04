using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ROC.Infrastructure.Bootstrap
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    [RequireComponent(typeof(UnityTransport))]
    public sealed class NetcodeBootstrapper : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport unityTransport;

        [Header("Connection Approval")]
        [SerializeField, Min(1)] private int maxApprovedClients = 32;

        [Tooltip("Keep false until character select controls spawning.")]
        [SerializeField] private bool createPlayerObjectOnConnect = false;

        public event Action<string> StatusChanged;
        public event Action ServerStarted;
        public event Action ClientStarted;
        public event Action HostStarted;
        public event Action ClientConnectedToServer;
        public event Action ClientDisconnectedFromServer;

        private readonly HashSet<ulong> _approvedClientIds = new();
        private bool _initialized;

        public bool IsListening => networkManager != null && networkManager.IsListening;
        public bool IsServer => networkManager != null && networkManager.IsServer;
        public bool IsClient => networkManager != null && networkManager.IsClient;
        public bool IsHost => networkManager != null && networkManager.IsHost;

        public void Initialize(AppRuntimeConfig config)
        {
            if (_initialized)
            {
                return;
            }

            networkManager = networkManager != null ? networkManager : GetComponent<NetworkManager>();
            unityTransport = unityTransport != null ? unityTransport : GetComponent<UnityTransport>();

            if (networkManager == null)
            {
                Debug.LogError("[NetcodeBootstrapper] Missing NetworkManager.");
                return;
            }

            if (unityTransport == null)
            {
                Debug.LogError("[NetcodeBootstrapper] Missing UnityTransport.");
                return;
            }

            networkManager.NetworkConfig.NetworkTransport = unityTransport;
            networkManager.NetworkConfig.ConnectionApproval = true;

            networkManager.ConnectionApprovalCallback -= ApproveConnection;
            networkManager.ConnectionApprovalCallback += ApproveConnection;

            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientConnectedCallback += HandleClientConnected;

            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;

            config.Normalize();
            ConfigureTransport(config);

            _initialized = true;
            PublishStatus($"Initialized netcode bootstrapper. Environment={config.Environment}, Mode={config.LaunchMode}, Address={config.ConnectAddress}, Port={config.Port}");
        }

        public bool StartServer(AppRuntimeConfig config, out string error)
        {
            error = string.Empty;

            if (!CanStartNetwork(out error))
            {
                return false;
            }

            ConfigureTransport(config);

            bool started = networkManager.StartServer();
            if (!started)
            {
                error = "NetworkManager.StartServer returned false.";
                PublishStatus(error);
                return false;
            }

            PublishStatus($"Server started. ListenAddress={config.ListenAddress}, Port={config.Port}");
            ServerStarted?.Invoke();
            return true;
        }

        public bool StartClient(AppRuntimeConfig config, out string error)
        {
            error = string.Empty;

            if (!CanStartNetwork(out error))
            {
                return false;
            }

            ConfigureTransport(config);

            bool started = networkManager.StartClient();
            if (!started)
            {
                error = "NetworkManager.StartClient returned false.";
                PublishStatus(error);
                return false;
            }

            PublishStatus($"Client started. Connecting to {config.ConnectAddress}:{config.Port}");
            ClientStarted?.Invoke();
            return true;
        }

        public bool StartHost(AppRuntimeConfig config, out string error)
        {
            error = string.Empty;

            if (!CanStartNetwork(out error))
            {
                return false;
            }

            ConfigureTransport(config);

            bool started = networkManager.StartHost();
            if (!started)
            {
                error = "NetworkManager.StartHost returned false.";
                PublishStatus(error);
                return false;
            }

            PublishStatus($"Host started. Address={config.ConnectAddress}, Port={config.Port}");
            HostStarted?.Invoke();
            return true;
        }

        public void Shutdown()
        {
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }

            PublishStatus("Shutting down network.");
            networkManager.Shutdown();
            _approvedClientIds.Clear();
        }

        private bool CanStartNetwork(out string error)
        {
            if (!_initialized)
            {
                error = "NetcodeBootstrapper has not been initialized.";
                return false;
            }

            if (networkManager == null)
            {
                error = "Missing NetworkManager.";
                return false;
            }

            if (unityTransport == null)
            {
                error = "Missing UnityTransport.";
                return false;
            }

            if (networkManager.IsListening)
            {
                error = "NetworkManager is already listening.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void ConfigureTransport(AppRuntimeConfig config)
        {
            config.Normalize();

            unityTransport.SetConnectionData(
                config.ConnectAddress,
                config.Port,
                config.ListenAddress
            );
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            bool serverFull = networkManager.ConnectedClientsIds.Count >= maxApprovedClients;

            response.Approved = !serverFull;
            response.CreatePlayerObject = !serverFull && createPlayerObjectOnConnect;
            response.Pending = false;

            if (serverFull)
            {
                response.Reason = "Server full.";
                PublishStatus($"Rejected client {request.ClientNetworkId}: server full.");
                return;
            }

            _approvedClientIds.Add(request.ClientNetworkId);
            PublishStatus($"Approved client {request.ClientNetworkId}.");
        }

        private void HandleClientConnected(ulong clientId)
        {
            PublishStatus($"Client connected: {clientId}");

            if (networkManager.IsClient && clientId == networkManager.LocalClientId)
            {
                ClientConnectedToServer?.Invoke();
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            _approvedClientIds.Remove(clientId);
            PublishStatus($"Client disconnected: {clientId}");

            if (networkManager.IsClient && clientId == networkManager.LocalClientId)
            {
                ClientDisconnectedFromServer?.Invoke();
            }
        }

        private void PublishStatus(string message)
        {
            Debug.Log($"[NetcodeBootstrapper] {message}");
            StatusChanged?.Invoke(message);
        }

        private void OnDestroy()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ConnectionApprovalCallback -= ApproveConnection;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }
}