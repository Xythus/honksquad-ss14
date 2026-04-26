namespace Content.Client.RussStation.ActionBar;

public static class ActionBarConstants
{
    /// <summary>Width of the in-bar drag grabber, in virtual pixels.</summary>
    public const float DragHandleWidth = 10f;

    /// <summary>Height of the drag grabber. Matches roughly half a slot so it doesn't dominate the bar.</summary>
    public const float DragHandleHeight = 24f;

    /// <summary>Border thickness on the drag grabber's stylebox.</summary>
    public const int DragHandleBorderThickness = 1;

    /// <summary>Pixel margin from the viewport edge when clamping a moved bar so it doesn't sit flush.</summary>
    public const float PositionEdgeMargin = 4f;
}
