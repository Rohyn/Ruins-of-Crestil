using ROC.Networking.Interactions.Data;

namespace ROC.Networking.Interactions
{
    /// <summary>
    /// Optional companion interface for availability-rule components that can declare which local mirrors
    /// should invalidate cached client-side selection preview.
    /// </summary>
    public interface IInteractionAvailabilityDependencyProvider
    {
        InteractionRuleDependencyFlags LocalPreviewDependencies { get; }
    }
}
