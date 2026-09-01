using Stavebound.Config;
using Stavebound.Portals;
using Stavebound.Tiers;
using UnityEngine;

namespace Stavebound.Travel
{
    /// <summary>
    /// Answers "will this get through?" while you are packing, rather than at the doorway.
    /// <para>
    /// DESIGN.md §5 calls this the layer that matters, and the reason is timing: a refusal at the
    /// threshold tells you something you can no longer act on without walking back. A mark on the
    /// stack while your chest is open tells you in time to leave it behind or go build the rune.
    /// </para>
    /// <para>
    /// <b>Proximity-gated</b>, deliberately. Always-on would paint your ore red for the entire game
    /// and train everyone to ignore it, which costs more than it gives. Within
    /// <c>CargoPreviewRange</c> of a portal the marks mean "this portal's destination will refuse
    /// this"; outside it they keep vanilla's meaning, "this cannot teleport at all". Both are true
    /// statements, and each is the useful one where it appears.
    /// </para>
    /// <para>
    /// It touches no item data. The answer is computed from the tier map against a destination mask —
    /// flipping <c>m_shared.m_teleportable</c> to drive a UI state would leak into tooltips, other
    /// mods and everything else that asks, which is the failure mode CLAUDE.md names.
    /// </para>
    /// </summary>
    internal static class CargoPreview
    {
        /// <summary>
        /// The clearance of whatever portal the player is standing near, or null when there is no
        /// portal close enough to be talking about.
        /// <para>
        /// Resolved through the same path the travel gate uses, so what the icons promise and what the
        /// doorway does cannot disagree.
        /// </para>
        /// </summary>
        internal static bool TryGetNearbyDestination(out Clearance mask, out bool allowsEverything)
        {
            mask = Clearance.None;
            allowsEverything = false;

            if (!StaveboundConfig.ShowBlockedCargoOverlay.Value || Player.m_localPlayer == null)
            {
                return false;
            }

            TeleportWorld portal = PortalTarget.FindNearest(
                Player.m_localPlayer.transform.position,
                StaveboundConfig.CargoPreviewRange.Value);

            if (portal == null)
            {
                return false;
            }

            if (portal.m_allowAllItems)
            {
                // A portal the base game lets everything through. Nothing to warn about.
                allowsEverything = true;
                return true;
            }

            ZDO zdo = PortalTarget.ZdoOf(portal);
            if (!ClearanceGate.TryResolveDestination(zdo, out ZDO destination, out long _) || destination == null)
            {
                // Pointing nowhere, or somewhere this client has not been told about. Saying nothing
                // beats guessing, and vanilla's own marks stay as they were.
                return false;
            }

            mask = ClearanceGate.EffectiveMask(zdo, destination);
            return true;
        }

        /// <summary>
        /// Should this stack be marked as unable to make the trip?
        /// <para>
        /// Only ever true for items the game already refuses to teleport, so nothing new becomes
        /// blocked — the mark narrows from "cannot teleport" to "cannot go <em>there</em>".
        /// </para>
        /// </summary>
        internal static bool WouldBeRefused(ItemDrop.ItemData item, Clearance destinationMask, bool allowsEverything)
        {
            if (item?.m_shared == null || item.m_shared.m_teleportable)
            {
                return false;
            }

            if (allowsEverything)
            {
                return false;
            }

            string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
            return !destinationMask.Permits(TierMap.RequiredFor(prefab));
        }
    }
}
