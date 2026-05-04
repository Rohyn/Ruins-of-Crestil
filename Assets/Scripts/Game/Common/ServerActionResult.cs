namespace ROC.Game.Common
{
    public readonly struct ServerActionResult
    {
        public readonly bool Success;
        public readonly ServerActionErrorCode ErrorCode;
        public readonly string Message;

        private ServerActionResult(bool success, ServerActionErrorCode errorCode, string message)
        {
            Success = success;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
        }

        public static ServerActionResult Ok(string message = "")
        {
            return new ServerActionResult(true, ServerActionErrorCode.None, message);
        }

        public static ServerActionResult Fail(ServerActionErrorCode errorCode, string message)
        {
            return new ServerActionResult(false, errorCode, message);
        }

        public override string ToString()
        {
            return Success
                ? string.IsNullOrWhiteSpace(Message) ? "Success" : $"Success: {Message}"
                : $"{ErrorCode}: {Message}";
        }
    }
}