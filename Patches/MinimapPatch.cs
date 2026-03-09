using System.Collections.Generic;
using System.Linq;
using AutoMapPins.Common;
using AutoMapPins.Data;
using AutoMapPins.Model;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace AutoMapPins.Patches;

[HarmonyPatch(typeof(Minimap))]
internal abstract class MinimapPatch : HasLogger
{
    private static readonly List<MapPin> MapTableTempStorage = new();

    private static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> PinsRef =
        AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");

    [UsedImplicitly]
    [HarmonyPatch("LoadMapData")]
    [HarmonyPostfix]
    internal static void LoadMapDataPostfix(ref Minimap __instance)
    {
        var pins = PinsRef(__instance);
        var pinsToRemove = new List<Minimap.PinData>();
        var convertedPins = new List<MapPin>();

        Log.LogInfo($"loading map data with '{pins.Count}' pins");

        foreach (var pin in pins)
        {
            var config = Registry.ConfiguredPins
                .FirstOrDefault(pinConfig => pinConfig.Value.Name == pin.m_name)
                .Value;

            if (config == null) continue;

            pinsToRemove.Add(pin);
            var newMapPin = new MapPin(pin, config);
            convertedPins.Add(newMapPin);
        }

        if (pinsToRemove.Count > 0)
        {
            Log.LogInfo(
                $"removing {pinsToRemove.Count} pins due to config updates and/or replacement by loading from player save file");

            foreach (var pin in pinsToRemove)
            {
                __instance.RemovePin(pin);
            }
        }

        if (convertedPins.Count > 0)
        {
            Log.LogInfo($"adding {convertedPins.Count} AMP pins to replace vanilla stored pins");

            foreach (var pin in convertedPins)
            {
                pins.Add(pin);
            }
        }

        Log.LogInfo($"Loaded map with {pins.Count} existing pins");
    }

    [UsedImplicitly]
    [HarmonyPatch("UpdatePins")]
    [HarmonyPostfix]
    internal static void UpdatePinsPostfix(ref Minimap __instance)
    {
        var pins = PinsRef(__instance);

        foreach (var pin in pins)
        {
            if (pin is MapPin mapPin && mapPin.m_iconElement)
            {
                mapPin.UpdatePinColor();
            }
        }
    }

    [UsedImplicitly]
    [HarmonyPatch("GetSharedMapData")]
    [HarmonyPrefix]
    internal static void GetSharedMapDataPrefix(ref Minimap __instance)
    {
        var pins = PinsRef(__instance);

        MapTableTempStorage.Clear();
        MapTableTempStorage.AddRange(pins.OfType<MapPin>());

        foreach (var mapPin in MapTableTempStorage)
        {
            pins.Remove((Minimap.PinData)mapPin);
        }
    }

    [UsedImplicitly]
    [HarmonyPatch("GetSharedMapData")]
    [HarmonyPostfix]
    internal static void GetSharedMapDataPostfix(ref Minimap __instance)
    {
        var pins = PinsRef(__instance);
        pins.AddRange(MapTableTempStorage.Select(pin => (Minimap.PinData)pin));
        MapTableTempStorage.Clear();
    }

    internal static void UpsertPin(GameObject objectToPin)
    {
        if (!Minimap.instance) return;

        var pins = PinsRef(Minimap.instance);
        if (pins == null) return;

        string internalName = Constants.ParseInternalName(objectToPin.name);

        if (!Registry.ConfiguredPins.TryGetValue(internalName, out Config.Pin config) || !config.IsActive)
        {
            Registry.AddMissingConfig(internalName);
            return;
        }

        Minimap.PinData? existingPin = pins.FirstOrDefault(pin =>
            pin.m_pos == objectToPin.transform.position && pin.m_name == config.Name);

        if (existingPin == null)
        {
            CreateAndInsertPin(new PositionedObject(internalName, objectToPin.transform.position), config);
        }
        else
        {
            // found existing pin
        }
    }

    private static void CreateAndInsertPin(PositionedObject positionedObject, Config.Pin config)
    {
        if (!ObjectGroupingAvailable(positionedObject, config))
        {
            PinsRef(Minimap.instance).Add(new MapPin(positionedObject, config));
        }
    }

    private static bool ObjectGroupingAvailable(PositionedObject positionedObject, Config.Pin config)
    {
        if (!config.Groupable) return false;

        MapPin? viablePinForGrouping = PinsRef(Minimap.instance)
            .OfType<MapPin>()
            .Where(pin => pin.Config.Groupable)
            .FirstOrDefault(pin => pin.AcceptsObject(positionedObject));

        if (viablePinForGrouping == null) return false;

        viablePinForGrouping.AddObjectToPin(positionedObject);
        return true;
    }

    internal static void UnpinObject(GameObject objectToRemove) =>
        UnpinObject(PositionedObject.FromGameObject(objectToRemove));

    private static void UnpinObject(PositionedObject positionToRemove)
    {
        if (!Minimap.instance) return;

        MapPin? pinFound = PinsRef(Minimap.instance)
            .OfType<MapPin>()
            .FirstOrDefault(pin => pin.RemoveObjectFromPin(positionToRemove));

        if (pinFound == null) return;

        Minimap.instance.RemovePin((Minimap.PinData)pinFound);
    }

    internal static void UpdatePins()
    {
        if (!Minimap.instance) return;

        Log.LogInfo("updating pins");

        var pinsToRemove = new List<PositionedObject>();

        foreach (var pin in PinsRef(Minimap.instance).OfType<MapPin>())
        {
            if (Registry.ConfiguredPins.TryGetValue(pin.InternalName, out Config.Pin config))
            {
                pin.ApplyConfigUpdate(config);
            }
            else
            {
                pinsToRemove.Add(pin.GetPinnedPosition());
            }
        }

        foreach (var pinnedPosition in pinsToRemove)
        {
            UnpinObject(pinnedPosition);
        }
    }

    internal static string PrintMapPins()
    {
        string result = "";

        if (!Minimap.instance) return result;

        var pins = PinsRef(Minimap.instance).ToDictionary(
            pin => new PositionedObject(pin.m_NamePinData?.PinNameText.text ?? "no name", pin.m_pos),
            pin => pin);

        foreach (var pin in pins.OrderBy(pair => pair.Key))
        {
            if (pin.Value is MapPin mapPin)
            {
                result += mapPin + "\n";
            }
            else
            {
                result += $"vanilla pin: {pin.Key} of type '{pin.Value.m_type}'\n";
            }
        }

        return result;
    }
}