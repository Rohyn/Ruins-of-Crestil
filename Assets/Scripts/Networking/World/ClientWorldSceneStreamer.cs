using System;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ROC.Networking.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ClientWorldSceneStreamer : NetworkBehaviour
    {
        public static event Action<ulong, string, string> ServerClientWorldSceneReady;

        public static event Action<string, string> LocalWorldSceneStreamStarted;
        public static event Action<string, string> LocalWorldSceneReady;
        public static event Action LocalWorldSceneCleared;

        private string _currentWorldUnitySceneName;
        private string _currentSceneId;
        private string _currentInstanceId;
        private bool _isStreaming;

        public void StreamClientToWorldScene(
            ulong targetClientId,
            string sceneId,
            string unitySceneName,
            string instanceId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[ClientWorldSceneStreamer] StreamClientToWorldScene called outside server context.");
                return;
            }

            StreamWorldSceneClientRpc(
                new FixedString64Bytes(sceneId),
                new FixedString64Bytes(unitySceneName),
                new FixedString128Bytes(instanceId),
                TargetClient(targetClientId));
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                LocalWorldSceneCleared?.Invoke();
            }
        }

        [ClientRpc]
        private void StreamWorldSceneClientRpc(
            FixedString64Bytes sceneId,
            FixedString64Bytes unitySceneName,
            FixedString128Bytes instanceId,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            if (_isStreaming)
            {
                Debug.LogWarning("[ClientWorldSceneStreamer] Already streaming a world scene.");
                return;
            }

            StartCoroutine(StreamRoutine(
                sceneId.ToString(),
                unitySceneName.ToString(),
                instanceId.ToString()));
        }

        private IEnumerator StreamRoutine(string sceneId, string unitySceneName, string instanceId)
        {
            _isStreaming = true;

            LocalWorldSceneStreamStarted?.Invoke(sceneId, instanceId);

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                _currentSceneId = sceneId;
                _currentInstanceId = instanceId;
                _currentWorldUnitySceneName = unitySceneName;

                LocalWorldSceneReady?.Invoke(sceneId, instanceId);

                ConfirmWorldSceneReadyServerRpc(
                    new FixedString64Bytes(sceneId),
                    new FixedString128Bytes(instanceId));

                _isStreaming = false;
                yield break;
            }

            string previousWorldScene = _currentWorldUnitySceneName;

            Scene targetScene = SceneManager.GetSceneByName(unitySceneName);

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(unitySceneName, LoadSceneMode.Additive);

                if (load == null)
                {
                    Debug.LogError($"[ClientWorldSceneStreamer] Failed to start loading {unitySceneName}.");
                    _isStreaming = false;
                    yield break;
                }

                while (!load.isDone)
                {
                    yield return null;
                }

                targetScene = SceneManager.GetSceneByName(unitySceneName);
            }

            if (targetScene.IsValid() && targetScene.isLoaded)
            {
                SceneManager.SetActiveScene(targetScene);
            }

            _currentSceneId = sceneId;
            _currentInstanceId = instanceId;
            _currentWorldUnitySceneName = unitySceneName;

            if (!string.IsNullOrWhiteSpace(previousWorldScene) &&
                previousWorldScene != unitySceneName)
            {
                Scene previousScene = SceneManager.GetSceneByName(previousWorldScene);

                if (previousScene.IsValid() && previousScene.isLoaded)
                {
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(previousScene);

                    if (unload != null)
                    {
                        while (!unload.isDone)
                        {
                            yield return null;
                        }
                    }
                }
            }

            LocalWorldSceneReady?.Invoke(sceneId, instanceId);

            ConfirmWorldSceneReadyServerRpc(
                new FixedString64Bytes(sceneId),
                new FixedString128Bytes(instanceId));

            _isStreaming = false;
        }

        [ServerRpc]
        private void ConfirmWorldSceneReadyServerRpc(
            FixedString64Bytes sceneId,
            FixedString128Bytes instanceId,
            ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;
            ServerClientWorldSceneReady?.Invoke(clientId, sceneId.ToString(), instanceId.ToString());
        }

        private static ClientRpcParams TargetClient(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
        }
    }
}