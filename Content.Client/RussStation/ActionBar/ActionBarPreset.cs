namespace Content.Client.RussStation.ActionBar;

// HONK Persistent layout snapshot saved to user_data://honk/action_bar_presets.json.
// Schema is versioned so future fields can be added without breaking older files;
// the loader will drop entries with an unknown version rather than guessing.
public sealed class ActionBarPreset
{
    public int SchemaVersion { get; set; } = ActionBarPresetSchema.CurrentVersion;
    public string Name { get; set; } = string.Empty;

    /// <summary>Character (humanoid profile) name this preset was saved against. Empty
    /// means the preset is character-agnostic, which keeps presets saved before the
    /// character-keyed flow loadable on every character.</summary>
    public string CharacterName { get; set; } = string.Empty;

    public int Rows { get; set; }
    public int SlotsPerRow { get; set; }
    public int SlotSpacing { get; set; }
    public bool ShowKeybindLabel { get; set; }
    public bool ShowEmptySlots { get; set; }
    public bool AutoAddActions { get; set; }
    public bool Lock { get; set; }
    public float ButtonBackgroundAlpha { get; set; }

    public float PositionX { get; set; } = -1f;
    public float PositionY { get; set; } = -1f;

    /// <summary>Action prototype IDs in slot order. Empty / null entries are blanks
    /// the player intentionally left unbound at save time.</summary>
    public List<string?> SlotProtoIds { get; set; } = new();

    /// <summary>Parallel to <see cref="SlotProtoIds"/>: emote id (e.g. "Wave") for each
    /// slot that holds an emote action, null otherwise. Emote actions all share the
    /// same `HonkActionEmote` prototype, so the prototype id can't disambiguate which
    /// emote sits in which slot; the emote id stored here does.</summary>
    public List<string?> EmoteIds { get; set; } = new();
}

public static class ActionBarPresetSchema
{
    public const int CurrentVersion = 2;
}
