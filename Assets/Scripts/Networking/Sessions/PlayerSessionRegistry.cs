using System;
using System.Collections.Generic;
using ROC.Game.Common;
using ROC.Game.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Sessions
{
    [DisallowMultipleComponent]
    public sealed class PlayerSessionRegistry : MonoBehaviour
    {
        public static PlayerSessionRegistry Instance { get; private set; }

        public event Action<PlayerSessionData> SessionChanged;
        public event Action<ulong> SessionRemoved;

        [SerializeField] private bool verboseLogging;

        private readonly Dictionary<ulong, PlayerSessionData> _sessionsByClientId = new();
        private readonly Dictionary<string, ulong> _clientIdByCharacterId = new(StringComparer.Ordinal);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public ServerActionResult RegisterConnectedClient(ulong clientId, string accountId)
        {
            PlayerSessionData session = GetOrCreate(clientId);

            if (!string.IsNullOrWhiteSpace(accountId))
            {
                session.AccountId = accountId;
            }

            if (session.State == PlayerSessionState.Disconnected)
            {
                session.State = PlayerSessionState.Connected;
            }

            NotifyChanged(session);
            return ServerActionResult.Ok();
        }

        public ServerActionResult RegisterSessionProxy(
            ulong clientId,
            NetworkObject sessionProxy,
            string accountId)
        {
            if (sessionProxy == null)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoSessionProxy,
                    "Cannot register a null session proxy.");
            }

            PlayerSessionData session = GetOrCreate(clientId);

            session.AccountId = accountId ?? string.Empty;
            session.SessionProxyObject = sessionProxy;
            session.State = PlayerSessionState.CharacterSelect;

            NotifyChanged(session);

            if (verboseLogging)
            {
                Debug.Log($"[PlayerSessionRegistry] Registered session proxy for client {clientId}.");
            }

            return ServerActionResult.Ok();
        }

        public ServerActionResult SetSelectedCharacter(
            ulong clientId,
            string characterId,
            string displayName)
        {
            if (!_sessionsByClientId.TryGetValue(clientId, out PlayerSessionData session))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoSession,
                    $"No session exists for client {clientId}.");
            }

            if (string.IsNullOrWhiteSpace(characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidRequest,
                    "CharacterId is empty.");
            }

            if (_clientIdByCharacterId.TryGetValue(characterId, out ulong existingClientId) &&
                existingClientId != clientId)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.CharacterAlreadyOnline,
                    "That character is already online.");
            }

            if (!string.IsNullOrWhiteSpace(session.CharacterId))
            {
                _clientIdByCharacterId.Remove(session.CharacterId);
            }

            session.CharacterId = characterId;
            session.DisplayName = displayName ?? string.Empty;
            session.State = PlayerSessionState.CharacterSelect;

            _clientIdByCharacterId[characterId] = clientId;

            NotifyChanged(session);
            return ServerActionResult.Ok();
        }

        public ServerActionResult SetLoadingWorld(ulong clientId, WorldLocation targetLocation)
        {
            if (!_sessionsByClientId.TryGetValue(clientId, out PlayerSessionData session))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoSession,
                    $"No session exists for client {clientId}.");
            }

            if (!session.HasSelectedCharacter)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot load world before a character is selected.");
            }

            session.CurrentLocation = targetLocation;
            session.SceneId = targetLocation.SceneId ?? string.Empty;
            session.InstanceId = targetLocation.InstanceId ?? string.Empty;
            session.AvatarObject = null;
            session.State = PlayerSessionState.LoadingWorld;

            NotifyChanged(session);
            return ServerActionResult.Ok();
        }

        public ServerActionResult SetInWorld(
            ulong clientId,
            WorldLocation location,
            NetworkObject avatarObject)
        {
            if (!_sessionsByClientId.TryGetValue(clientId, out PlayerSessionData session))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoSession,
                    $"No session exists for client {clientId}.");
            }

            if (avatarObject == null || !avatarObject.IsSpawned)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidTarget,
                    "Cannot enter world with a null or unspawned avatar.");
            }

            session.CurrentLocation = location;
            session.SceneId = location.SceneId ?? string.Empty;
            session.InstanceId = location.InstanceId ?? string.Empty;
            session.AvatarObject = avatarObject;
            session.State = PlayerSessionState.InWorld;

            NotifyChanged(session);
            return ServerActionResult.Ok();
        }

        public void ClearAvatar(ulong clientId)
        {
            if (!_sessionsByClientId.TryGetValue(clientId, out PlayerSessionData session))
            {
                return;
            }

            session.AvatarObject = null;

            if (session.State == PlayerSessionState.InWorld)
            {
                session.State = PlayerSessionState.LoadingWorld;
            }

            NotifyChanged(session);
        }

        public void RemoveClient(ulong clientId)
        {
            if (!_sessionsByClientId.TryGetValue(clientId, out PlayerSessionData session))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(session.CharacterId))
            {
                _clientIdByCharacterId.Remove(session.CharacterId);
            }

            session.State = PlayerSessionState.Disconnecting;
            NotifyChanged(session);

            _sessionsByClientId.Remove(clientId);

            SessionRemoved?.Invoke(clientId);

            if (verboseLogging)
            {
                Debug.Log($"[PlayerSessionRegistry] Removed client {clientId}.");
            }
        }

        public bool TryGet(ulong clientId, out PlayerSessionData session)
        {
            return _sessionsByClientId.TryGetValue(clientId, out session);
        }

        public bool TryGetAccountId(ulong clientId, out string accountId)
        {
            if (_sessionsByClientId.TryGetValue(clientId, out PlayerSessionData session))
            {
                accountId = session.AccountId;
                return !string.IsNullOrWhiteSpace(accountId);
            }

            accountId = string.Empty;
            return false;
        }

        public bool TryGetCharacterId(ulong clientId, out string characterId)
        {
            if (_sessionsByClientId.TryGetValue(clientId, out PlayerSessionData session))
            {
                characterId = session.CharacterId;
                return !string.IsNullOrWhiteSpace(characterId);
            }

            characterId = string.Empty;
            return false;
        }

        public bool TryGetAvatarObject(ulong clientId, out NetworkObject avatarObject)
        {
            if (_sessionsByClientId.TryGetValue(clientId, out PlayerSessionData session) &&
                session.AvatarObject != null &&
                session.AvatarObject.IsSpawned)
            {
                avatarObject = session.AvatarObject;
                return true;
            }

            avatarObject = null;
            return false;
        }

        public bool TryGetInstanceId(ulong clientId, out string instanceId)
        {
            if (_sessionsByClientId.TryGetValue(clientId, out PlayerSessionData session) &&
                !string.IsNullOrWhiteSpace(session.InstanceId))
            {
                instanceId = session.InstanceId;
                return true;
            }

            instanceId = string.Empty;
            return false;
        }

        public void GetSessionsNonAlloc(List<PlayerSessionData> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            foreach (PlayerSessionData session in _sessionsByClientId.Values)
            {
                results.Add(session);
            }
        }

        private PlayerSessionData GetOrCreate(ulong clientId)
        {
            if (_sessionsByClientId.TryGetValue(clientId, out PlayerSessionData session))
            {
                return session;
            }

            session = new PlayerSessionData(clientId);
            _sessionsByClientId.Add(clientId, session);
            return session;
        }

        private void NotifyChanged(PlayerSessionData session)
        {
            SessionChanged?.Invoke(session);

            if (verboseLogging)
            {
                Debug.Log(
                    $"[PlayerSessionRegistry] Client={session.ClientId}, State={session.State}, " +
                    $"Account={session.AccountId}, Character={session.CharacterId}, Instance={session.InstanceId}");
            }
        }
    }
}