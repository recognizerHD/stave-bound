using HarmonyLib;
using Stavebound.UI;

namespace Stavebound.Patches
{
    /// <summary>
    /// Shares the mouse wheel between the world map and the destination selector drawn over it.
    /// <para>
    /// The map is the only thing in the game that reads the wheel — <c>Minimap.UpdateMap</c>, which
    /// zooms with it — so the reading is taken here, on its way there. While the pointer is over the
    /// selector, the selector keeps it and the map sees nothing; everywhere else the map zooms exactly
    /// as it always did. Patching the read rather than the zoom means the zoom is never reimplemented,
    /// and one roll of the wheel never does two things at once.
    /// </para>
    /// <para>
    /// <c>UpdateMap</c> is called only from <c>Minimap.Update</c>, whose postfix drives the selector,
    /// so the reading captured here is always from the same frame the selector acts on it.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))]
    internal static class SelectorWheelPatches
    {
        [HarmonyPostfix]
        private static void LendTheWheelToTheSelector(ref float __result)
        {
            if (__result == 0f || !DestinationSelector.OwnsWheel())
            {
                return;
            }

            DestinationSelector.ReceiveWheel(__result);
            __result = 0f;
        }
    }
}
