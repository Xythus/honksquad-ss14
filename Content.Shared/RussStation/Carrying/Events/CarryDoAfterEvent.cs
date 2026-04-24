using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Carrying.Events;

/// <summary>
/// DoAfter event for the carry windup.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CarryDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// DoAfter event for a third party prying a carried entity free from its carrier.
/// User is the interrupter, target is the carried entity.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CarryInterruptDoAfterEvent : SimpleDoAfterEvent;
