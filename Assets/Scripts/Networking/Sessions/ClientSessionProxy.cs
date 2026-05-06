using System;
using System.Collections.Generic;
using ROC.Game.Sessions;
using ROC.Game.Common;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Sessions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ClientSessionProxy : NetworkBehaviour
    {
        public static ClientSessionProxy Local { get; private set; }
        public static event Action<ClientSessionProxy> LocalSessionReady;

        public event Action<IReadOnlyList<CharacterSummaryNet>> CharacterListReceived;
        public event Action<string> StatusReceived;

        private readonly List<CharacterSummaryNet> _receivedCharacters = new(capacity: 3);

        private string _selectedCharacterId;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                string accountId = LocalAccountStub.GetAccountId(OwnerClientId);

                PlayerSessionRegistry.Instance?.RegisterSessionProxy(
                    OwnerClientId,
                    NetworkObject,
                    accountId);
            }

            if (IsOwner)
            {
                Local = this;
                LocalSessionReady?.Invoke(this);
                RequestCharacterList();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                PlayerSessionRegistry.Instance?.RemoveClient(OwnerClientId);
            }

            if (Local == this)
            {
                Local = null;
            }
        }

        public void RequestCharacterList()
        {
            if (!IsOwner)
            {
                return;
            }

            RequestCharacterListServerRpc();
        }

        public void SelectCharacter(string characterId)
        {
            if (!IsOwner || string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }

            SelectCharacterServerRpc(new FixedString64Bytes(characterId));
        }

        [ServerRpc]
        private void RequestCharacterListServerRpc(ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;
            string accountId = LocalAccountStub.GetAccountId(clientId);

            ICharacterRepository repository =
                ServerRepositoryProvider.Instance != null
                    ? ServerRepositoryProvider.Instance.CharacterRepository
                    : LocalCharacterRepository.Instance;
            if (repository == null)
            {
                SendStatusToClient(clientId, "Server missing LocalCharacterRepository.");
                return;
            }

            IReadOnlyList<PersistentCharacterRecord> records = repository.GetCharactersForAccount(accountId);

            CharacterSummaryNet slot0 = default;
            CharacterSummaryNet slot1 = default;
            CharacterSummaryNet slot2 = default;

            byte count = (byte)Mathf.Min(records.Count, 3);

            if (count > 0)
            {
                slot0 = ToSummary(records[0]);
            }

            if (count > 1)
            {
                slot1 = ToSummary(records[1]);
            }

            if (count > 2)
            {
                slot2 = ToSummary(records[2]);
            }

            ReceiveCharacterListClientRpc(
                count,
                slot0,
                slot1,
                slot2,
                TargetClient(clientId));
        }

        [ServerRpc]
        private void SelectCharacterServerRpc(FixedString64Bytes characterId, ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;

            if (clientId != OwnerClientId)
            {
                SendStatusToClient(clientId, "Rejected character selection: ownership mismatch.");
                return;
            }

            string accountId = LocalAccountStub.GetAccountId(clientId);
            string selectedCharacterId = characterId.ToString();

            ICharacterRepository repository =
                ServerRepositoryProvider.Instance != null
                    ? ServerRepositoryProvider.Instance.CharacterRepository
                    : LocalCharacterRepository.Instance;
            if (repository == null)
            {
                SendStatusToClient(clientId, "Server missing LocalCharacterRepository.");
                return;
            }

            if (!repository.TryGetCharacter(accountId, selectedCharacterId, out PersistentCharacterRecord character))
            {
                SendStatusToClient(clientId, "Rejected character selection: character not found.");
                return;
            }

            _selectedCharacterId = selectedCharacterId;

            GameSessionManager sessionManager = GameSessionManager.Instance;
            if (sessionManager == null)
            {
                SendStatusToClient(clientId, "Server missing GameSessionManager.");
                return;
            }

            SendStatusToClient(clientId, $"Entering game as {character.DisplayName}...");
            ServerActionResult selectionResult =
                PlayerSessionRegistry.Instance != null
                    ? PlayerSessionRegistry.Instance.SetSelectedCharacter(
                        clientId,
                        character.CharacterId,
                        character.DisplayName)
                    : ServerActionResult.Ok();

            if (!selectionResult.Success)
            {
                SendStatusToClient(clientId, selectionResult.Message);
                return;
            }
            sessionManager.EnterGame(clientId, character);
        }

        [ClientRpc]
        private void ReceiveCharacterListClientRpc(
            byte count,
            CharacterSummaryNet slot0,
            CharacterSummaryNet slot1,
            CharacterSummaryNet slot2,
            ClientRpcParams clientRpcParams = default)
        {
            _receivedCharacters.Clear();

            if (count > 0)
            {
                _receivedCharacters.Add(slot0);
            }

            if (count > 1)
            {
                _receivedCharacters.Add(slot1);
            }

            if (count > 2)
            {
                _receivedCharacters.Add(slot2);
            }

            CharacterListReceived?.Invoke(_receivedCharacters);
            StatusReceived?.Invoke($"Loaded {_receivedCharacters.Count} character(s).");
        }

        [ClientRpc]
        private void ReceiveStatusClientRpc(FixedString128Bytes message, ClientRpcParams clientRpcParams = default)
        {
            StatusReceived?.Invoke(message.ToString());
        }

        private static CharacterSummaryNet ToSummary(PersistentCharacterRecord character)
        {
            return new CharacterSummaryNet(
                character.CharacterId,
                character.DisplayName,
                character.HasCompletedIntro,
                character.CurrentLocation.SceneId,
                character.CurrentLocation.InstanceId);
        }

        private void SendStatusToClient(ulong clientId, string message)
        {
            ReceiveStatusClientRpc(new FixedString128Bytes(message), TargetClient(clientId));
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