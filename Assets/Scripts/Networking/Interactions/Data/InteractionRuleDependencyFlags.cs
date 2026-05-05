using System;

namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// Describes which local gameplay mirrors can affect an interaction rule's client-side preview result.
    /// Server-side rule evaluation remains authoritative and is not cached by these flags.
    /// </summary>
    [Flags]
    public enum InteractionRuleDependencyFlags
    {
        None = 0,
        Inventory = 1 << 0,
        Equipment = 1 << 1,
        ProgressFlags = 1 << 2,
        Conditions = 1 << 3,
        ObjectUsage = 1 << 4,
        QuestState = 1 << 5,
        Reputation = 1 << 6,
        Weather = 1 << 7,
        TimeOfDay = 1 << 8,
        WorldPhase = 1 << 9,
        SceneOrInstance = 1 << 10,
        Custom = 1 << 11,
        All = ~0
    }
}
