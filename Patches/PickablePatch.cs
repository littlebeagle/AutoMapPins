using AutoMapPins.Model;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace AutoMapPins.Patches;

[HarmonyPatch(typeof(Pickable), "Awake")]
internal class PickablePatch
{
    private static readonly AccessTools.FieldRef<Pickable, bool> PickedRef =
        AccessTools.FieldRefAccess<Pickable, bool>("m_picked");

    [UsedImplicitly]
    [HarmonyPriority(Priority.High)]
    static void Postfix(ref Pickable __instance)
    {
        // only add a pin, if the pickable is not picked already
        if (!PickedRef(__instance)) CommonPatchLogic.Patch(__instance.gameObject);
    }
}

[HarmonyPatch(typeof(Pickable), "SetPicked")]
public class PickableDropPatch
{
    [UsedImplicitly]
    private static void Postfix(ref Pickable __instance, bool picked)
    {
        GameObject gameObject = __instance.gameObject;

        // we skip any objects inside dungeons (above 4000m height)
        if (gameObject.transform.position.y <= AutoMapPinsPlugin.MaxDetectionHeight.Value && picked)
        {
            if (gameObject.TryGetComponent(out PinComponent component))
            {
                // if the item is picked, let's destroy the pin
                component.OnDestroy();
            }
        }
    }
}