using System;
using ROC.Networking.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    /// <summary>
    /// Backward-compatible alias for prefabs that already have OnceOnlyInteractable assigned.
    /// New content should use InteractionUsageGate.
    /// </summary>
    [Obsolete("Use InteractionUsageGate instead. This wrapper is kept so existing prefab components do not break.")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkInteractableTarget))]
    [RequireComponent(typeof(NetworkInstanceObject))]
    public sealed class OnceOnlyInteractable : InteractionUsageGate
    {
    }
}
