namespace ROC.Game.Sessions
{
    public static class LocalAccountStub
    {
        public static string GetAccountId(ulong clientId)
        {
            return $"local-account-{clientId}";
        }
    }
}