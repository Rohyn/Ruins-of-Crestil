namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// Controls how an InteractionExecutor treats validation/execution failure for an action slot.
    /// </summary>
    public enum InteractionActionFailureBehavior : byte
    {
        Required = 0,
        Optional = 1
    }
}
