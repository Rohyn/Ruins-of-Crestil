using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.World
{
    [DisallowMultipleComponent]
    public sealed class InstanceVisibilityService : MonoBehaviour
    {
        public static InstanceVisibilityService Instance { get; private set; }

        private readonly List<NetworkInstanceObject> _objects = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public void Register(NetworkInstanceObject instanceObject)
        {
            if (instanceObject == null)
            {
                return;
            }

            if (!_objects.Contains(instanceObject))
            {
                _objects.Add(instanceObject);
            }

            RefreshObject(instanceObject);
        }

        public void Unregister(NetworkInstanceObject instanceObject)
        {
            _objects.Remove(instanceObject);
        }

        public void RefreshAll()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                NetworkInstanceObject instanceObject = _objects[i];

                if (instanceObject == null)
                {
                    _objects.RemoveAt(i);
                    continue;
                }

                RefreshObject(instanceObject);
            }
        }

        public void RefreshClient(ulong clientId)
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                NetworkInstanceObject instanceObject = _objects[i];

                if (instanceObject == null)
                {
                    _objects.RemoveAt(i);
                    continue;
                }

                RefreshObjectForClient(instanceObject, clientId);
            }
        }

        public void RefreshObject(NetworkInstanceObject instanceObject)
        {
            if (instanceObject == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                RefreshObjectForClient(instanceObject, clientId);
            }
        }

        private static void RefreshObjectForClient(NetworkInstanceObject instanceObject, ulong clientId)
        {
            if (instanceObject == null)
            {
                return;
            }

            NetworkObject netObj = instanceObject.NetworkObject;

            if (netObj == null || !netObj.IsSpawned)
            {
                return;
            }

            bool shouldSee = instanceObject.ShouldBeVisibleTo(clientId);
            bool doesSee = netObj.IsNetworkVisibleTo(clientId);

            if (shouldSee && !doesSee)
            {
                netObj.NetworkShow(clientId);
            }
            else if (!shouldSee && doesSee)
            {
                netObj.NetworkHide(clientId);
            }
        }
    }
}