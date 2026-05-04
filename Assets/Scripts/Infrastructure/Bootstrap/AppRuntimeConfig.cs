using System;
using UnityEngine;

namespace ROC.Infrastructure.Bootstrap
{
    public enum RuntimeEnvironment
    {
        Local = 0,
        Dev = 1,
        Staging = 2,
        Production = 3
    }

    public enum LaunchMode
    {
        Manual = 0,
        Client = 1,
        Server = 2,
        Host = 3
    }

    [Serializable]
    public sealed class AppRuntimeConfig
    {
        [Header("Runtime")]
        public RuntimeEnvironment Environment = RuntimeEnvironment.Local;
        public LaunchMode LaunchMode = LaunchMode.Manual;

        [Header("Networking")]
        [Tooltip("Address the client connects to. Use 127.0.0.1 for local dedicated-server testing.")]
        public string ConnectAddress = "127.0.0.1";

        [Tooltip("Address the server listens on. 0.0.0.0 means all local interfaces.")]
        public string ListenAddress = "0.0.0.0";

        [Min(1)]
        public ushort Port = 7777;

        [Header("Development")]
        public bool VerboseLogging = true;

        public AppRuntimeConfig Clone()
        {
            return new AppRuntimeConfig
            {
                Environment = Environment,
                LaunchMode = LaunchMode,
                ConnectAddress = ConnectAddress,
                ListenAddress = ListenAddress,
                Port = Port,
                VerboseLogging = VerboseLogging
            };
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(ConnectAddress))
            {
                ConnectAddress = "127.0.0.1";
            }

            if (string.IsNullOrWhiteSpace(ListenAddress))
            {
                ListenAddress = "0.0.0.0";
            }

            if (Port == 0)
            {
                Port = 7777;
            }
        }
    }
}