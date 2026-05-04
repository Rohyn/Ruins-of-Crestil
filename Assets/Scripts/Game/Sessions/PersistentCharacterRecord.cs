using System;
using ROC.Game.World;

namespace ROC.Game.Sessions
{
    [Serializable]
    public sealed class PersistentCharacterRecord
    {
        public string AccountId;
        public string CharacterId;
        public string DisplayName;

        public bool HasCompletedIntro;

        public WorldLocation CurrentLocation;

        public string CreatedUtc;
        public string LastOnlineUtc;
    }
}