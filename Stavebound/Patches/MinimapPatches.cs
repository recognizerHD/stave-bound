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
        /// While selecting, a click moves the highlight instead of dropping a ping. Confirming stays
        /// a separate keypress — see <see cref="DestinationSelector"/>.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Minimap.OnMapLeftClick))]
        private static bool ClickToHighlight(Minimap __instance)
        {
            if (!DestinationSelector.IsOpen)
            {
                return true;
            }

            // ScreenToWorldPoint is private, so it goes through Harmony rather than the publicised
            // signature, which would throw at runtime — see DESIGN.md §12.
            var world = (Vector3)AccessTools.Method(typeof(Minimap), "ScreenToWorldPoint")
                .Invoke(__instance, new object[] { Input.mousePosition });

            DestinationSelector.HighlightNearest(world);
            return false;
        }
    }
}
