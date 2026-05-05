namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// Controls how an InteractionRuleSetDefinition combines its enabled rule entries.
    /// </summary>
    public enum InteractionRuleEvaluationMode : byte
    {
        All = 0,
        Any = 1
    }
}
