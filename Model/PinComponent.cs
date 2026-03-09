using System.Collections;
using System.Reflection;
using AutoMapPins.Patches;
using HarmonyLib;
using UnityEngine;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace AutoMapPins.Model;

internal class PinComponent : MonoBehaviour
{
    private static readonly MethodInfo? IsExploredMethod =
        AccessTools.DeclaredMethod(typeof(Minimap), "IsExplored", new[] { typeof(Vector3) });

    private PinComponent()
    {
    }

    private Vector3 Position;
    private bool IsVisible;
    private Coroutine Routine = null!;

    internal void Awake()
    {
        Position = gameObject.transform.position;
        IsVisible = Minimap.instance && CallIsExplored(Minimap.instance, transform.position);
        SetVisiblePin();
    }

    internal void OnDestroy()
    {
        if (Routine != null) StopCoroutine(Routine);
        MinimapPatch.UnpinObject(gameObject);
    }

    private IEnumerator VisibleCheck()
    {
        while (!IsVisible)
        {
            IsVisible = Minimap.instance && CallIsExplored(Minimap.instance, Position);
            if (IsVisible)
            {
                SetVisiblePin();
                yield break;
            }

            yield return new WaitForFixedUpdate();
        }
    }

    private void SetVisiblePin()
    {
        if (IsVisible)
        {
            MinimapPatch.UpsertPin(gameObject);
        }
        else
        {
            if (Routine != null) StopCoroutine(Routine);
            Routine = StartCoroutine(VisibleCheck());
        }
    }

    private static bool CallIsExplored(Minimap minimap, Vector3 position)
    {
        if (IsExploredMethod == null) return false;

        object? result = IsExploredMethod.Invoke(minimap, new object[] { position });
        return result is bool explored && explored;
    }
}