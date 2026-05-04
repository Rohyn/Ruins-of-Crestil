namespace ROC.Game.Common
{
    public enum ServerActionErrorCode : ushort
    {
        None = 0,

        Unknown = 1,
        NotImplemented = 2,

        NotConnected = 100,
        NoSession = 101,
        NoSessionProxy = 102,
        NoCharacterSelected = 103,
        CharacterAlreadyOnline = 104,
        NotInWorld = 105,

        InvalidRequest = 200,
        InvalidTarget = 201,
        InvalidState = 202,
        PermissionDenied = 203,

        DifferentInstance = 300,
        TargetNotVisible = 301,
        TooFarAway = 302,

        CooldownActive = 400,
        MissingResource = 401,
        AlreadyCompleted = 402,
        Depleted = 403,

        PersistenceUnavailable = 500,
        PersistenceFailed = 501
    }
}