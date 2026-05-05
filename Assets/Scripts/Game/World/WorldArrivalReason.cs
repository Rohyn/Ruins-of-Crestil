using System;

namespace ROC.Game.World
{
    public enum WorldArrivalReason : byte
    {
        Unknown = 0,
        InitialCharacterSpawn = 1,
        SavedLocationResume = 2,
        DefaultWorldFallback = 3,
        IntroCompletion = 4,
        SceneTransition = 5,
        ForcedRelocation = 6,
        Respawn = 7,
        AdminMove = 8
    }

    [Flags]
    public enum WorldArrivalReasonFlags
    {
        None = 0,
        Unknown = 1 << 0,
        InitialCharacterSpawn = 1 << 1,
        SavedLocationResume = 1 << 2,
        DefaultWorldFallback = 1 << 3,
        IntroCompletion = 1 << 4,
        SceneTransition = 1 << 5,
        ForcedRelocation = 1 << 6,
        Respawn = 1 << 7,
        AdminMove = 1 << 8,

        All =
            Unknown |
            InitialCharacterSpawn |
            SavedLocationResume |
            DefaultWorldFallback |
            IntroCompletion |
            SceneTransition |
            ForcedRelocation |
            Respawn |
            AdminMove
    }

    public static class WorldArrivalReasonExtensions
    {
        public static WorldArrivalReasonFlags ToFlag(this WorldArrivalReason reason)
        {
            switch (reason)
            {
                case WorldArrivalReason.InitialCharacterSpawn:
                    return WorldArrivalReasonFlags.InitialCharacterSpawn;
                case WorldArrivalReason.SavedLocationResume:
                    return WorldArrivalReasonFlags.SavedLocationResume;
                case WorldArrivalReason.DefaultWorldFallback:
                    return WorldArrivalReasonFlags.DefaultWorldFallback;
                case WorldArrivalReason.IntroCompletion:
                    return WorldArrivalReasonFlags.IntroCompletion;
                case WorldArrivalReason.SceneTransition:
                    return WorldArrivalReasonFlags.SceneTransition;
                case WorldArrivalReason.ForcedRelocation:
                    return WorldArrivalReasonFlags.ForcedRelocation;
                case WorldArrivalReason.Respawn:
                    return WorldArrivalReasonFlags.Respawn;
                case WorldArrivalReason.AdminMove:
                    return WorldArrivalReasonFlags.AdminMove;
                default:
                    return WorldArrivalReasonFlags.Unknown;
            }
        }
    }
}
