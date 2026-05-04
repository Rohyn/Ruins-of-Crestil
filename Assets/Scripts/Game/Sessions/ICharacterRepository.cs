using System.Collections.Generic;
using ROC.Game.World;

namespace ROC.Game.Sessions
{
    public interface ICharacterRepository
    {
        IReadOnlyList<PersistentCharacterRecord> GetCharactersForAccount(string accountId);

        bool TryGetCharacter(
            string accountId,
            string characterId,
            out PersistentCharacterRecord character);

        bool MarkIntroComplete(
            string accountId,
            string characterId,
            WorldLocation sharedWorldLocation);
    }
}