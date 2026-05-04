namespace ROC.Networking.World
{
    public interface IInstanceVisibilityRule
    {
        bool IsVisibleToClient(ulong clientId);
    }
}