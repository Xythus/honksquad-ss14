using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Stunnable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas:true), AutoGenerateComponentPause, Access(typeof(SharedStunSystem))]
public sealed partial class KnockedDownComponent : Component
{
    /// <summary>
    /// Game time that we can stand up.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// Should we try to stand up?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoStand = true;

    /// <summary>
    /// The Standing Up DoAfter.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ushort? DoAfterId;

    /// <summary>
    /// Friction modifier for knocked down players.
    /// Makes them accelerate and deccelerate slower.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FrictionModifier = 1f; // Should add a friction modifier to slipping to compensate for this

    /// <summary>
    /// Modifier to the maximum movement speed of a knocked down mover.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1f;

    /// <summary>
    /// How long does it take us to get up?
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan GetUpDoAfter = TimeSpan.FromSeconds(1);

    // HONK START - #492: debounce failed auto-stand retries so prediction divergence can't spawn per-tick DoAfter ghosts.
    /// <summary>
    /// Earliest game time at which a failed auto-stand attempt may retry. Separate from <see cref="NextUpdate"/>
    /// so the knockdown expiry timer and knockdown alert cooldown aren't disturbed by retry throttling.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextStandAttempt;
    // HONK END
}
