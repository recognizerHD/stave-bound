using System;
using HarmonyLib;
using UnityEngine;

namespace Stavebound.Portals
{
    /// <summary>
    /// Reads and writes a portal's identity and its one-way target — the state the rewire model
    /// stands on.
    /// <para>
    /// Both are referred to by <see cref="ZdoKeys.Pid"/>, never by ZDOID, because the game renumbers
    /// every ZDO on every world load (DESIGN.md §12). A ZDOID is a valid handle for as long as you
    /// are logged in and worthless the moment you are not.
    /// </para>
    /// <para>
    /// Ours also lives outside vanilla's portal connection, which the server rebuilds from tag
    /// matches every five seconds. The happy side effect is that a portal nobody has re-aimed still
    /// pairs by tag exactly as it always did — the vanilla fallback §5 asks for, delivered by
    /// leaving well alone.
    /// </para>
    /// </summary>
    internal static class PortalTarget
    {
        /// <summary>
        /// <c>TeleportWorld.m_nview</c> is private, and Jotunn's publicised assemblies only make it
        /// look otherwise to the compiler. At runtime the real assembly is loaded and this game
        /// build's Mono enforces the access check, so reading the field directly compiles cleanly
        /// and then throws <c>FieldAccessException</c> on every call — see DESIGN.md §12.
        /// <para>
        /// Harmony's field accessor is emitted once and costs about what the field access would
        /// have. Patch methods get the same field injected as a <c>___m_nview</c> parameter instead,
        /// which is cheaper still; this exists for the callers that are not patches.
        /// </para>
        /// </summary>
        private static readonly AccessTools.FieldRef<TeleportWorld, ZNetView> NetView =
            AccessTools.FieldRefAccess<TeleportWorld, ZNetView>("m_nview");

        /// <summary>No portal ever has this id, so it doubles as "not set".</summary>
        internal const long NoPid = 0L;

        /// <summary>This portal's permanent id, or <see cref="NoPid"/> if the server hasn't been round yet.</summary>
        internal static long GetPid(ZDO zdo) => zdo == null ? NoPid : zdo.GetLong(ZdoKeys.Pid, NoPid);

        /// <summary>The pid this portal sends you to, or <see cref="NoPid"/> if it has never been re-aimed.</summary>
        internal static long GetDestination(ZDO zdo) => zdo == null ? NoPid : zdo.GetLong(ZdoKeys.Destination, NoPid);

        /// <summary>Convenience for the common case of asking a live portal component.</summary>
        internal static long GetDestination(TeleportWorld portal) => GetDestination(ZdoOf(portal));

        /// <summary>
        /// The ZDO behind a loaded portal, or null if it has none yet — which happens for a frame or
        /// two after the piece spawns.
        /// </summary>
        internal static ZDO ZdoOf(TeleportWorld portal)
        {
            if (portal == null)
            {
                return null;
            }

            ZNetView view = NetView(portal);
            return view != null && view.IsValid() ? view.GetZDO() : null;
        }

        /// <summary>
        /// Gives a portal its permanent id, if it hasn't got one. <b>Server only</b> — one author
        /// means no chance of two peers inventing different ids for the same portal.
        /// <para>
        /// Also strips the abandoned ZDOID-based key, so a world written by an earlier build stops
        /// carrying a reference that cannot mean anything.
        /// </para>
        /// </summary>
        internal static long EnsurePid(ZDO zdo, Func<long, bool> isTaken)
        {
            if (zdo == null)
            {
                return NoPid;
            }

            long pid = GetPid(zdo);
            bool legacy = !zdo.GetZDOID(ZdoKeys.LegacyTarget).IsNone();

            // Re-mint on collision as well as on absence. Nothing in the base game can duplicate a
            // pid, but a blueprint mod that clones a placed portal would copy its ZDO wholesale, and
            // two portals sharing an id would silently cross-wire their destinations.
            bool needsPid = pid == NoPid || isTaken(pid);
            if (!needsPid && !legacy)
            {
                return pid;
            }

            Claim(zdo);

            if (needsPid)
            {
                pid = Mint();
                zdo.Set(ZdoKeys.Pid, pid);
            }

            if (legacy)
            {
                zdo.RemoveZDOID(ZdoKeys.LegacyTarget);
            }

            Publish(zdo);
            return pid;
        }

        /// <summary>
        /// Points a portal at another one. Pointers are one-way by design: this writes nothing to
        /// the destination, so B may well still point somewhere else entirely (DESIGN.md §5).
        /// </summary>
        internal static void Set(ZDO zdo, long destinationPid)
        {
            if (zdo == null || destinationPid == NoPid)
            {
                return;
            }

            Claim(zdo);
            zdo.Set(ZdoKeys.Destination, destinationPid);
            Publish(zdo);
        }

        /// <summary>
        /// Drops the explicit target, handing the portal back to vanilla tag pairing rather than
        /// leaving it pointing nowhere.
        /// </summary>
        internal static void Clear(ZDO zdo)
        {
            if (zdo == null)
            {
                return;
            }

            Claim(zdo);
            zdo.Set(ZdoKeys.Destination, NoPid);
            Publish(zdo);
        }

        /// <summary>
        /// 64 random bits. Not sequential: a counter would have to be stored somewhere world-wide and
        /// kept correct across every server restart, and getting that wrong reuses an id, which is
        /// the one failure this whole mechanism exists to prevent.
        /// </summary>
        private static long Mint()
        {
            long pid = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
            return pid == NoPid ? 1L : pid;
        }

        /// <summary>
        /// A ZDO can only be written by whoever owns it. Taking ownership of a portal you are
        /// standing at is ordinary Valheim behaviour — players take ownership of nearby objects
        /// constantly — and re-aiming is deliberate and rare enough that a last-writer-wins race
        /// between two players at the same portal is the contention §5 already accepts, not a bug
        /// to engineer around. Vanilla claims portal ZDOs the same way when it repairs connections.
        /// </summary>
        internal static void Claim(ZDO zdo)
        {
            if (!zdo.IsOwner())
            {
                zdo.SetOwner(ZDOMan.GetSessionID());
            }
        }

        /// <summary>
        /// Pushes the change out now instead of waiting for the next routine sync. Re-aiming is a
        /// thing a player does and then immediately walks into, so the usual lazy propagation is
        /// exactly the wrong trade here. Vanilla does the same after rewriting portal connections.
        /// </summary>
        internal static void Publish(ZDO zdo)
        {
            ZDOMan.instance?.ForceSendZDO(zdo.m_uid);
        }

        /// <summary>
        /// The loaded portal nearest a point, within <paramref name="range"/> metres.
        /// <para>
        /// Only finds portals that are loaded, which is fine for its callers: you are standing at the
        /// portal you mean. This is also the rule §5 settles on for "the portal you are near" when
        /// two are close together — nearest within range, the same way an anchor picks the portal it
        /// grants clearance to.
        /// </para>
        /// </summary>
        internal static TeleportWorld FindNearest(Vector3 point, float range)
        {
            TeleportWorld nearest = null;
            float nearestDistance = range * range;

            // Unsorted: we are picking the minimum ourselves, so paying for InstanceID ordering
            // would be pure waste.
            // Fully qualified: `using System` above makes a bare Object ambiguous.
            foreach (TeleportWorld candidate in UnityEngine.Object.FindObjectsByType<TeleportWorld>(FindObjectsSortMode.None))
            {
                if (ZdoOf(candidate) == null)
                {
                    continue;
                }

                float distance = (candidate.transform.position - point).sqrMagnitude;
                if (distance > nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearest = candidate;
            }

            return nearest;
        }
    }
}
