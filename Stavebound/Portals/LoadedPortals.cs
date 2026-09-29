using System.Collections.Generic;
using UnityEngine;

namespace Stavebound.Portals
{
    /// <summary>
    /// The portals loaded on this machine right now, kept as a list so that "which portal am I
    /// standing at?" costs a walk over a handful of entries instead of a walk over the scene.
    /// <para>
    /// <b>This exists because of a bug worth not repeating.</b> The question used to be answered with
    /// <c>Object.FindObjectsByType&lt;TeleportWorld&gt;</c>, which walks every component of that type
    /// in the scene and allocates an array to hand back. Called once from a console command that is
    /// what it looks like: free. But the cargo overlay also asks, from a postfix on
    /// <c>InventoryGrid.UpdateGui</c> — and the game calls that every frame, for the player's grid and
    /// again for an open chest's, for as long as the panel is open. That turned a scene-wide search
    /// into a per-frame one and cost players most of their framerate while their inventory was open,
    /// on a world with no portals in it at all: the search is priced by how much is loaded, not by how
    /// many portals it finds.
    /// </para>
    /// <para>
    /// Maintained from <c>TeleportWorld.Awake</c>, which every portal runs as it loads. There is no
    /// matching <c>OnDestroy</c> on the game's side to hook, so entries are dropped lazily instead:
    /// Unity's <c>==</c> reports a destroyed component as null, so <see cref="Current"/> drops what
    /// has gone on its way past. That makes unloading free and costs one null check per portal per
    /// question, which is the right way round — portals unload in crowds when you sail away, and the
    /// questions are asked by things that are already looping.
    /// </para>
    /// </summary>
    internal static class LoadedPortals
    {
        /// <summary>
        /// Pruned in place by <see cref="Current"/>, which everything goes through. Holds only
        /// portals, so it stays small — tens of entries in a well-built world, against the tens of
        /// thousands of objects a scene search walks.
        /// </summary>
        private static readonly List<TeleportWorld> Live = new List<TeleportWorld>();

        /// <summary>
        /// A portal has loaded. Called from <c>TeleportWorld.Awake</c>, before the portal has a ZDO —
        /// callers that need one ask <see cref="PortalTarget.ZdoOf"/> when they read the list, which
        /// they have to do anyway for the frame or two after a portal is built.
        /// <para>
        /// The duplicate check is for a portal whose <c>Awake</c> runs twice, which is not something
        /// the game does but is well within what another mod might cause. It is a scan of a short
        /// list, once per portal loaded.
        /// </para>
        /// </summary>
        internal static void Remember(TeleportWorld portal)
        {
            if (portal == null)
            {
                return;
            }

            // Through Current rather than the field, so the pruning happens even for a player who
            // sails past a hundred portals without once opening a chest or holding a stave. It walks
            // the same short list the duplicate check has to walk anyway.
            List<TeleportWorld> live = Current();
            if (!live.Contains(portal))
            {
                live.Add(portal);
            }
        }

        /// <summary>
        /// Every loaded portal, with the destroyed ones dropped first.
        /// <para>
        /// Returns the list itself rather than a copy, because the callers only read it and copying
        /// per frame is the kind of cost this class was written to remove. Treat it as read-only, and
        /// do not hold onto it across frames.
        /// </para>
        /// </summary>
        internal static List<TeleportWorld> Current()
        {
            // Backwards, so removing an entry cannot move one we have not looked at yet.
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                if (Live[i] == null)
                {
                    Live.RemoveAt(i);
                }
            }

            return Live;
        }

        /// <summary>
        /// Leaving a world destroys every portal in it. Clearing here rather than waiting for the
        /// pruning keeps the list from carrying a world's worth of dead entries into the next one.
        /// </summary>
        internal static void Forget()
        {
            Live.Clear();
        }
    }
}
