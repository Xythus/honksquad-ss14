using Robust.Shared.Serialization.Manager;
// HONK START - #634: species lookup for species-aware Accentless.
using Content.Shared.Humanoid;
// HONK END

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// This handles removing accents when using the accentless trait.
/// </summary>
public sealed class AccentlessSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AccentlessComponent, ComponentStartup>(RemoveAccents);
    }

    private void RemoveAccents(EntityUid uid, AccentlessComponent component, ComponentStartup args)
    {
        foreach (var accent in component.RemovedAccents.Values)
        {
            var accentComponent = accent.Component;
            RemComp(uid, accentComponent.GetType());
        }

        // HONK START - #634: species-aware removals on humanoids.
        if (component.SpeciesEffects.Count > 0
            && TryComp<HumanoidProfileComponent>(uid, out var humanoid)
            && component.SpeciesEffects.TryGetValue(humanoid.Species, out var effect))
        {
            foreach (var strip in effect.Strips.Values)
                RemComp(uid, strip.Component.GetType());

            // Dispatch the replacement-accent list cleanup to a shared event handled server-side
            // where ReplacementAccentComponent lives.
            if (effect.StripsReplacementAccents.Count > 0)
            {
                var ev = new AccentlessStripReplacementAccentsEvent(effect.StripsReplacementAccents);
                RaiseLocalEvent(uid, ref ev);
            }
        }
        // HONK END
    }
}

// HONK START - #634: raised when Accentless needs to remove specific accent ids from
// ReplacementAccentComponent.Accents on a humanoid. Handled server-side only.
[ByRefEvent]
public readonly record struct AccentlessStripReplacementAccentsEvent(List<string> AccentIds);
// HONK END
