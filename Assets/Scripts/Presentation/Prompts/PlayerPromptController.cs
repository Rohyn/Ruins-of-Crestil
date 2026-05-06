using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ROC.Presentation.Prompts
{
    /// <summary>
    /// Owner-local prompt queue and display controller. This is presentation-only and does not mutate gameplay state.
    /// Attach to the persistent HUD or ClientSessionProxy presentation object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerPromptController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PromptCatalog promptCatalog;
        [SerializeField] private PromptToastView toastView;

        [Header("Queue Rules")]
        [SerializeField, Min(0f)] private float secondsBetweenPrompts = 0.35f;
        [SerializeField] private bool allowHighPriorityInterrupts = true;
        [SerializeField] private int interruptPriorityDelta = 500;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        private readonly List<QueuedPrompt> _queue = new();
        private readonly HashSet<string> _shownInitialThisSession = new();
        private readonly Dictionary<string, float> _cooldownUntilByPromptId = new();

        private Coroutine _displayRoutine;
        private QueuedPrompt _currentlyDisplayedPrompt;

        public PromptCatalog PromptCatalog => promptCatalog;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (_displayRoutine != null)
            {
                StopCoroutine(_displayRoutine);
                _displayRoutine = null;
            }

            _queue.Clear();
            _currentlyDisplayedPrompt = null;

            if (toastView != null)
            {
                toastView.Hide();
            }
        }

        public bool TryShowPromptById(
            string promptId,
            PromptLineKind lineKind = PromptLineKind.Initial,
            bool force = false,
            bool bypassRepeat = false)
        {
            if (!TryResolvePrompt(promptId, out PromptDefinition prompt))
            {
                if (verboseLogging)
                {
                    Debug.LogWarning($"[PlayerPromptController] Unknown prompt id '{promptId}'.", this);
                }

                return false;
            }

            return TryShowPrompt(prompt, lineKind, force, bypassRepeat);
        }

        public bool TryShowPrompt(
            PromptDefinition prompt,
            PromptLineKind lineKind = PromptLineKind.Initial,
            bool force = false,
            bool bypassRepeat = false)
        {
            if (prompt == null || !prompt.HasUsableText(lineKind))
            {
                return false;
            }

            string promptId = NormalizeId(prompt.PromptId);
            if (string.IsNullOrWhiteSpace(promptId))
            {
                return false;
            }

            if (!force && !CanDisplayPrompt(prompt, lineKind, bypassRepeat))
            {
                return false;
            }

            if (IsPromptAlreadyQueued(promptId, lineKind))
            {
                return false;
            }

            if (_currentlyDisplayedPrompt != null &&
                IdsMatch(_currentlyDisplayedPrompt.PromptId, promptId) &&
                _currentlyDisplayedPrompt.LineKind == lineKind)
            {
                return false;
            }

            int priority = lineKind == PromptLineKind.Reminder ? prompt.ReminderPriority : prompt.Priority;

            if (allowHighPriorityInterrupts &&
                _currentlyDisplayedPrompt != null &&
                priority >= _currentlyDisplayedPrompt.Priority + interruptPriorityDelta)
            {
                InterruptCurrentPrompt();
            }

            _queue.Add(new QueuedPrompt(
                promptId,
                prompt.SpeakerName,
                prompt.GetText(lineKind),
                lineKind,
                priority,
                prompt.DisplaySeconds,
                Time.realtimeSinceStartup,
                prompt.RepeatMode,
                prompt.CooldownSeconds,
                bypassRepeat));

            _queue.Sort(CompareQueuedPrompts);

            if (_displayRoutine == null)
            {
                _displayRoutine = StartCoroutine(DisplayRoutine());
            }

            if (verboseLogging)
            {
                Debug.Log($"[PlayerPromptController] Queued {lineKind} prompt '{promptId}'.", this);
            }

            return true;
        }

        public bool CanDisplayPrompt(
            PromptDefinition prompt,
            PromptLineKind lineKind = PromptLineKind.Initial,
            bool bypassRepeat = false)
        {
            if (prompt == null || !prompt.HasUsableText(lineKind))
            {
                return false;
            }

            string promptId = NormalizeId(prompt.PromptId);
            if (string.IsNullOrWhiteSpace(promptId))
            {
                return false;
            }

            if (!bypassRepeat &&
                lineKind == PromptLineKind.Initial &&
                prompt.RepeatMode == PromptRepeatMode.OncePerSession &&
                _shownInitialThisSession.Contains(promptId))
            {
                return false;
            }

            if (prompt.CooldownSeconds > 0f &&
                _cooldownUntilByPromptId.TryGetValue(promptId, out float cooldownUntil) &&
                Time.realtimeSinceStartup < cooldownUntil)
            {
                return false;
            }

            return true;
        }

        public void ClearQueue()
        {
            _queue.Clear();
        }

        private IEnumerator DisplayRoutine()
        {
            ResolveReferences();

            while (_queue.Count > 0)
            {
                QueuedPrompt prompt = _queue[0];
                _queue.RemoveAt(0);

                if (!prompt.ForceBypassRepeat &&
                    prompt.LineKind == PromptLineKind.Initial &&
                    prompt.RepeatMode == PromptRepeatMode.OncePerSession &&
                    _shownInitialThisSession.Contains(prompt.PromptId))
                {
                    continue;
                }

                _currentlyDisplayedPrompt = prompt;
                MarkPromptDisplayed(prompt);

                if (toastView == null)
                {
                    ResolveReferences();
                }

                if (toastView != null)
                {
                    toastView.Show(prompt.SpeakerName, prompt.Message);
                }
                else
                {
                    Debug.LogWarning("[PlayerPromptController] Cannot show prompt because no PromptToastView is assigned.", this);
                }

                yield return new WaitForSecondsRealtime(prompt.DisplaySeconds);

                if (toastView != null)
                {
                    toastView.Hide();
                }

                _currentlyDisplayedPrompt = null;

                if (secondsBetweenPrompts > 0f)
                {
                    yield return new WaitForSecondsRealtime(secondsBetweenPrompts);
                }
            }

            _displayRoutine = null;
        }

        private void InterruptCurrentPrompt()
        {
            if (_displayRoutine != null)
            {
                StopCoroutine(_displayRoutine);
                _displayRoutine = null;
            }

            _currentlyDisplayedPrompt = null;

            if (toastView != null)
            {
                toastView.Hide();
            }
        }

        private void MarkPromptDisplayed(QueuedPrompt prompt)
        {
            if (prompt == null || string.IsNullOrWhiteSpace(prompt.PromptId))
            {
                return;
            }

            if (prompt.LineKind == PromptLineKind.Initial && prompt.RepeatMode == PromptRepeatMode.OncePerSession)
            {
                _shownInitialThisSession.Add(prompt.PromptId);
            }

            if (prompt.CooldownSeconds > 0f)
            {
                _cooldownUntilByPromptId[prompt.PromptId] = Time.realtimeSinceStartup + prompt.CooldownSeconds;
            }
        }

        private void ResolveReferences()
        {
            if (toastView == null)
            {
                toastView = FindFirstObjectByType<PromptToastView>();
            }
        }

        private bool TryResolvePrompt(string promptId, out PromptDefinition prompt)
        {
            prompt = null;

            if (string.IsNullOrWhiteSpace(promptId) || promptCatalog == null)
            {
                return false;
            }

            return promptCatalog.TryGetDefinition(promptId, out prompt);
        }

        private bool IsPromptAlreadyQueued(string promptId, PromptLineKind lineKind)
        {
            string normalizedPromptId = NormalizeId(promptId);

            for (int i = 0; i < _queue.Count; i++)
            {
                QueuedPrompt queued = _queue[i];
                if (queued == null)
                {
                    continue;
                }

                if (IdsMatch(queued.PromptId, normalizedPromptId) && queued.LineKind == lineKind)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareQueuedPrompts(QueuedPrompt a, QueuedPrompt b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int priorityCompare = b.Priority.CompareTo(a.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            return a.QueuedTime.CompareTo(b.QueuedTime);
        }

        private static bool IdsMatch(string a, string b)
        {
            return string.Equals(NormalizeId(a), NormalizeId(b), System.StringComparison.Ordinal);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private sealed class QueuedPrompt
        {
            public QueuedPrompt(
                string promptId,
                string speakerName,
                string message,
                PromptLineKind lineKind,
                int priority,
                float displaySeconds,
                float queuedTime,
                PromptRepeatMode repeatMode,
                float cooldownSeconds,
                bool forceBypassRepeat)
            {
                PromptId = NormalizeId(promptId);
                SpeakerName = speakerName ?? string.Empty;
                Message = message ?? string.Empty;
                LineKind = lineKind;
                Priority = priority;
                DisplaySeconds = Mathf.Max(0.25f, displaySeconds);
                QueuedTime = queuedTime;
                RepeatMode = repeatMode;
                CooldownSeconds = Mathf.Max(0f, cooldownSeconds);
                ForceBypassRepeat = forceBypassRepeat;
            }

            public string PromptId { get; }
            public string SpeakerName { get; }
            public string Message { get; }
            public PromptLineKind LineKind { get; }
            public int Priority { get; }
            public float DisplaySeconds { get; }
            public float QueuedTime { get; }
            public PromptRepeatMode RepeatMode { get; }
            public float CooldownSeconds { get; }
            public bool ForceBypassRepeat { get; }
        }
    }
}
