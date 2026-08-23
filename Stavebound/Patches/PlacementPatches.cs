using Stavebound.Staves;
using HarmonyLib;
using UnityEngine;

namespace Stavebound.Patches
{
    /// <summary>
    /// Drives the build-mode feedback from the game's own placement update, so it appears exactly
    /// while a ghost is on screen and stops the moment it is not.
    /// </summary>
    [HarmonyPatch(typeof(Player))]
    internal static class PlacementPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("UpdatePlacementGhost")]
        private static void ShowStaveRange(GameObject ___m_placementGhost)
        {
            PlacementFeedback.Update(___m_placementGhost);
        }
    }
}
