namespace ROC.Networking.Sessions
{
    public enum PlayerSessionState : byte
    {
        Disconnected = 0,
        Connected = 1,
        CharacterSelect = 2,
        LoadingWorld = 3,
        InWorld = 4,
        Disconnecting = 5
    }
}