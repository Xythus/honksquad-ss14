using System.Globalization;
using System.IO;
using System.Linq;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Client.RussStation.ActionBar;

// HONK YAML-backed store for ActionBarPreset entries. Lives inside user_data:// so
// each player keeps their own presets across sessions; the controller owns the
// only instance to avoid pulling in IoC for a per-account file.
public sealed class ActionBarPresetStore
{
    private static readonly ResPath FilePath = new("/honk/action_bar_presets.yml");
    private const string SchemaKey = "schema";

    private readonly IResourceManager _resources;
    private readonly ISawmill _log = Logger.GetSawmill("honk.action_bar.presets");
    private readonly List<ActionBarPreset> _presets = new();
    private bool _loaded;

    public ActionBarPresetStore(IResourceManager resources)
    {
        _resources = resources;
    }

    public IReadOnlyList<ActionBarPreset> Presets
    {
        get
        {
            EnsureLoaded();
            return _presets;
        }
    }

    public event Action? Changed;

    public void Save(ActionBarPreset preset)
    {
        EnsureLoaded();
        // Replace any existing preset with the same name so a re-save updates rather
        // than producing duplicates the user can't tell apart.
        _presets.RemoveAll(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        _presets.Add(preset);
        Persist();
        Changed?.Invoke();
    }

    public bool Delete(string name)
    {
        EnsureLoaded();
        var removed = _presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
            return false;
        Persist();
        Changed?.Invoke();
        return true;
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;
        if (!_resources.UserData.Exists(FilePath))
            return;
        try
        {
            using var reader = _resources.UserData.OpenText(FilePath);
            var stream = new YamlStream();
            stream.Load(reader);
            if (stream.Documents.Count == 0)
                return;
            if (stream.Documents[0].RootNode is not YamlSequenceNode root)
                return;
            foreach (var entry in root.Children)
            {
                if (entry is not YamlMappingNode map)
                    continue;
                var preset = ReadPreset(map);
                if (preset != null)
                    _presets.Add(preset);
            }
        }
        catch (Exception e)
        {
            _log.Warning($"Failed to read action bar presets from {FilePath}: {e.Message}");
        }
    }

    private void Persist()
    {
        try
        {
            _resources.UserData.CreateDir(FilePath.Directory);
            var sequence = new YamlSequenceNode();
            foreach (var preset in _presets)
                sequence.Add(WritePreset(preset));
            var stream = new YamlStream(new YamlDocument(sequence));
            using var writer = _resources.UserData.OpenWriteText(FilePath);
            stream.Save(writer, false);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to write action bar presets to {FilePath}: {e.Message}");
        }
    }

    private static YamlMappingNode WritePreset(ActionBarPreset p)
    {
        var map = new YamlMappingNode();
        map.Add(SchemaKey, p.SchemaVersion.ToString(CultureInfo.InvariantCulture));
        map.Add("name", p.Name);
        map.Add("rows", p.Rows.ToString(CultureInfo.InvariantCulture));
        map.Add("slotsPerRow", p.SlotsPerRow.ToString(CultureInfo.InvariantCulture));
        map.Add("slotSpacing", p.SlotSpacing.ToString(CultureInfo.InvariantCulture));
        map.Add("showKeybindLabel", p.ShowKeybindLabel.ToString());
        map.Add("showEmptySlots", p.ShowEmptySlots.ToString());
        map.Add("autoAddActions", p.AutoAddActions.ToString());
        map.Add("lock", p.Lock.ToString());
        map.Add("buttonBackgroundAlpha", p.ButtonBackgroundAlpha.ToString(CultureInfo.InvariantCulture));
        map.Add("positionX", p.PositionX.ToString(CultureInfo.InvariantCulture));
        map.Add("positionY", p.PositionY.ToString(CultureInfo.InvariantCulture));

        var slots = new YamlSequenceNode();
        foreach (var id in p.SlotProtoIds)
            slots.Add(new YamlScalarNode(id ?? string.Empty));
        map.Add("slots", slots);
        return map;
    }

    private ActionBarPreset? ReadPreset(YamlMappingNode map)
    {
        if (!TryGetInt(map, SchemaKey, out var schema) || schema != ActionBarPresetSchema.CurrentVersion)
        {
            // Skip silently rather than guess at field shapes from another schema; the
            // caller has already opted in to the file by clicking Load.
            return null;
        }

        var preset = new ActionBarPreset
        {
            SchemaVersion = schema,
            Name = TryGetString(map, "name") ?? string.Empty,
            Rows = TryGetInt(map, "rows", out var rows) ? rows : 1,
            SlotsPerRow = TryGetInt(map, "slotsPerRow", out var sr) ? sr : 10,
            SlotSpacing = TryGetInt(map, "slotSpacing", out var sp) ? sp : 0,
            ShowKeybindLabel = TryGetBool(map, "showKeybindLabel", true),
            ShowEmptySlots = TryGetBool(map, "showEmptySlots", false),
            AutoAddActions = TryGetBool(map, "autoAddActions", true),
            Lock = TryGetBool(map, "lock", false),
            ButtonBackgroundAlpha = TryGetFloat(map, "buttonBackgroundAlpha", 150f / 255f),
            PositionX = TryGetFloat(map, "positionX", -1f),
            PositionY = TryGetFloat(map, "positionY", -1f),
        };

        if (map.Children.TryGetValue(new YamlScalarNode("slots"), out var slotsNode)
            && slotsNode is YamlSequenceNode slotsSeq)
        {
            preset.SlotProtoIds = slotsSeq.Children
                .OfType<YamlScalarNode>()
                .Select(s => string.IsNullOrEmpty(s.Value) ? null : s.Value)
                .ToList();
        }
        return preset;
    }

    private static string? TryGetString(YamlMappingNode map, string key)
    {
        return map.Children.TryGetValue(new YamlScalarNode(key), out var node)
               && node is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }

    private static bool TryGetInt(YamlMappingNode map, string key, out int value)
    {
        var raw = TryGetString(map, key);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetBool(YamlMappingNode map, string key, bool fallback)
    {
        var raw = TryGetString(map, key);
        return bool.TryParse(raw, out var value) ? value : fallback;
    }

    private static float TryGetFloat(YamlMappingNode map, string key, float fallback)
    {
        var raw = TryGetString(map, key);
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
