using System.Collections.Generic;
using Stavebound.Tiers;
using Stavebound.Travel;
using HarmonyLib;

namespace Stavebound.Patches
{
    /// <summary>
    /// Narrows vanilla's per-slot "cannot teleport" mark to "cannot go where this portal points".
    /// <para>
    /// Vanilla already draws the icon, on every slot holding something non-teleportable. That mark is
    /// true but blunt once clearance exists: standing at a portal whose destination happily accepts
    /// copper, every copper stack in the chest is still crossed out. A postfix re-answers the question
    /// per slot, leaving vanilla's answer alone whenever there is no portal nearby to be talking about.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid))]
    internal static class CargoPreviewPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("UpdateGui")]
        private static void MarkWhatTheDestinationRefuses(
            List<InventoryGrid.Element> ___m_elements,
            Inventory ___m_inventory)
        {
            if (___m_elements == null || ___m_inventory == null)
            {
                return;
            }

            if (!CargoPreview.TryGetNearbyDestination(out Clearance mask, out bool allowsEverything))
            {
                // No portal in range: vanilla's meaning is the right one and stays untouched.
                return;
            }

            foreach (InventoryGrid.Element element in ___m_elements)
            {
                if (element?.m_noteleport == null || !element.m_used)
                {
                    continue;
                }

                ItemDrop.ItemData item = ___m_inventory.GetItemAt(element.m_pos.x, element.m_pos.y);
                element.m_noteleport.enabled = CargoPreview.WouldBeRefused(item, mask, allowsEverything);
            }
        }
    }
}
