using Stavebound.Travel;
using HarmonyLib;
using UnityEngine;

namespace Stavebound.Patches
{
    /// <summary>
    /// Ends a teleport when the destination has arrived, rather than when a timer says so.
    /// <para>
    /// The one patch seamless transit needs. Vanilla's <c>UpdateTeleport</c> holds a distant trip for
    /// eight seconds before it will so much as check whether the area is ready; clearing
    /// <c>m_distantTeleport</c> once it <em>is</em> ready lets vanilla's very next tick finish the
    /// trip through its own code path.
    /// </para>
    /// <para>
    /// Nothing is skipped that was doing anything. The loading screen still appears for a trip that
    /// needs one, and still stays up for as long as the zone genuinely takes — this only removes the
    /// portion of the wait that was a constant.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(Player))]
    internal static class TransitPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("UpdateTeleport")]
        private static void EndTheWaitWhenTheWorldIsThere(
            bool ___m_teleporting,
            ref bool ___m_distantTeleport,
            Vector3 ___m_teleportTargetPos)
        {
            if (!___m_teleporting || !___m_distantTeleport)
            {
                return;
            }

            if (SeamlessTransit.ArrivalIsReady(___m_teleportTargetPos))
            {
                // The condition vanilla was going to wait for is already true. Only the timer was
                // still holding, and it is not measuring anything.
                ___m_distantTeleport = false;
            }
        }
    }
}
