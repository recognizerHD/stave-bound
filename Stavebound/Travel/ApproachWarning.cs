using Stavebound.Config;
using Stavebound.Portals;
using UnityEngine;

namespace Stavebound.Travel
{
    /// <summary>
    /// Tells a player their load will be refused as they walk up to a portal, rather than as they
    /// walk into it.
    /// <para>
    /// DESIGN.md §7 argues for this as the answer to the base game loop, not merely to seamless
    /// transit: since §5 settled on rewire, <em>every</em> trip is a walk-in and there is never a
    /// dialog to carry a refusal. Vanilla's stone wall tells you at the threshold, which is a moment
    /// too late to be useful and just early enough to be annoying.
    /// </para>
    /// <para>
    /// The quiet half of this already exists — the portal's runes go dark when the destination will
    /// not take what you hold. That is the right default: ambient, ignorable, no interruption. This is
    /// the loud half, and it says the thing the glow cannot: <em>which</em> item, and <em>which</em>
    /// stave is missing.
    /// </para>
    /// </summary>
    internal static class ApproachWarning
    {
        /// <summary>
        /// The portal most recently warned about, so walking up to one produces one message rather
        /// than one per frame. Cleared when the player leaves, so returning warns again — by then it
        /// is news rather than nagging.
        /// </summary>
        private static ZDOID _warnedAbout = ZDOID.None;

        /// <summary>
        /// Called for each portal the glow updates, which is already once per frame per portal with a
        /// player nearby — so this costs a comparison in the common case where nothing has changed.
        /// </summary>
        internal static void Consider(ZDO portal, ZDO destination, Player nearby, bool allowed)
        {
            if (portal == null)
            {
                return;
            }

            // Only ever about the person reading the screen. GetClosestPlayer finds anyone, and being
            // told about someone else's cargo would be baffling.
            if (nearby == null || nearby != Player.m_localPlayer)
            {
                Forget(portal);
                return;
            }

            if (allowed || !StaveboundConfig.WarnOnApproach.Value)
            {
                Forget(portal);
                return;
            }

            if (_warnedAbout == portal.m_uid)
            {
                // Already said. Standing here does not make it truer.
                return;
            }

            ClearanceGate.Refusal refusal = ClearanceGate.FirstRefusal(
                nearby.GetInventory(),
                ClearanceGate.MaskOf(destination));

            if (refusal == null)
            {
                return;
            }

            _warnedAbout = portal.m_uid;
            nearby.Message(
                MessageHud.MessageType.Center,
                ClearanceGate.Explain(refusal, destination.GetString(ZDOVars.s_tag, string.Empty)));
        }

        /// <summary>
        /// Forgets a portal once it is no longer refusing this player, so the next genuine refusal is
        /// announced instead of swallowed.
        /// </summary>
        private static void Forget(ZDO portal)
        {
            if (_warnedAbout == portal.m_uid)
            {
                _warnedAbout = ZDOID.None;
            }
        }

        internal static void Reset() => _warnedAbout = ZDOID.None;
    }
}
