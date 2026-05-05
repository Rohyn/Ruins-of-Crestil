namespace ROC.Game.ObjectUsage
{
    /// <summary>
    /// Defines where interaction usage state is stored.
    /// </summary>
    public enum ObjectUsagePersistenceMode : byte
    {
        /// <summary>
        /// Usage exists only while the server process is running.
        /// </summary>
        RuntimeOnly = 0,

        /// <summary>
        /// Usage is stored in IObjectUsageRepository and survives server restarts.
        /// </summary>
        PersistentObjectUsage = 1
    }
}
