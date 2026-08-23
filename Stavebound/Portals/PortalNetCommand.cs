using System.Collections.Generic;
using System.Linq;

namespace Stavebound.Portals
{
    /// <summary>
    /// <c>stave_net</c> — what this instance's portal sync is actually doing.
    /// <para>
    /// Answers the questions that decide where a multiplayer fault lies, without needing the log:
    /// am I the server or a client, has anything ever arrived, and do the destinations I hold
    /// resolve to portals I know about. Run it on both machines and the disagreement is the bug.
    /// </para>
    /// </summary>
    internal sealed class PortalNetCommand : StaveboundCommand
    {
        public override string Name => "stave_net";

        public override string Help =>
            "Report the portal registry's network state: role, peers, traffic, and whether every " +
            "destination resolves. Run on the server and the client and compare.";

        protected override void Execute(string[] args, Terminal context)
        {
            if (ZNet.instance == null)
            {
                Echo(context,"Stavebound: no world loaded, so there is no sync to report on.");
                return;
            }

            bool server = ZNet.instance.IsServer();
            bool dedicated = ZNet.instance.IsDedicated();
            List<ZNetPeer> peers = ZNet.instance.GetConnectedPeers() ?? new List<ZNetPeer>();

            string role = server
                ? dedicated ? "DEDICATED SERVER" : peers.Count > 0 ? "HOST (server + client)" : "SINGLE PLAYER (own server)"
                : "CLIENT";

            Echo(context,$"Stavebound sync - {role}");
            Echo(context,$"  connected peers: {peers.Count}" +
                              (peers.Count > 0 ? $" ({string.Join(", ", peers.Select(p => p.m_uid.ToString()))})" : string.Empty));
            Echo(context,$"  {PortalRegistry.Traffic}");

            IReadOnlyList<PortalRecord> portals = PortalRegistry.All;
            Echo(context,$"  portals known: {portals.Count}");

            if (server && peers.Count == 0)
            {
                Echo(context,"  Note: nothing is broadcast with no peers connected, so an absence of " +
                                  "traffic here is expected rather than a fault.");
            }

            if (!server && portals.Count == 0)
            {
                Echo(context,"  A client with no portals has not been told anything. Either the server " +
                                  "is not running Stavebound, or the initial sync did not arrive.");
                return;
            }

            // The check that matters: a target that does not resolve means the two sides disagree
            // about what exists, which is the failure the registry is supposed to make impossible.
            List<PortalRecord> aimed = portals.Where(p => p.TargetPid != PortalTarget.NoPid).ToList();
            List<PortalRecord> dangling = aimed.Where(p => !PortalRegistry.TryGet(p.TargetPid, out _)).ToList();

            Echo(context,$"  re-aimed: {aimed.Count}, of which {dangling.Count} point at something " +
                              "this instance does not know about");

            foreach (PortalRecord portal in dangling)
            {
                Echo(context,$"    {portal} -> pid {portal.TargetPid}, unresolved");
            }
        }
    }
}
