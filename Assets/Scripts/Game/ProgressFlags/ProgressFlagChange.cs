namespace ROC.Game.ProgressFlags
{
    public readonly struct ProgressFlagChange
    {
        public readonly ulong ClientId;
        public readonly string CharacterId;
        public readonly string FlagIdOrPrefix;
        public readonly ProgressFlagChangeKind ChangeKind;
        public readonly ProgressFlagLifetime Lifetime;
        public readonly string Source;

        public ProgressFlagChange(
            ulong clientId,
            string characterId,
            string flagIdOrPrefix,
            ProgressFlagChangeKind changeKind,
            ProgressFlagLifetime lifetime,
            string source)
        {
            ClientId = clientId;
            CharacterId = characterId ?? string.Empty;
            FlagIdOrPrefix = flagIdOrPrefix ?? string.Empty;
            ChangeKind = changeKind;
            Lifetime = lifetime;
            Source = source ?? string.Empty;
        }
    }
}