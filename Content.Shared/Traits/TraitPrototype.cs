using Content.Shared.Roles;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
// HONK START - #634: species-aware trait visibility + per-species description overrides.
using Content.Shared.Humanoid.Prototypes;
// HONK END

namespace Content.Shared.Traits;

/// <summary>
/// Describes a trait.
/// </summary>
[Prototype]
public sealed partial class TraitPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The name of this trait.
    /// </summary>
    [DataField]
    public LocId Name { get; private set; } = string.Empty;

    /// <summary>
    /// The description of this trait.
    /// </summary>
    [DataField]
    public LocId? Description { get; private set; }

    /// <summary>
    /// Don't apply this trait to entities this whitelist IS NOT valid for.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Don't apply this trait to entities this whitelist IS valid for. (hence, a blacklist)
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// The components that get added to the player, when they pick this trait.
    /// NOTE: When implementing a new trait, it's preferable to add it as a status effect instead if possible.
    /// </summary>
    [DataField]
    [Obsolete("Use JobSpecial instead.")]
    public ComponentRegistry Components { get; private set; } = new();

    /// <summary>
    /// Special effects applied to the player who takes this Trait.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<JobSpecial> Specials { get; private set; } = new();

    /// <summary>
    /// Gear that is given to the player, when they pick this trait.
    /// </summary>
    [DataField]
    public EntProtoId? TraitGear;

    /// <summary>
    /// Trait Price. If negative number, points will be added.
    /// </summary>
    [DataField]
    public int Cost = 0;

    /// <summary>
    /// Adds a trait to a category, allowing you to limit the selection of some traits to the settings of that category.
    /// </summary>
    [DataField]
    public ProtoId<TraitCategoryPrototype>? Category;

    //HONK START - Tag-based quirk exclusion system
    /// <summary>
    /// Domain tags describing what this trait affects (e.g. "sight", "speech").
    /// Other traits can exclude these tags to prevent incompatible combinations.
    /// </summary>
    [DataField]
    public List<string> Tags { get; private set; } = new();

    /// <summary>
    /// Tags that this trait cannot coexist with.
    /// When this trait is selected, any other trait that has a tag in this list is blocked.
    /// </summary>
    [DataField]
    public List<string> ExcludedTags { get; private set; } = new();
    //HONK END

    // HONK START - #634: species-aware trait visibility + per-species description overrides.
    /// <summary>
    /// If set, the trait is only offered in the character editor when the selected species is in this list.
    /// Used to hide traits that have no effect on certain species (e.g. Accentless on Human).
    /// </summary>
    [DataField]
    public List<ProtoId<SpeciesPrototype>>? SpeciesWhitelist;

    /// <summary>
    /// Optional per-species override for the trait's description, shown in the character editor.
    /// Keyed by SpeciesPrototype id. Falls back to <see cref="Description"/> when no entry matches.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SpeciesPrototype>, LocId>? DescriptionOverrides;
    // HONK END
}
