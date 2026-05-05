using ROC.Game.Common;

namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// Structured result for interaction rule evaluation.
    /// It can be converted to ServerActionResult for existing server interaction plumbing while keeping
    /// future prompt/notification identifiers separate from debug text.
    /// </summary>
    public readonly struct InteractionRuleResult
    {
        public readonly bool Passed;
        public readonly ServerActionErrorCode ErrorCode;
        public readonly string FailureReasonId;
        public readonly string UserMessageId;
        public readonly string DebugMessage;

        private InteractionRuleResult(
            bool passed,
            ServerActionErrorCode errorCode,
            string failureReasonId,
            string userMessageId,
            string debugMessage)
        {
            Passed = passed;
            ErrorCode = errorCode;
            FailureReasonId = failureReasonId ?? string.Empty;
            UserMessageId = userMessageId ?? string.Empty;
            DebugMessage = debugMessage ?? string.Empty;
        }

        public static InteractionRuleResult Pass()
        {
            return new InteractionRuleResult(true, ServerActionErrorCode.None, string.Empty, string.Empty, string.Empty);
        }

        public static InteractionRuleResult Fail(
            ServerActionErrorCode errorCode,
            string debugMessage,
            string failureReasonId = "",
            string userMessageId = "")
        {
            return new InteractionRuleResult(
                false,
                errorCode == ServerActionErrorCode.None ? ServerActionErrorCode.InvalidState : errorCode,
                failureReasonId,
                userMessageId,
                debugMessage);
        }

        public ServerActionResult ToServerActionResult()
        {
            return Passed
                ? ServerActionResult.Ok()
                : ServerActionResult.Fail(ErrorCode, DebugMessage);
        }

        public override string ToString()
        {
            if (Passed)
            {
                return "Passed";
            }

            return string.IsNullOrWhiteSpace(FailureReasonId)
                ? $"{ErrorCode}: {DebugMessage}"
                : $"{ErrorCode}: {FailureReasonId} - {DebugMessage}";
        }
    }
}
