using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Stavebound.Config;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Stavebound.Portals
{
    /// <summary>
    /// Every portal in the world, and the one place the rest of the mod asks about them.
    /// <para>
    /// Server-authoritative by necessity rather than by taste. A client's <c>ZDOMan</c> only holds
    /// the ZDOs near it, so <c>ZDOMan.GetPortalList()</c> on a client returns whatever happens to be
    /// loaded — never the world. Only the server has the full set, so the server builds the list and
    /// pushes it; clients only ever read what they were sent. See DESIGN.md §6.
    /// </para>
    /// <para>
    /// On a single-player or client-hosted game the local instance <em>is</em> the server, so the
    /// sweep fills this directly and no packet is ever sent.
    /// </para>
    /// </summary>
    internal static class PortalRegistry
    {
        /// <summary>
        /// Guards against a wire-format change between builds that Jotunn's version check lets
        /// through. <c>VersionStrictness.Minor</c> only pins major.minor, so two patch releases can
        /// meet on one server; a mismatched package is then refused loudly instead of being read as
        /// garbage.
        /// </summary>
        private const byte WireVersion = 2;

        /// <summary>
        /// How often the server rechecks the world for portals placed, destroyed, renamed or
        /// re-aimed. Portals change on a human timescale, so this is deliberately lazy — nothing
        /// waits on it, because whoever changes a portal writes the ZDO themselves and only other
        /// players need telling.
        /// </summary>
        private const float ServerSweepSeconds = 2f;

        private static readonly List<PortalRecord> Ordered = new List<PortalRecord>();
        private static readonly Dictionary<long, PortalRecord> ByPid = new Dictionary<long, PortalRecord>();

        /// <summary>Scratch list for the server sweep, reused so a 2 s timer doesn't allocate forever.</summary>
        private static readonly List<PortalRecord> SweepBuffer = new List<PortalRecord>();

        /// <summary>
        /// Pids handed out during the sweep in progress, so a duplicate is caught the moment it is
        /// seen rather than after both portals have been published.
        /// </summary>
        private static readonly HashSet<long> SweepPids = new HashSet<long>();

        private static CustomRPC _rpc;
        private static Coroutine _sweep;

        // Kept for stave_net, so "has this client ever heard from the server" is answerable
        // without trawling the log.
        private static int _lastSentBytes;
        private static int _lastReceivedBytes;
        private static int _receiveCount;

        /// <summary>What the sync has actually done, for <c>stave_net</c> to report.</summary>
        internal static string Traffic =>
            $"packages received: {_receiveCount}" +
            (_receiveCount > 0 ? $" (last {_lastReceivedBytes} bytes)" : string.Empty) +
            (_lastSentBytes > 0 ? $"; last broadcast {_lastSentBytes} bytes" : "; nothing broadcast yet");

        /// <summary>
        /// Every known portal. Ordered as the server found them, which is stable enough to page
        /// through and is not meant to be relied on beyond that — sort it for display.
        /// </summary>
        internal static IReadOnlyList<PortalRecord> All => Ordered;

        /// <summary>
        /// Look up a single portal by its permanent id — resolving a stored destination, usually.
        /// This is the step that turns a reference that survives relogs into a ZDOID you can reach
        /// this session.
        /// </summary>
        internal static bool TryGet(long pid, out PortalRecord record) => ByPid.TryGetValue(pid, out record);

        /// <summary>
        /// Registers the sync RPC. Called once from <see cref="Plugin"/>, before any world exists.
        /// </summary>
        internal static void Register()
        {
            _rpc = NetworkManager.Instance.AddRPC("stave_portals", OnServerReceive, OnClientReceive);

            // Fires on the server once a client has logged in but before it loads into the world, so
            // a joining player has the full list in hand before they can walk into anything.
            SynchronizationManager.Instance.AddInitialSynchronization(_rpc, BuildSnapshotForJoiningClient);

            CommandManager.Instance.AddConsoleCommand(new PortalRegistryCommand());
            CommandManager.Instance.AddConsoleCommand(new PortalAimCommand());
            CommandManager.Instance.AddConsoleCommand(new PortalNetCommand());
            CommandManager.Instance.AddConsoleCommand(new Tiers.BlockedItemsCommand());
            CommandManager.Instance.AddConsoleCommand(new Tiers.PrefabSearchCommand());
            CommandManager.Instance.AddConsoleCommand(new Tiers.PrefabInspectCommand());
            CommandManager.Instance.AddConsoleCommand(new Tiers.PrefabPreviewCommand());
        }

        /// <summary>Called when a world starts, from the <c>Game.Start</c> patch.</summary>
        internal static void OnWorldStart()
        {
            Clear();

            if (!IsServer())
            {
                // Clients wait to be told. AddInitialSynchronization has already sent, or is about
                // to send, the snapshot for this login.
                SyncLog.Say("World started as a CLIENT. Waiting for the server's portal list; " +
                            "nothing will be known until it arrives.");
                return;
            }

            bool dedicated = ZNet.instance != null && ZNet.instance.IsDedicated();
            SyncLog.Say($"World started as {(dedicated ? "a DEDICATED SERVER" : "the SERVER (host or single player)")}. " +
                        $"Sweeping every {ServerSweepSeconds}s; broadcasting only on change.");

            _sweep = Plugin.Instance.StartCoroutine(SweepRoutine());
        }

        /// <summary>Called when a world ends, from the <c>Game.OnDestroy</c> patch.</summary>
        internal static void OnWorldEnd()
        {
            if (_sweep != null)
            {
                // Plugin outlives the world; if it is being torn down too, the coroutine dies with it.
                if (Plugin.Instance != null)
                {
                    Plugin.Instance.StopCoroutine(_sweep);
                }

                _sweep = null;
            }

            Clear();
        }

        private static void Clear()
        {
            Ordered.Clear();
            ByPid.Clear();
            SweepBuffer.Clear();
            SweepPids.Clear();
        }

        private static bool IsServer() => ZNet.instance != null && ZNet.instance.IsServer();

        // -- Server side ---------------------------------------------------------------------------

        private static IEnumerator SweepRoutine()
        {
            var wait = new WaitForSeconds(ServerSweepSeconds);

            while (true)
            {
                if (RebuildFromWorld())
                {
                    Broadcast();
                }

                yield return wait;
            }
        }

        /// <summary>
        /// Rereads every portal ZDO the server holds. Returns true when the result differs from what
        /// we last published, which is the only thing that puts a packet on the wire.
        /// </summary>
        private static bool RebuildFromWorld()
        {
            SweepBuffer.Clear();
            SweepPids.Clear();

            // Game 1.0 buckets portal ZDOs by sector — GetPortals() now hands back a
            // Dictionary<SectorIndex, List<ZDO>>. GetPortalList() flattens that, and unlike the old
            // GetPortals() it allocates a fresh list per call rather than exposing the live one. A
            // few hundred references every couple of seconds is nothing beside the diff below, and
            // it removes the old hazard of mutating the game's own collection.
            List<ZDO> portals = ZDOMan.instance?.GetPortalList();
            if (portals == null)
            {
                return false;
            }

            foreach (ZDO zdo in portals)
            {
                if (zdo == null || !zdo.IsValid())
                {
                    continue;
                }

                // The server is the only thing that hands out permanent ids, which is what keeps
                // them unique without any coordination.
                long existing = PortalTarget.GetPid(zdo);
                long pid = PortalTarget.EnsurePid(zdo, SweepPids.Contains);
                if (pid == PortalTarget.NoPid)
                {
                    continue;
                }

                if (existing != pid)
                {
                    SyncLog.Say(existing == PortalTarget.NoPid
                        ? $"Minted pid {pid} for a portal at {zdo.GetPosition().x:F0},{zdo.GetPosition().z:F0}."
                        : $"Re-minted pid {existing} -> {pid}: another portal already claimed it.");
                }

                SweepPids.Add(pid);

                SweepBuffer.Add(new PortalRecord(
                    pid,
                    zdo.m_uid,
                    zdo.GetString(ZDOVars.s_tag, string.Empty),
                    zdo.GetPosition(),
                    PortalTarget.GetDestination(zdo),
                    zdo.GetInt(ZdoKeys.ClearanceMask, 0)));
            }

            if (Matches(SweepBuffer))
            {
                return false;
            }

            Apply(SweepBuffer);
            return true;
        }

        private static bool Matches(List<PortalRecord> candidate)
        {
            if (candidate.Count != Ordered.Count)
            {
                return false;
            }

            for (int i = 0; i < candidate.Count; i++)
            {
                if (!candidate[i].Equals(Ordered[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void Broadcast()
        {
            List<ZNetPeer> peers = ZNet.instance?.GetConnectedPeers();
            if (peers == null || peers.Count == 0)
            {
                // Single player, or a dedicated server with nobody on it. Worth saying: on a solo
                // test this is the line that explains why no packet ever appears.
                SyncLog.Say($"Change not broadcast - no connected peers. {Ordered.Count} portal(s) held locally.");
                return;
            }

            if (_rpc == null)
            {
                SyncLog.Warn("No RPC registered; connected clients will never receive the portal list.");
                return;
            }

            ZPackage package = BuildSnapshot();
            _lastSentBytes = package.Size();

            SyncLog.Say($"Broadcasting {Ordered.Count} portal(s), {_lastSentBytes} bytes, to {peers.Count} peer(s): " +
                        $"{string.Join(", ", peers.Select(p => p.m_uid.ToString()))}");

            _rpc.SendPackage(peers, package);
        }

        private static ZPackage BuildSnapshotForJoiningClient()
        {
            // Cheap insurance against the sweep not having run since the last change: the joining
            // client's copy is the one nobody gets to correct until the next change lands.
            RebuildFromWorld();

            ZPackage package = BuildSnapshot();
            SyncLog.Say($"A client is joining - sending the initial portal list: {Ordered.Count} portal(s), " +
                        $"{package.Size()} bytes. This is guaranteed to arrive before they load in.");
            return package;
        }

        private static ZPackage BuildSnapshot()
        {
            var package = new ZPackage();
            package.Write(WireVersion);
            package.Write(Ordered.Count);

            foreach (PortalRecord record in Ordered)
            {
                record.WriteTo(package);
            }

            return package;
        }

        // -- Client side ---------------------------------------------------------------------------

        private static IEnumerator OnClientReceive(long sender, ZPackage package)
        {
            int bytes = package.Size();
            byte version = package.ReadByte();
            if (version != WireVersion)
            {
                Jotunn.Logger.LogError(
                    $"Ignoring a portal list in wire format {version}; this build speaks {WireVersion}. " +
                    "The server and this client are running different Stavebound builds - portal " +
                    "destinations will not work until they match.");
                yield break;
            }

            int count = package.ReadInt();
            var received = new List<PortalRecord>(count);
            for (int i = 0; i < count; i++)
            {
                received.Add(PortalRecord.ReadFrom(package));
            }

            SyncLog.Say($"Received {count} portal(s) from the server (peer {sender}), {bytes} bytes, wire v{version}.");
            _lastReceivedBytes = bytes;
            _receiveCount++;

            Apply(received);

            // The point of the whole exercise: portals this client has never been near. If this is
            // zero on a client that has moved around, the registry is not doing its job however
            // healthy the counts look.
            if (Player.m_localPlayer == null)
            {
                // Expected for the join-time sync, which is guaranteed to land before the player
                // loads in. Saying so beats reporting a distance from nowhere.
                SyncLog.Say("Player has not spawned yet, so this is the join-time sync.");
                yield break;
            }

            Vector3 here = Player.m_localPlayer.transform.position;
            int distant = received.Count(p => Vector3.Distance(here, p.Position) > 200f);
            SyncLog.Say($"Of those, {distant} are more than 200m away - portals this client could not " +
                        "see for itself, which is what the registry exists to deliver.");
        }

        private static IEnumerator OnServerReceive(long sender, ZPackage package)
        {
            // Nothing legitimate travels client-to-server on this RPC: the server is the only author
            // of the list, and re-aiming a portal writes the portal's own ZDO rather than asking for
            // the registry to be edited. Jotunn's Initiate() sends an empty package, so an empty one
            // is not worth a word.
            if (package != null && package.Size() > 0)
            {
                Jotunn.Logger.LogWarning($"Discarding an unexpected portal-registry package from peer {sender}.");
            }

            yield break;
        }

        // -- Shared --------------------------------------------------------------------------------

        private static void Apply(List<PortalRecord> records)
        {
            if (StaveboundConfig.LogNetworkSync != null && StaveboundConfig.LogNetworkSync.Value)
            {
                SyncLog.Say($"Registry {Ordered.Count} -> {records.Count} portal(s): " +
                            SyncLog.Difference(Ordered, records));
            }

            Ordered.Clear();
            ByPid.Clear();

            foreach (PortalRecord record in records)
            {
                Ordered.Add(record);

                // The server re-mints on collision, so duplicates should never reach here. Indexing
                // rather than adding keeps a bad server from taking the client down anyway.
                ByPid[record.Pid] = record;
            }
        }
    }
}
