using Stavebound.Staves;
using Stavebound.Portals;
using Stavebound.Travel;
using Stavebound.UI;
using HarmonyLib;

namespace Stavebound.Patches
{
    /// <summary>
    /// Starts and stops the portal registry with the world.
    /// <para>
    /// <c>Game</c> is created when a world loads and destroyed when you leave it, which makes it the
    /// right lifetime for anything that reads ZDOs. The plugin itself outlives every world, so state
    /// that isn't cleared here leaks from one save into the next.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(Game))]
    internal static class GameLifecyclePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Game.Start))]
        private static void StartRegistry()
        {
            PortalRegistry.OnWorldStart();
            SiteSweep.Start();
        }

        // Private in the game, which Harmony does not care about.
        [HarmonyPrefix]
        [HarmonyPatch("OnDestroy")]
        private static void StopRegistry()
        {
            PortalRegistry.OnWorldEnd();
            SiteSweep.Stop();
            PlacementFeedback.Reset();
            ApproachWarning.Reset();
            DestinationSelector.Reset();
        }
    }
}
