using ROC.Networking.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace ROC.Presentation.UI
{
    [DisallowMultipleComponent]
    public sealed class GameplayMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseMenu);
            }

            ApplyMode(PlayerLookController.Local != null
                ? PlayerLookController.Local.CurrentCursorMode
                : PlayerLookController.CursorModeState.TemporaryFreeCursor);
        }

        private void OnEnable()
        {
            PlayerLookController.LocalCursorModeChanged += ApplyMode;
        }

        private void OnDisable()
        {
            PlayerLookController.LocalCursorModeChanged -= ApplyMode;
        }

        private void ApplyMode(PlayerLookController.CursorModeState mode)
        {
            if (menuRoot == null)
            {
                return;
            }

            menuRoot.SetActive(mode == PlayerLookController.CursorModeState.MenuCursor);
        }

        private static void CloseMenu()
        {
            if (PlayerLookController.Local != null)
            {
                PlayerLookController.Local.SetCursorMode(PlayerLookController.CursorModeState.GameplayLocked);
            }
        }
    }
}