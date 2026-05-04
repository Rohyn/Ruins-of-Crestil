namespace ROC.Game.World
{
    public enum WorldInstanceKind : byte
    {
        PrivateSession = 0,
        PrivateCharacter = 1,
        SharedShard = 2,
        TemporaryGroup = 3
    }
}