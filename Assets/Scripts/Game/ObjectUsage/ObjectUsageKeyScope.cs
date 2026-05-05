namespace ROC.Game.ObjectUsage
{
    /// <summary>
    /// Controls how an authored object usage key is generated when no explicit override is supplied.
    /// </summary>
    public enum ObjectUsageKeyScope : byte
    {
        /// <summary>
        /// One usage record for this stable object ID regardless of scene or instance.
        /// Use sparingly, mostly for unique global authored objects.
        /// </summary>
        StableObjectOnly = 0,

        /// <summary>
        /// One usage record for this scene ID + stable object ID.
        /// Recommended default for authored world objects such as the intro wardrobe.
        /// </summary>
        SceneAndStableObject = 1,

        /// <summary>
        /// One usage record for this runtime instance ID + stable object ID.
        /// Use for temporary/procedural instances that should clean up or reset independently.
        /// </summary>
        InstanceAndStableObject = 2,

        /// <summary>
        /// Do not generate a key. An explicit usage key override is required.
        /// </summary>
        CustomOnly = 3
    }
}
