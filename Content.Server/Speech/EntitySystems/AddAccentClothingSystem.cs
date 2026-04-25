using Content.Server.Speech.Components;
using Content.Shared.Clothing;

namespace Content.Server.Speech.EntitySystems;

public sealed class AddAccentClothingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddAccentClothingComponent, ClothingGotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<AddAccentClothingComponent, ClothingGotUnequippedEvent>(OnGotUnequipped);
    }


//  TODO: Turn this into a relay event.
    private void OnGotEquipped(EntityUid uid, AddAccentClothingComponent component, ref ClothingGotEquippedEvent args)
    {
        var componentType = Factory.GetRegistration(component.Accent).Type;

        // HONK START - #634: ReplacementAccent composes via its list, so append instead of short-circuiting on HasComp.
        if (componentType == typeof(ReplacementAccentComponent))
        {
            if (component.ReplacementPrototype == null)
                return;

            var rep = EnsureComp<ReplacementAccentComponent>(args.Wearer);
            rep.Accents.Add(component.ReplacementPrototype);
            component.IsActive = true;
            return;
        }
        // HONK END

        // does the user already has this accent?
        if (HasComp(args.Wearer, componentType))
            return;

        // add accent to the user
        var accentComponent = (Component) Factory.GetComponent(componentType);
        AddComp(args.Wearer, accentComponent);

        component.IsActive = true;
    }

    private void OnGotUnequipped(EntityUid uid, AddAccentClothingComponent component, ref ClothingGotUnequippedEvent args)
    {
        if (!component.IsActive)
            return;

        var componentType = Factory.GetRegistration(component.Accent).Type;

        // HONK START - #634: remove just our contributed entry from the list, preserving other accents (species baseline, other hats).
        if (componentType == typeof(ReplacementAccentComponent))
        {
            if (component.ReplacementPrototype != null
                && TryComp<ReplacementAccentComponent>(args.Wearer, out var rep))
            {
                rep.Accents.Remove(component.ReplacementPrototype);
                if (rep.Accents.Count == 0)
                    RemComp<ReplacementAccentComponent>(args.Wearer);
            }
            component.IsActive = false;
            return;
        }
        // HONK END

        // try to remove accent
        RemComp(args.Wearer, componentType);

        component.IsActive = false;
    }
}
