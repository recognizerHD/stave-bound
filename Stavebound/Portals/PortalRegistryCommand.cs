using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using UnityEngine;

namespace Stavebound.Portals
{
    /// <summary>
    /// <c>stave_portals</c> — print what this instance believes about the world's portals.
    /// <para>
    /// The registry has no visible effect until the selector exists, so without this there is no way
    /// to tell a working sync from a silently empty one. It stays useful past Phase 1: the whole
    /// design turns on a client holding accurate data about portals it cannot see, and this is how
    /// you check whether it does.
    /// </para>
    /// <para>
    /// Read-only, so not a cheat and not server-only — the interesting question is usually what a
    /// <em>client</em> thinks it knows.
    /// </para>
    /// </summary>
    internal sealed class PortalRegistryCommand : StaveboundCommand
    {
        public override string Name => "stave_portals";

        public override string Help =>
            "List the portals Stavebound knows about, nearest first. " +
            "On a client this is what the server has told you, not what is loaded around you.";

        protected override void Execute(string[] args, Terminal context)
        {
            IReadOnlyList<PortalRecord> portals = PortalRegistry.All;
            string role = ZNet.instance == null
                ? "no world loaded"
                : ZNet.instance.IsServer() ? "server" : "client";

            if (portals.Count == 0)
            {
                Echo(context,$"Stavebound ({role}): no portals known.");
                return;
            }

            Vector3 from = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero;

            Echo(context,$"Stavebound ({role}): {portals.Count} portal(s).");

            foreach (PortalRecord portal in portals.OrderBy(p => Vector3.Distance(from, p.Position)))
            {
                string target = portal.TargetPid == PortalTarget.NoPid
                    ? "vanilla tag pairing"
                    : PortalRegistry.TryGet(portal.TargetPid, out PortalRecord destination)
                        ? destination.ToString()
                        : "a portal that no longer exists";

                Echo(context,
                    $"  {portal} at {portal.Position.x:F0},{portal.Position.z:F0} " +
                    $"({Vector3.Distance(from, portal.Position):F0}m) " +
                    $"-> {target}, clearance 0x{portal.ClearanceMask:X}");
            }
        }
    }
}
