using System;
using UnityEngine;

namespace ROC.Infrastructure.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class AppRoot : MonoBehaviour
    {
        public static AppRoot Instance { get; private set; }

        [SerializeField] private AppRuntimeConfig runtimeConfig = new();
        [SerializeField] private NetcodeBootstrapper netcodeBootstrapper;

        public AppRuntimeConfig RuntimeConfig => runtimeConfig;
        public NetcodeBootstrapper Network => netcodeBootstrapper;

        private bool _startedFromLaunchMode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (netcodeBootstrapper == null)
            {
                netcodeBootstrapper = GetComponent<NetcodeBootstrapper>();
            }

            ApplyCommandLineOverrides(runtimeConfig);
            runtimeConfig.Normalize();

            if (netcodeBootstrapper != null)
            {
                netcodeBootstrapper.Initialize(runtimeConfig);
            }
            else
            {
                Debug.LogError("[AppRoot] Missing NetcodeBootstrapper component.");
            }
        }

        private void Start()
        {
            StartConfiguredLaunchMode();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool StartClient(string address, ushort port, out string error)
        {
            runtimeConfig.ConnectAddress = address;
            runtimeConfig.Port = port;
            runtimeConfig.Normalize();

            return netcodeBootstrapper.StartClient(runtimeConfig, out error);
        }

        public bool StartServer(ushort port, out string error)
        {
            runtimeConfig.Port = port;
            runtimeConfig.Normalize();

            return netcodeBootstrapper.StartServer(runtimeConfig, out error);
        }

        public bool StartHost(string address, ushort port, out string error)
        {
            runtimeConfig.ConnectAddress = address;
            runtimeConfig.Port = port;
            runtimeConfig.Normalize();

            return netcodeBootstrapper.StartHost(runtimeConfig, out error);
        }

        public void ShutdownNetwork()
        {
            netcodeBootstrapper.Shutdown();
        }

        private void StartConfiguredLaunchMode()
        {
            if (_startedFromLaunchMode || netcodeBootstrapper == null)
            {
                return;
            }

            _startedFromLaunchMode = true;

            string error;
            switch (runtimeConfig.LaunchMode)
            {
                case LaunchMode.Server:
                    if (!netcodeBootstrapper.StartServer(runtimeConfig, out error))
                    {
                        Debug.LogError($"[AppRoot] Failed to start server: {error}");
                    }
                    break;

                case LaunchMode.Client:
                    if (!netcodeBootstrapper.StartClient(runtimeConfig, out error))
                    {
                        Debug.LogError($"[AppRoot] Failed to start client: {error}");
                    }
                    break;

                case LaunchMode.Host:
                    if (!netcodeBootstrapper.StartHost(runtimeConfig, out error))
                    {
                        Debug.LogError($"[AppRoot] Failed to start host: {error}");
                    }
                    break;

                case LaunchMode.Manual:
                default:
                    break;
            }
        }

        private static void ApplyCommandLineOverrides(AppRuntimeConfig config)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (EqualsArg(arg, "-server"))
                {
                    config.LaunchMode = LaunchMode.Server;
                    continue;
                }

                if (EqualsArg(arg, "-client"))
                {
                    config.LaunchMode = LaunchMode.Client;
                    continue;
                }

                if (EqualsArg(arg, "-host"))
                {
                    config.LaunchMode = LaunchMode.Host;
                    continue;
                }

                if (EqualsArg(arg, "-mode") && TryReadNext(args, i, out string mode))
                {
                    if (Enum.TryParse(mode, true, out LaunchMode launchMode))
                    {
                        config.LaunchMode = launchMode;
                    }

                    i++;
                    continue;
                }

                if (EqualsArg(arg, "-env") && TryReadNext(args, i, out string environment))
                {
                    if (Enum.TryParse(environment, true, out RuntimeEnvironment runtimeEnvironment))
                    {
                        config.Environment = runtimeEnvironment;
                    }

                    i++;
                    continue;
                }

                if ((EqualsArg(arg, "-address") || EqualsArg(arg, "-connectAddress")) &&
                    TryReadNext(args, i, out string address))
                {
                    config.ConnectAddress = address;
                    i++;
                    continue;
                }

                if (EqualsArg(arg, "-listenAddress") && TryReadNext(args, i, out string listenAddress))
                {
                    config.ListenAddress = listenAddress;
                    i++;
                    continue;
                }

                if (EqualsArg(arg, "-port") && TryReadNext(args, i, out string portText))
                {
                    if (ushort.TryParse(portText, out ushort port) && port > 0)
                    {
                        config.Port = port;
                    }

                    i++;
                }
            }
        }

        private static bool TryReadNext(string[] args, int currentIndex, out string value)
        {
            int nextIndex = currentIndex + 1;

            if (nextIndex >= args.Length)
            {
                value = string.Empty;
                return false;
            }

            value = args[nextIndex];
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool EqualsArg(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}