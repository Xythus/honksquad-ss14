using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client.RussStation.ActionBar;

// HONK Grabber control mounted on the upstream ActionsBar widget. While unlocked,
// the player can left-drag this handle to reposition the bar; the controller writes
// the new coordinates to honk.action_bar.position_x/y and clamps to the viewport.
// Hidden entirely when LockActions is set so a curated layout can't be nudged.
public sealed class ActionBarDragHandle : PanelContainer
{
    /// <summary>Cursor moved while held. Argument is the cursor delta in screen pixels.</summary>
    public event Action<Vector2>? DragMoved;
    public event Action? DragEnded;

    private bool _dragging;

    public ActionBarDragHandle()
    {
        MinSize = new Vector2(ActionBarConstants.DragHandleWidth, ActionBarConstants.DragHandleHeight);
        VerticalAlignment = VAlignment.Center;
        MouseFilter = MouseFilterMode.Stop;
        TooltipSupplier = _ => new Label { Text = Loc.GetString("ui-actionbar-drag-handle-tooltip") };
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#888888"),
            BorderColor = Color.FromHex("#444444"),
            BorderThickness = new Thickness(ActionBarConstants.DragHandleBorderThickness),
        };
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;
        _dragging = true;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !_dragging)
            return;
        _dragging = false;
        DragEnded?.Invoke();
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_dragging)
            return;
        if (args.Relative != Vector2.Zero)
            DragMoved?.Invoke(args.Relative);
    }
}
