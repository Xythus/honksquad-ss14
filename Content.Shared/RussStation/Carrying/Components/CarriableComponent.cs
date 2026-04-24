using Content.Shared.RussStation.Carrying.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Carrying.Components;

/// <summary>
/// Indicates this entity can be fireman carried (permission tag).
/// The active relationship lives on <see cref="BeingCarriedComponent"/>;
/// this component carries no per-carry state, only tuning for third-party
/// interventions against the carry.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedCarryingSystem))]
public sealed partial class CarriableComponent : Component
{
    /// <summary>
    /// Time a third party must stand next to the carrier to pry this entity
    /// free from them.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan InterruptDuration = CarryingConstants.DefaultInterruptDuration;

    /// <summary>
    /// How long the carrier is stunned and knocked down after a successful
    /// third-party intervention. Short by design — it buys the interrupter a
    /// window to act, it isn't an arrest tool.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan InterruptStunDuration = CarryingConstants.DefaultInterruptStunDuration;

    /// <summary>
    /// If true, the interrupter needs at least one free hand to start the
    /// intervention DoAfter. Matches the intuition that you can't pry someone
    /// off a carrier with both hands full.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool InterruptRequiresFreeHand = true;
}
