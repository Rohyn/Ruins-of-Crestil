namespace ROC.Game.Conditions
{
    public enum ConditionEffectType : byte
    {
        BlockMovement = 0,
        BlockJump = 1,
        BlockSprint = 2,
        BlockGravity = 3,

        // Reserved for later.
        MovementSpeedMultiplier = 20,
        SuppressConditionEffectsById = 100,
        SuppressConditionEffectsByTag = 101
    }
}