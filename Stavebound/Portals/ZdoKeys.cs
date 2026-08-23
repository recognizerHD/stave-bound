using System.Collections.Generic;

namespace Stavebound.Portals
{
    /// <summary>
    /// The ZDO keys this mod owns, hashed once at load.
    /// <para>
    /// Every key is prefixed <c>stave_</c>. That is a functional requirement, not a courtesy:
    /// two mods writing the same key on the same world corrupt each other's state, and unlike a
    /// name collision it fails silently. See DESIGN.md §11.
    /// </para>
    /// <para>
    /// Vanilla caches its own key hashes as statics on <c>ZDOVars</c> for the same reason we do —
    /// <see cref="StringExtensionMethods.GetStableHashCode"/> runs over the whole string, and these
    /// are read in loops over every portal in the world.
    /// </para>
    /// </summary>
    internal static class ZdoKeys
    {
        /// <summary>
        /// This portal's permanent identity, assigned once by the server and never reused.
        /// <para>
        /// It exists because <b>a ZDOID is not a persistent reference</b>. Every ZDO in the world is
        /// renumbered on every save and load — measured, not assumed: across one logout a portal went
        /// from <c>1:20372</c> to <c>1:20375</c>, and one created that session went from
        /// <c>2261713014:42343</c> to <c>1:32429</c>. A stored ZDOID therefore points at nothing, or
        /// worse at whatever inherited its number. Vanilla has the same problem and solves it by
        /// persisting portal connections as hash data and rebuilding the ids after load; we solve it
        /// by never depending on the game's numbering at all. See DESIGN.md §12.
        /// </para>
        /// </summary>
        internal static readonly int Pid = "stave_pid".GetStableHashCode();

        /// <summary>
        /// Where this portal sends you: the destination's <see cref="Pid"/>, resolved to a live ZDO
        /// through the registry.
        /// <para>
        /// Deliberately <em>not</em> vanilla's <c>ConnectionType.Portal</c> connection. The server
        /// rebuilds those from tag matches every five seconds and clears any whose ends disagree,
        /// so a one-way target written there survives seconds at most — see DESIGN.md §12. Keeping
        /// ours separate also leaves vanilla's tag pairing running underneath as the fallback §5
        /// asks for, at no cost: a portal nobody has re-aimed still behaves exactly as it always did.
        /// </para>
        /// </summary>
        internal static readonly int Destination = "stave_dest".GetStableHashCode();

        /// <summary>
        /// The first attempt, which stored the destination's ZDOID and broke on the first relog. The
        /// server strips it when it sees it, so worlds written by an earlier build clean themselves
        /// up rather than carrying a key that means nothing.
        /// </summary>
        internal static readonly KeyValuePair<int, int> LegacyTarget = ZDO.GetHashZDOID("stave_target");

        /// <summary>
        /// The clearance mask granted to this portal by the staves bound to it — per-tier flags, not
        /// a level (R1). Written by the server only, mirrored from each stave onto every portal in
        /// radius, and read by clients that cannot see the staves themselves. Phase 2 fills it;
        /// Phase 1 carries it through the registry so the wire format does not change later.
        /// </summary>
        internal static readonly int ClearanceMask = "stave_mask".GetStableHashCode();
    }
}
