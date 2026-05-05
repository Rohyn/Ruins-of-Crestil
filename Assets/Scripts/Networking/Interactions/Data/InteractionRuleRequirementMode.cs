namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// Common have/lack requirement mode used by many interaction rules.
    /// </summary>
    public enum InteractionRuleRequirementMode : byte
    {
        MustHave = 0,
        MustNotHave = 1
    }
}
