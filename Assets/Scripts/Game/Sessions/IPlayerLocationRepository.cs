using ROC.Game.World;

namespace ROC.Game.Sessions
{
    public interface IPlayerLocationRepository
    {
        bool UpdateCharacterLocation(
            string accountId,
            string characterId,
            WorldLocation location);
    }
}