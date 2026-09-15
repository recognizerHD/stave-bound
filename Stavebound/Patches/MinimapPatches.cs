using System;
using Stavebound.UI;
using HarmonyLib;
using UnityEngine;

namespace Stavebound.Patches
{
    /// <summary>
    /// Lends the world map to the destination selector while it is open, and stays entirely out of
    /// the way when it is not.
    /// </summary>
    [HarmonyPatch(typeof(Minimap))]
    internal static class MinimapPatches
    {
        /// <summary>
        /// The map's own pin-click distance — its remove radius scaled by the current zoom. The getter is
        /// private, so it is reached through Harmony and cached rather than through the publicised
        /// signature, which would compile and then throw at runtime (DESIGN.md §12).
        /// </summary>
        private static readonly Func<Minimap, float> PinInteractRadius =
            AccessTools.MethodDelegate<Func<Minimap, float>>(
                AccessTools.PropertyGetter(typeof(Minimap), "PinInteractRadius"));

        /// <summary>
        /// Drives the selector from the map's own update, which ties its lifetime to the thing it
        /// draws on: if the map stops, so does the selector, however the player closed it.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        private static void DriveSelector()
        {
            DestinationSelector.Update();
        }

        /// <summary>
        /// While selecting, clicking a portal's pin re-aims at it, and clicking anywhere else does
        /// nothing — no pin dialog, no ping. See <see cref="DestinationSelector.SelectNear"/>.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Minimap.OnMapLeftClick))]
        private static bool ClickToSelect(Minimap __instance)
        {
            if (!DestinationSelector.IsOpen)
            {
                return true;
            }

            // ScreenToWorldPoint is private, so it goes through Harmony rather than the publicised
            // signature, which would throw at runtime — see DESIGN.md §12.
            //
            // ZInput.pointerPosition rather than UnityEngine.Input.mousePosition: game 1.0 reads input
            // through the Input System, and vanilla's own OnMapLeftClick takes the pointer from ZInput.
            // The legacy call is not guaranteed to report anything under it.
            var world = (Vector3)AccessTools.Method(typeof(Minimap), "ScreenToWorldPoint")
                .Invoke(__instance, new object[] { ZInput.pointerPosition });

            DestinationSelector.SelectNear(world, PinInteractRadius(__instance));
            return false;
        }
    }
}
