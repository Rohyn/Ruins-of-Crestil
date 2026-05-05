namespace ROC.Networking.Interactions.Actions
{
    /// <summary>
    /// How AnchorActionDefinition resolves anchor transforms at runtime.
    /// </summary>
    public enum InteractionAnchorResolutionMode : byte
    {
        TargetAnchorProvider = 0,
        TargetTransform = 1
    }
}
