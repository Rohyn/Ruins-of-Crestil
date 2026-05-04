using System;

namespace ROC.Networking.Conditions
{
    [Flags]
    public enum PlayerControlBlockFlags : byte
    {
        None = 0,
        Movement = 1 << 0,
        Jump = 1 << 1,
        Sprint = 1 << 2,
        Gravity = 1 << 3
    }
}