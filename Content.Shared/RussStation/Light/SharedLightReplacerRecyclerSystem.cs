namespace Content.Shared.RussStation.Light;

/// <summary>
/// Base type for the light replacer recycler system. Exists so
/// <see cref="LightReplacerRecyclerComponent"/>'s <c>[Access]</c> attribute
/// can reference a shared type while the actual logic lives server-side.
/// </summary>
public abstract class SharedLightReplacerRecyclerSystem : EntitySystem
{
}
