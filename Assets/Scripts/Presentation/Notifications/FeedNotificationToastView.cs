using TMPro;
using UnityEngine;

namespace ROC.Presentation.Notifications
{
    /// <summary>
    /// Pure TMP UI view for one lower-priority feed notification group.
    /// Keep this object active; visibility is controlled with CanvasGroup alpha.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FeedNotificationToastView : MonoBehaviour
    {
        [Header("Visibility")]
        [SerializeField] private GameObject rootObject;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Text")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;

        private void Awake()
        {
            if (rootObject == null)
            {
                rootObject = gameObject;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            Hide();
        }

        public void Show(string title, string body)
        {
            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
                titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(title));
            }

            if (bodyText != null)
            {
                bodyText.text = body ?? string.Empty;
                bodyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(body));
            }

            SetVisible(true);

            if (rootObject != null)
            {
                rootObject.transform.SetAsLastSibling();
            }
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (rootObject != null && !rootObject.activeSelf)
            {
                // Do not force inactive UI roots active at edit-time. If the root starts inactive,
                // the controller cannot find this view. Keep the view GameObject active in the HUD.
            }
        }
    }
}
