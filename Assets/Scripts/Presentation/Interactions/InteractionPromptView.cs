using TMPro;
using UnityEngine;

namespace ROC.Presentation.Interactions
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private TMP_Text promptText;

        [Header("Positioning")]
        [SerializeField] private Vector2 screenOffset = new(0f, 40f);

        [Header("Visibility")]
        [SerializeField] private bool useCanvasGroupIfPresent = true;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();

            if (promptText == null)
            {
                promptText = GetComponent<TMP_Text>();
            }

            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
            }

            if (useCanvasGroupIfPresent)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            Hide();
        }

        public void Show(string displayText)
        {
            if (promptText != null)
            {
                promptText.text = displayText;
            }

            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void SetScreenPosition(Vector2 screenPoint, UnityEngine.Camera worldCamera)
        {
            if (_rectTransform == null || rootCanvas == null)
            {
                return;
            }

            RectTransform canvasRect = rootCanvas.transform as RectTransform;

            if (canvasRect == null)
            {
                return;
            }

            UnityEngine.Camera uiCamera = null;

            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = rootCanvas.worldCamera != null
                    ? rootCanvas.worldCamera
                    : worldCamera;
            }

            Vector2 adjustedPoint = screenPoint + screenOffset;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    adjustedPoint,
                    uiCamera,
                    out Vector2 localPoint))
            {
                _rectTransform.anchoredPosition = localPoint;
            }
        }

        public Canvas GetRootCanvas()
        {
            return rootCanvas;
        }

        private void SetVisible(bool isVisible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = isVisible ? 1f : 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
                return;
            }

            gameObject.SetActive(isVisible);
        }
    }
}