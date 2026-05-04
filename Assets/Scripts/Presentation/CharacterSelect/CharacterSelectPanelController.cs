using System.Collections.Generic;
using ROC.Networking.Sessions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ROC.Presentation.CharacterSelect
{
    [DisallowMultipleComponent]
    public sealed class CharacterSelectPanelController : MonoBehaviour
    {
        [Header("Character Slots")]
        [SerializeField] private Button[] characterButtons;
        [SerializeField] private TMP_Text[] characterLabels;

        [Header("Actions")]
        [SerializeField] private Button refreshButton;

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;

        private readonly CharacterSummaryNet[] _characters = new CharacterSummaryNet[3];

        private ClientSessionProxy _session;

        private void Awake()
        {
            if (refreshButton != null)
            {
                refreshButton.onClick.AddListener(Refresh);
            }

            WireCharacterButtons();
            ClearSlots();
        }

        private void OnEnable()
        {
            ClientSessionProxy.LocalSessionReady += BindSession;

            if (ClientSessionProxy.Local != null)
            {
                BindSession(ClientSessionProxy.Local);
                Refresh();
            }
            else
            {
                SetStatus("Waiting for session...");
                ClearSlots();
            }
        }

        private void OnDisable()
        {
            ClientSessionProxy.LocalSessionReady -= BindSession;
            UnbindSession();
        }

        public void Refresh()
        {
            if (_session == null)
            {
                SetStatus("No local session available.");
                ClearSlots();
                return;
            }

            SetStatus("Requesting characters...");
            _session.RequestCharacterList();
        }

        private void BindSession(ClientSessionProxy session)
        {
            if (_session == session)
            {
                return;
            }

            UnbindSession();

            _session = session;
            _session.CharacterListReceived += HandleCharacterListReceived;
            _session.StatusReceived += SetStatus;

            SetStatus("Session ready.");
        }

        private void UnbindSession()
        {
            if (_session == null)
            {
                return;
            }

            _session.CharacterListReceived -= HandleCharacterListReceived;
            _session.StatusReceived -= SetStatus;
            _session = null;
        }

        private void WireCharacterButtons()
        {
            if (characterButtons == null)
            {
                return;
            }

            for (int i = 0; i < characterButtons.Length; i++)
            {
                int index = i;
                Button button = characterButtons[i];

                if (button != null)
                {
                    button.onClick.AddListener(() => SelectCharacter(index));
                }
            }
        }

        private void HandleCharacterListReceived(IReadOnlyList<CharacterSummaryNet> summaries)
        {
            ClearSlots();

            int count = Mathf.Min(summaries.Count, _characters.Length);

            for (int i = 0; i < count; i++)
            {
                _characters[i] = summaries[i];

                if (i < characterButtons.Length && characterButtons[i] != null)
                {
                    characterButtons[i].interactable = true;
                    characterButtons[i].gameObject.SetActive(true);
                }

                if (i < characterLabels.Length && characterLabels[i] != null)
                {
                    string stateLabel = summaries[i].HasCompletedIntro
                        ? $"Location: {summaries[i].SceneId}"
                        : "Intro Required";

                    characterLabels[i].text = $"{summaries[i].DisplayName}\n{stateLabel}";
                }
            }

            SetStatus(count == 0 ? "No characters found." : "Select a character.");
        }

        private void SelectCharacter(int index)
        {
            if (_session == null)
            {
                SetStatus("No local session available.");
                return;
            }

            if (index < 0 || index >= _characters.Length)
            {
                return;
            }

            string characterId = _characters[index].CharacterId.ToString();

            if (string.IsNullOrWhiteSpace(characterId))
            {
                SetStatus("Empty character slot.");
                return;
            }

            SetStatus("Selecting character...");
            _session.SelectCharacter(characterId);
        }

        private void ClearSlots()
        {
            if (characterButtons != null)
            {
                for (int i = 0; i < characterButtons.Length; i++)
                {
                    if (characterButtons[i] != null)
                    {
                        characterButtons[i].interactable = false;
                        characterButtons[i].gameObject.SetActive(true);
                    }
                }
            }

            if (characterLabels != null)
            {
                for (int i = 0; i < characterLabels.Length; i++)
                {
                    if (characterLabels[i] != null)
                    {
                        characterLabels[i].text = "Empty";
                    }
                }
            }

            for (int i = 0; i < _characters.Length; i++)
            {
                _characters[i] = default;
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }

            Debug.Log($"[CharacterSelect] {message}");
        }
    }
}