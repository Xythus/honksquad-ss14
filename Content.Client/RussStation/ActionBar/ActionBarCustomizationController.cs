using System.Linq;
using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Client.UserInterface.Systems.Actions;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Client.UserInterface.Systems.Actions.Windows;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client.RussStation.ActionBar;

// HONK Fork UIController that applies user-tunable layout settings to the
// upstream action bar widget. Reads CVars registered in CCVars.ActionBar.cs
// and re-applies them whenever any of them change, so settings take effect
// the moment the user clicks Apply in the options menu. Also re-applies on
// gameplay state entry since the ActionsBar widget only exists then.
public sealed class ActionBarCustomizationController : UIController, IOnStateEntered<GameplayState>
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IClientPreferencesManager _prefs = default!;

    private int _rows;
    private int _slotsPerRow;
    private int _slotSpacing;
    private bool _showKeybindLabel;

    // Read by ActionButton.UpdateBackground (HONK block) on every frame so empty
    // slots can render a faint outline. A static keeps the hot path free of a
    // UIController lookup per button per frame.
    public static bool ShowEmptySlots { get; private set; }

    // Read by ActionUIController.OnActionAdded (HONK guard) so newly granted actions
    // can skip auto-population when the user wants a curated bar layout.
    public static bool AutoAddActions { get; private set; } = true;

    // Read by ActionUIController drag / right-click paths (HONK guards) to keep
    // the bar immutable when the player has locked the layout.
    public static bool LockActions { get; private set; }

    // Read by the gameplay-screen resize handlers (HONK guards) to keep them from
    // calling MaxGridHeight/MaxGridWidth, which would flip the grid into size-limit
    // mode and silently overwrite the user's explicit row count on resize.
    public static readonly bool OverridesRowLayout = true;

    // Base 0.0-1.0 alpha applied to every action button's slot background. Read each
    // frame by ActionButton.UpdateBackground (HONK block). The empty-slot fade scales
    // proportionally so the relative contrast stays consistent.
    public static float ButtonBackgroundAlpha { get; private set; } = 150f / 255f;

    // Flipped by ActionUIController drag hooks so empty drop targets are padded into the
    // container even when the persistent show-empty toggle is off.
    public static bool IsDragActive { get; private set; }

    // Mirrored from SlotHotkeyController so the bar-side code (UpdateBackground, ApplyLabels)
    // can reveal every slot and its keybind label while the player is assigning hotkeys.
    public static bool AssignHotkeyMode { get; private set; }

    // Stashed when we lift the bar out of its XAML parent for free-positioning, so a
    // reset to anchored mode can drop it back at the same sibling index.
    private Control? _anchoredParent;
    private int _anchoredIndex;
    private LayoutContainer? _floatParent;
    private float _positionX;
    private float _positionY;

    // Emote id ("Wave", "Salute", ...) -> slot index. Sourced from the active preset's
    // EmoteIds list (loaded at startup and refreshed when ApplyPreset runs) so a player's
    // curated emote layout survives disconnects and server restarts via the preset file
    // rather than a separate CVar. Read by OnActionAdded through TryGetSavedEmoteSlot.
    private readonly Dictionary<string, int> _emoteSlots = new();

    public bool TryGetSavedEmoteSlot(string? emoteId, out int slot)
    {
        if (!string.IsNullOrEmpty(emoteId) && _emoteSlots.TryGetValue(emoteId, out slot))
            return true;
        slot = default;
        return false;
    }

    public override void Initialize()
    {
        base.Initialize();

        _rows = _cfg.GetCVar(CCVars.HonkActionBarRows);
        _slotsPerRow = _cfg.GetCVar(CCVars.HonkActionBarSlotsPerRow);
        _slotSpacing = _cfg.GetCVar(CCVars.HonkActionBarSlotSpacing);
        _showKeybindLabel = _cfg.GetCVar(CCVars.HonkActionBarShowKeybindLabel);
        ShowEmptySlots = _cfg.GetCVar(CCVars.HonkActionBarShowEmptySlots);
        AutoAddActions = _cfg.GetCVar(CCVars.HonkActionBarAutoAddActions);
        LockActions = _cfg.GetCVar(CCVars.HonkActionBarLock);
        ButtonBackgroundAlpha = Math.Clamp(_cfg.GetCVar(CCVars.HonkActionBarButtonBackgroundAlpha), 0f, 1f);

        _cfg.OnValueChanged(CCVars.HonkActionBarRows, OnRowsChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarSlotsPerRow, OnSlotsPerRowChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarSlotSpacing, OnSlotSpacingChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarShowKeybindLabel, OnShowKeybindLabelChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarShowEmptySlots, OnShowEmptySlotsChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarAutoAddActions, v => AutoAddActions = v, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarLock, v => LockActions = v, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarButtonBackgroundAlpha,
            v => ButtonBackgroundAlpha = Math.Clamp(v, 0f, 1f), true);

        // Seed _emoteSlots from the active preset before the actions system dispatches its
        // initial OnActionAdded burst, so emote actions can land on their saved slots even
        // though the full preset apply waits until HonkOnContainerReady has actions to bind.
        LoadActiveEmoteSlots();

        _positionX = _cfg.GetCVar(CCVars.HonkActionBarPositionX);
        _positionY = _cfg.GetCVar(CCVars.HonkActionBarPositionY);
        _cfg.OnValueChanged(CCVars.HonkActionBarPositionX, v => { _positionX = v; ApplyPosition(); }, false);
        _cfg.OnValueChanged(CCVars.HonkActionBarPositionY, v => { _positionY = v; ApplyPosition(); }, false);
        _cfg.OnValueChanged(CCVars.HonkActionBarLock, _ => RefreshDragHandleVisibility(), false);

        // Mirror the assign-hotkey toggle so the bar auto-reveals while the player rebinds slots.
        var slotHotkeys = UIManager.GetUIController<SlotHotkeyController>();
        AssignHotkeyMode = slotHotkeys.AssignMode;
        slotHotkeys.AssignStateChanged += OnAssignStateChanged;
        // Rebuild the hotbar when any action-bar slot's binding changes so the labels track
        // what Settings → Controls currently holds.
        slotHotkeys.SlotBindingChanged += RefreshHotbar;
    }

    private void OnAssignStateChanged()
    {
        var slotHotkeys = UIManager.GetUIController<SlotHotkeyController>();
        AssignHotkeyMode = slotHotkeys.AssignMode;
        ApplyLayout();
        ApplyLabels();
        ApplyArmedHighlight();
        RefreshHotbar();
    }

    // Highlight the currently-armed slot so the player has feedback between clicking a slot
    // and pressing the hotbar key that will be assigned to it. Clears all highlights when
    // assign mode is off or no slot is armed.
    private void ApplyArmedHighlight()
    {
        if (GetContainer() is not { } container)
            return;

        var armed = UIManager.GetUIController<SlotHotkeyController>().ArmedSlot;
        var i = 0;
        foreach (var button in container.GetButtons())
        {
            button.HighlightRect.Visible = AssignHotkeyMode && armed == i;
            i++;
        }
    }

    private void OnRowsChanged(int value)
    {
        _rows = Math.Clamp(value, 1, 4);
        ApplyLayout();
        RefreshHotbar();
    }

    private void OnSlotsPerRowChanged(int value)
    {
        _slotsPerRow = Math.Clamp(value, 1, 10);
        ApplyLayout();
        RefreshHotbar();
    }

    private void OnSlotSpacingChanged(int value)
    {
        _slotSpacing = Math.Clamp(value, 0, 16);
        ApplyLayout();
    }

    private void OnShowKeybindLabelChanged(bool value)
    {
        _showKeybindLabel = value;
        ApplyLabels();
    }

    private void OnShowEmptySlotsChanged(bool value)
    {
        ShowEmptySlots = value;
        ApplyLayout();
        RefreshHotbar();
    }

    private ActionButtonContainer? GetContainer()
    {
        var bar = UIManager.GetActiveUIWidgetOrNull<ActionsBar>();
        return bar?.ActionsContainer;
    }

    private void ApplyLayout()
    {
        if (GetContainer() is not { } container)
            return;

        // Setting Columns (not Rows) makes the grid fill row-by-row, so hotkeys 1..0 land
        // in row 1 and 2 lands on row 2's leftmost slot rather than column-major flow.
        container.Columns = _slotsPerRow;
        container.HSeparationOverride = _slotSpacing;
        container.VSeparationOverride = _slotSpacing;
        // Reveal every slot (pad up to rows x slots_per_row) when either the user's persistent
        // toggle is on, a drag is active, or the player is actively rebinding slot hotkeys.
        container.HonkMinSlotCount = ShowEmptySlots || IsDragActive || AssignHotkeyMode
            ? _rows * _slotsPerRow
            : 0;
    }

    private void ApplyLabels()
    {
        if (GetContainer() is not { } container)
            return;

        // Normally labels only render on slots with an action and on drag targets. Assign-hotkey
        // mode forces every slot's label visible so the player can see which key they're rebinding.
        foreach (var button in container.GetButtons())
        {
            button.Label.Visible = AssignHotkeyMode
                || (_showKeybindLabel && (button.Action != null || IsDragActive));
        }
    }

    private void RefreshHotbar()
    {
        if (GetContainer() == null)
            return;
        UIManager.GetUIController<ActionUIController>().HonkRefreshHotbar();
        // Padding may have added buttons; labels must be re-applied to the new ones.
        ApplyLabels();
    }

    /// <summary>Refresh <see cref="_emoteSlots"/> from <paramref name="emoteIds"/>, a
    /// parallel-to-SlotProtoIds list where each non-null entry is the emote id that
    /// occupies that slot. Validates against EmotePrototype so a stale preset entry
    /// from a removed emote silently drops out instead of poisoning the bar.</summary>
    private void RefreshEmoteSlots(List<string?> emoteIds)
    {
        _emoteSlots.Clear();
        var protoMan = IoCManager.Resolve<Robust.Shared.Prototypes.IPrototypeManager>();
        for (var i = 0; i < emoteIds.Count; i++)
        {
            var id = emoteIds[i];
            if (string.IsNullOrEmpty(id))
                continue;
            if (!protoMan.HasIndex<Content.Shared.Chat.Prototypes.EmotePrototype>(id))
                continue;
            _emoteSlots[id] = i;
        }
    }

    /// <summary>Read the first saved preset that matches the active character (or any
    /// character-agnostic preset, for back-compat with files saved before this scope was
    /// added) and seed <see cref="_emoteSlots"/> from it. Called during Initialize so
    /// emote actions granted before HonkOnContainerReady runs still hit their saved
    /// slots.</summary>
    private void LoadActiveEmoteSlots()
    {
        var preset = FindActivePresetForCharacter();
        if (preset == null)
        {
            _emoteSlots.Clear();
            return;
        }
        RefreshEmoteSlots(preset.EmoteIds);
    }

    /// <summary>Currently-selected character profile name, or empty if preferences
    /// haven't synced from the server yet. Empty matches presets that were saved
    /// before character scoping landed (their <c>CharacterName</c> is also empty).</summary>
    public string GetActiveCharacterName()
        => _prefs.Preferences?.SelectedCharacter.Name ?? string.Empty;

    /// <summary>Picks the first saved preset whose <c>CharacterName</c> matches the
    /// active character, falling back to the first character-agnostic preset.</summary>
    public ActionBarPreset? FindActivePresetForCharacter()
    {
        var presets = GetPresetStore().Presets;
        if (presets.Count == 0)
            return null;
        var character = GetActiveCharacterName();
        foreach (var preset in presets)
        {
            if (string.Equals(preset.CharacterName, character, StringComparison.Ordinal))
                return preset;
        }
        // No exact match: fall through to a character-agnostic preset (CharacterName
        // empty) so old presets and "global" ones still work.
        foreach (var preset in presets)
        {
            if (string.IsNullOrEmpty(preset.CharacterName))
                return preset;
        }
        return null;
    }

    // Wires the auto-add toggle and the Presets button on the actions window, called from
    // the upstream LoadGui (HONK) each time the window is (re)created. Lock has moved into
    // the preset window so the bar's customisation controls live in one place.
    public void HonkBindWindow(ActionsWindow window)
    {
        window.AutoAddButton.Pressed = AutoAddActions;
        window.AutoAddButton.OnToggled += a => _cfg.SetCVar(CCVars.HonkActionBarAutoAddActions, a.Pressed);
        window.PresetsButton.OnPressed += _ => OpenPresetsWindow();
    }

    private ActionBarPresetsWindow? _presetsWindow;
    private ActionBarPresetStore? _presetStoreCached;

    private ActionBarPresetStore GetPresetStore()
    {
        // Lazily allocated so the controller doesn't pull in IResourceManager until the
        // player actually opens the preset UI; keeps client startup unaffected.
        return _presetStoreCached ??= new ActionBarPresetStore(IoCManager.Resolve<Robust.Shared.ContentPack.IResourceManager>());
    }

    private void OpenPresetsWindow()
    {
        if (_presetsWindow is { Disposed: false })
        {
            _presetsWindow.Open();
            _presetsWindow.MoveToFront();
            return;
        }
        _presetsWindow = new ActionBarPresetsWindow(GetPresetStore(), _cfg, CapturePreset, ApplyPreset, ResetToDefaults, GetActiveCharacterName);
        _presetsWindow.OpenCentered();
    }

    private ActionBarPreset CapturePreset()
    {
        var actions = UIManager.GetUIController<ActionUIController>();
        return new ActionBarPreset
        {
            CharacterName = GetActiveCharacterName(),
            Rows = _rows,
            SlotsPerRow = _slotsPerRow,
            SlotSpacing = _slotSpacing,
            ShowKeybindLabel = _showKeybindLabel,
            ShowEmptySlots = ShowEmptySlots,
            AutoAddActions = AutoAddActions,
            Lock = LockActions,
            ButtonBackgroundAlpha = ButtonBackgroundAlpha,
            PositionX = _positionX,
            PositionY = _positionY,
            SlotProtoIds = actions.HonkGetSlotProtoIds(),
            EmoteIds = actions.HonkGetSlotEmoteIds(),
        };
    }

    private void ApplyPreset(ActionBarPreset preset)
    {
        _cfg.SetCVar(CCVars.HonkActionBarRows, preset.Rows);
        _cfg.SetCVar(CCVars.HonkActionBarSlotsPerRow, preset.SlotsPerRow);
        _cfg.SetCVar(CCVars.HonkActionBarSlotSpacing, preset.SlotSpacing);
        _cfg.SetCVar(CCVars.HonkActionBarShowKeybindLabel, preset.ShowKeybindLabel);
        _cfg.SetCVar(CCVars.HonkActionBarShowEmptySlots, preset.ShowEmptySlots);
        _cfg.SetCVar(CCVars.HonkActionBarAutoAddActions, preset.AutoAddActions);
        // Force lock on after every preset apply: a freshly-loaded curated layout shouldn't
        // get nudged by mis-clicks before the player has even looked at it. Players can still
        // toggle the lock back off from the presets window.
        _cfg.SetCVar(CCVars.HonkActionBarLock, true);
        _cfg.SetCVar(CCVars.HonkActionBarButtonBackgroundAlpha, preset.ButtonBackgroundAlpha);
        _cfg.SetCVar(CCVars.HonkActionBarPositionX, preset.PositionX);
        _cfg.SetCVar(CCVars.HonkActionBarPositionY, preset.PositionY);
        _cfg.SaveToFile();

        RefreshEmoteSlots(preset.EmoteIds);
        UIManager.GetUIController<ActionUIController>().HonkLoadFromPreset(preset.SlotProtoIds, preset.EmoteIds);
    }

    private void ResetToDefaults()
    {
        // Scope: only reset things the presets window owns (slot contents and bar position).
        // Size/spacing/label/alpha settings live in Options → Misc and have their own
        // controls; wiping them from the preset window's Reset button surprised players
        // who expected only the layout (slot assignments) to revert.
        _cfg.SetCVar(CCVars.HonkActionBarPositionX, CCVars.HonkActionBarPositionX.DefaultValue);
        _cfg.SetCVar(CCVars.HonkActionBarPositionY, CCVars.HonkActionBarPositionY.DefaultValue);
        _cfg.SaveToFile();

        UIManager.GetUIController<ActionUIController>().HonkResetSlots();
    }

    // Called from the upstream ActionUIController (HONK) once the action bar widget
    // is registered. That's the first point the container reliably exists, so fresh
    // client starts get the stored layout applied here rather than relying on the
    // order in which UIControllers receive OnStateEntered.
    public void HonkOnContainerReady()
    {
        ApplyLayout();
        ApplyLabels();
        RefreshHotbar();
        WireDragHandle();
        ApplyPosition();
    }

    /// <summary>Auto-apply the first character-matched preset on first session start so a
    /// returning player gets their curated layout without clicking Load. Must run AFTER
    /// <c>LinkAllActions</c>: that call dispatches <c>OnActionAdded</c> for every linked
    /// action and appends them to the bar, which would clobber a preset applied earlier.
    /// Idempotent via the <see cref="_autoLoadedInitialPreset"/> flag, so subsequent screen
    /// reloads (e.g. respawn) don't trigger a second auto-load.</summary>
    public void HonkAfterInitialLink()
    {
        if (_autoLoadedInitialPreset)
            return;
        if (!UIManager.GetUIController<ActionUIController>().HonkHasClientActions())
            return;

        _autoLoadedInitialPreset = true;
        if (FindActivePresetForCharacter() is { } preset)
            ApplyPreset(preset);
    }

    private bool _autoLoadedInitialPreset;

    private ActionsBar? GetBar() => UIManager.GetActiveUIWidgetOrNull<ActionsBar>();

    private void WireDragHandle()
    {
        if (GetBar() is not { } bar)
            return;
        // Defensive: re-wiring on every container ready call would stack handlers, so
        // unsubscribe first.
        bar.HonkDragHandle.DragMoved -= OnHandleDragMoved;
        bar.HonkDragHandle.DragEnded -= OnHandleDragEnded;
        bar.HonkDragHandle.DragMoved += OnHandleDragMoved;
        bar.HonkDragHandle.DragEnded += OnHandleDragEnded;
        RefreshDragHandleVisibility();
    }

    private void RefreshDragHandleVisibility()
    {
        if (GetBar() is { } bar)
            bar.HonkDragHandle.Visible = !LockActions;
    }

    private void OnHandleDragMoved(Vector2 delta)
    {
        if (GetBar() is not { } bar)
            return;
        // Capture the bar's pre-reparent screen position so the first drag doesn't snap
        // the bar to (0,0) before we've written any explicit coordinates.
        var initial = bar.GlobalPosition;
        EnsureFloating(bar);
        if (_floatParent == null)
            return;
        if (_positionX < 0 || _positionY < 0)
        {
            var local = initial - _floatParent.GlobalPosition;
            _positionX = local.X;
            _positionY = local.Y;
        }
        var size = bar.Size;
        var bounds = _floatParent.Size;
        _positionX = Math.Clamp(_positionX + delta.X, ActionBarConstants.PositionEdgeMargin, MathF.Max(ActionBarConstants.PositionEdgeMargin, bounds.X - size.X - ActionBarConstants.PositionEdgeMargin));
        _positionY = Math.Clamp(_positionY + delta.Y, ActionBarConstants.PositionEdgeMargin, MathF.Max(ActionBarConstants.PositionEdgeMargin, bounds.Y - size.Y - ActionBarConstants.PositionEdgeMargin));
        LayoutContainer.SetPosition(bar, new Vector2(_positionX, _positionY));
    }

    private void OnHandleDragEnded()
    {
        // Persist whatever the in-flight drag landed on. SaveToFile so a crash mid-session
        // doesn't lose the player's chosen layout.
        _cfg.SetCVar(CCVars.HonkActionBarPositionX, _positionX);
        _cfg.SetCVar(CCVars.HonkActionBarPositionY, _positionY);
        _cfg.SaveToFile();
    }

    /// <summary>Applies the saved position CVars to the bar, lifting it into the screen's
    /// LayoutContainer when set or restoring its XAML parent when reset to -1.</summary>
    private void ApplyPosition()
    {
        if (GetBar() is not { } bar)
            return;
        if (_positionX < 0 || _positionY < 0)
        {
            EnsureAnchored(bar);
            return;
        }
        EnsureFloating(bar);
        if (_floatParent == null)
            return;
        LayoutContainer.SetPosition(bar, new Vector2(_positionX, _positionY));
    }

    private void EnsureFloating(ActionsBar bar)
    {
        if (bar.Parent is LayoutContainer current && current == _floatParent)
            return;
        var origin = bar.Parent;
        if (origin == null)
            return;
        // Walk up to find the screen-level LayoutContainer (ViewportContainer in both
        // game screens) so SetPosition's attached props will actually be honoured.
        Control? walker = origin;
        LayoutContainer? layout = null;
        while (walker != null)
        {
            if (walker is LayoutContainer lc)
            {
                layout = lc;
                break;
            }
            walker = walker.Parent;
        }
        if (layout == null)
            return;
        if (_anchoredParent == null)
        {
            _anchoredParent = origin;
            _anchoredIndex = bar.GetPositionInParent();
        }
        origin.RemoveChild(bar);
        layout.AddChild(bar);
        _floatParent = layout;
    }

    private void EnsureAnchored(ActionsBar bar)
    {
        if (_floatParent == null || bar.Parent != _floatParent || _anchoredParent == null)
            return;
        _floatParent.RemoveChild(bar);
        _anchoredParent.AddChild(bar);
        // Keep the original sibling order so the menu bar / vote menu stack as before.
        var clamped = Math.Clamp(_anchoredIndex, 0, _anchoredParent.ChildCount - 1);
        bar.SetPositionInParent(clamped);
    }

    // Called from the action drag hooks so the container can pad in empty drop targets
    // and then trim them back out once the drop completes.
    public void HonkSetDragActive(bool active)
    {
        if (IsDragActive == active)
            return;
        IsDragActive = active;
        ApplyLayout();
        RefreshHotbar();
        ApplyLabels();
    }

    public void OnStateEntered(GameplayState state)
    {
        // HonkOnContainerReady runs from ActionUIController.LoadGui once the widget is up;
        // nothing else to do here now that MaxGrid* resizes no longer clobber our Rows.
    }
}
