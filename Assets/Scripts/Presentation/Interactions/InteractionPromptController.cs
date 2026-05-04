using ROC.Networking.Interactions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ROC.Presentation.Interactions
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InteractionPromptView promptView;

        [Header("World Tracking")]
        [SerializeField] private Vector3 worldOffset = new(0f, 0.2f, 0f);

        [Header("Display")]
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private string promptSeparator = " - ";

        [Header("Selector Binding")]
        [SerializeField, Min(0.05f)] private float selectorSearchIntervalSeconds = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private PlayerInteractionSelector _boundSelector;
        private NetworkInteractableTarget _currentTarget;
        private float _nextSelectorSearchTime;

        private void Awake()
        {
            if (promptView == null)
            {
                promptView = FindFirstObjectByType<InteractionPromptView>();
            }

            if (promptView != null)
            {
                promptView.Hide();
            }
        }

        private void OnEnable()
        {
            PlayerInteractionSelector.LocalSelectorReady += BindSelector;

            if (PlayerInteractionSelector.Local != null)
            {
                BindSelector(PlayerInteractionSelector.Local);
            }
        }

        private void OnDisable()
        {
            PlayerInteractionSelector.LocalSelectorReady -= BindSelector;
            UnbindSelector();

            if (promptView != null)
            {
                promptView.Hide();
            }
        }

        private void Update()
        {
            if (_boundSelector == null)
            {
                if (Time.time >= _nextSelectorSearchTime)
                {
                    TryBindLocalSelector();
                    _nextSelectorSearchTime = Time.time + selectorSearchIntervalSeconds;
                }

                promptView?.Hide();
                return;
            }

            if (_currentTarget == null)
            {
                promptView?.Hide();
                return;
            }

            UpdatePromptPosition();
        }

        private void TryBindLocalSelector()
        {
            if (PlayerInteractionSelector.Local != null)
            {
                BindSelector(PlayerInteractionSelector.Local);
            }
        }

        private void BindSelector(PlayerInteractionSelector selector)
        {
            if (selector == null || selector == _boundSelector)
            {
                return;
            }

            UnbindSelector();

            _boundSelector = selector;
            _boundSelector.CurrentTargetChanged += HandleCurrentTargetChanged;
            _currentTarget = _boundSelector.CurrentTarget;

            if (verboseLogging)
            {
                Debug.Log("[InteractionPromptController] Bound to local PlayerInteractionSelector.");
            }

            RefreshPromptImmediate();
        }

        private void UnbindSelector()
        {
            if (_boundSelector != null)
            {
                _boundSelector.CurrentTargetChanged -= HandleCurrentTargetChanged;
                _boundSelector = null;
            }

            _currentTarget = null;
        }

        private void HandleCurrentTargetChanged(NetworkInteractableTarget newTarget)
        {
            _currentTarget = newTarget;

            if (verboseLogging)
            {
                string targetName = _currentTarget != null ? _currentTarget.name : "null";
                Debug.Log($"[InteractionPromptController] Current target changed to '{targetName}'.");
            }

            RefreshPromptImmediate();
        }

        private void RefreshPromptImmediate()
        {
            if (promptView == null)
            {
                return;
            }

            if (_currentTarget == null)
            {
                promptView.Hide();
                return;
            }

            promptView.Show(BuildPromptText());
            UpdatePromptPosition();
        }

        private string BuildPromptText()
        {
            string keyText = interactKey.ToString();
            string interactionText = _currentTarget != null
                ? _currentTarget.InteractionPrompt
                : string.Empty;

            return $"{keyText}{promptSeparator}{interactionText}";
        }

        private void UpdatePromptPosition()
        {
            if (promptView == null || _currentTarget == null)
            {
                return;
            }

            UnityEngine.Camera worldCamera = UnityEngine.Camera.main;

            if (worldCamera == null)
            {
                promptView.Hide();
                return;
            }

            Vector3 referencePosition = _boundSelector != null
                ? _boundSelector.transform.position
                : worldCamera.transform.position;

            Vector3 worldPosition =
                _currentTarget.GetBestInteractionFocusPosition(referencePosition) + worldOffset;

            Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);

            if (screenPosition.z <= 0f)
            {
                promptView.Hide();
                return;
            }

            UnityEngine.Camera uiCamera = null;
            Canvas canvas = promptView.GetRootCanvas();

            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = canvas.worldCamera != null ? canvas.worldCamera : worldCamera;
            }

            promptView.SetScreenPosition((Vector2)screenPosition, uiCamera);
        }
    }
}