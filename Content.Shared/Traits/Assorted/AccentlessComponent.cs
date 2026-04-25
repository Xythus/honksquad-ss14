using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
// HONK START - #634: species-aware Accentless needs SpeciesPrototype.
using Content.Shared.Humanoid.Prototypes;
// HONK END

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// This is used for the accentless trait
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AccentlessComponent : Component
{
    // HONK START - #634: required dropped so trait yaml can provide speciesEffects instead of removes.
    /// <summary>
    ///     The accents removed by the accentless trait.
    /// </summary>
    [DataField("removes"), ViewVariables(VVAccess.ReadWrite)]
    public ComponentRegistry RemovedAccents = new();

    /// <summary>
    ///     Per-species rules for what Accentless strips on that species. Keyed by SpeciesPrototype id.
    ///     If the wearer's species has no entry here, Accentless does nothing (on top of RemovedAccents).
    ///     Used by player-facing Accentless. NPC entity prototypes typically use RemovedAccents.
    /// </summary>
    [DataField("speciesEffects"), ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<ProtoId<SpeciesPrototype>, AccentlessSpeciesEffect> SpeciesEffects = new();
    // HONK END
}

// HONK START - #634
[DataDefinition]
public sealed partial class AccentlessSpeciesEffect
{
    /// <summary>
    ///     Component types on the wearer to RemComp. Same semantics as AccentlessComponent.RemovedAccents.
    /// </summary>
    [DataField("strips")]
    public ComponentRegistry Strips = new();

    /// <summary>
    ///     ReplacementAccentPrototype ids to remove from ReplacementAccentComponent.Accents on the wearer.
    ///     String-typed because ReplacementAccentPrototype lives in Content.Server; AccentlessSystem (Shared)
    ///     passes the list over to the Server-side handler that touches the list.
    /// </summary>
    [DataField("stripsReplacementAccents")]
    public List<string> StripsReplacementAccents = new();
}
// HONK END
