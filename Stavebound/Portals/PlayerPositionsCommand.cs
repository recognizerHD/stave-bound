using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Stavebound.Portals
{
    /// <summary>
    /// <c>stave_players</c> — where this machine believes every player is, three ways over, for the body
    /// left standing at a portal after its player has walked through (TESTING.md).
    /// <para>
    /// The game keeps three separate answers to "where is that player", and a leftover body is a
    /// disagreement between them:
    /// </para>
    /// <list type="bullet">
    /// <item><b>drawn</b> — where the character is rendered on this screen.</item>
    /// <item><b>data</b> — this machine's copy of that player's ZDO, which the server streams only to
    /// peers near the player.</item>
    /// <item><b>server</b> — the player list the server sends everyone for map markers, independent of
    /// ZDO streaming. Present only while that player has "Visible to other players" ticked.</item>
    /// </list>
    /// <para>
    /// One look cannot tell a stale copy from a player standing still, so the command looks twice, a few
    /// seconds apart, and reports whether each copy moved on in between. The watcher runs it while the
    /// body is in view; the traveller runs it too, which is what separates the game's streaming from a
    /// traveller whose own game never reported the move.
    /// </para>
    /// <para>
    /// Read-only, not a cheat, and not hidden behind devcommands — it needs only the console enabled.
    /// </para>
    /// </summary>
    internal sealed class PlayerPositionsCommand : StaveboundCommand
    {
        /// <summary>How long between the two looks.</summary>
        private const float WatchSeconds = 3f;

        /// <summary>
        /// How far apart two answers must be to count as disagreeing. Generous, because the server's list
        /// is refreshed every few seconds and a running player moves; a teleport moves hundreds of metres.
        /// </summary>
        private const float Apart = 30f;

        public override string Name => "stave_players";

        public override string Help =>
            "Diagnose a player's body left at a portal after they went through: where each player is drawn, " +
            "where this machine's data says, and where the server says. Watches for a few seconds. Run it " +
            "on the watcher's machine while the body is visible, and on the traveller's.";

        protected override void Execute(string[] args, Terminal context)
        {
            if (ZNet.instance == null || ZDOMan.instance == null || ZNetScene.instance == null)
            {
                Echo(context, "Stavebound: no world loaded.");
                return;
            }

            List<Sample> first = Take();
            if (first.Count == 0)
            {
                Echo(context, "Stavebound: the server has not sent a player list yet.");
                return;
            }

            Echo(context, $"Stavebound players - watching for {WatchSeconds:F0}s, stay put ...");
            Plugin.Instance.StartCoroutine(Finish(context, first));
        }

        private sealed class Sample
        {
            internal string Name;
            internal ZDOID Id;
            internal bool You;
            internal bool Public;
            internal Vector3 Server;
            internal bool HaveData;
            internal Vector3 Data;
            internal uint Revision;
            internal long Owner;
            internal bool HaveBody;
            internal Vector3 Body;
        }

        private static List<Sample> Take()
        {
            var samples = new List<Sample>();

            ZNetView localView = Player.m_localPlayer != null ? Player.m_localPlayer.GetComponent<ZNetView>() : null;
            ZDO localZdo = localView != null ? localView.GetZDO() : null;
            ZDOID local = localZdo != null ? localZdo.m_uid : ZDOID.None;

            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                var sample = new Sample
                {
                    Name = info.m_name,
                    Id = info.m_characterID,
                    You = !info.m_characterID.IsNone() && info.m_characterID.Equals(local),
                    Public = info.m_publicPosition,
                    Server = info.m_position,
                };

                if (!sample.Id.IsNone())
                {
                    ZDO zdo = ZDOMan.instance.GetZDO(sample.Id);
                    if (zdo != null)
                    {
                        sample.HaveData = true;
                        sample.Data = zdo.GetPosition();
                        sample.Revision = zdo.DataRevision;
                        sample.Owner = zdo.GetOwner();
                    }

                    GameObject body = ZNetScene.instance.FindInstance(sample.Id);
                    if (body != null)
                    {
                        sample.HaveBody = true;
                        sample.Body = body.transform.position;
                    }
                }

                samples.Add(sample);
            }

            return samples;
        }

        private static IEnumerator Finish(Terminal context, List<Sample> first)
        {
            yield return new WaitForSecondsRealtime(WatchSeconds);

            if (ZNet.instance == null || ZDOMan.instance == null || ZNetScene.instance == null)
            {
                yield break;
            }

            List<Sample> second = Take();
            Echo(context, $"Stavebound players - {second.Count} in the server's list:");

            foreach (Sample now in second)
            {
                Sample before = first.Find(sample => sample.Id.Equals(now.Id));
                bool updating = before != null && before.HaveData && now.HaveData && before.Revision != now.Revision;

                Echo(context, $"  {now.Name}{(now.You ? " (you)" : string.Empty)}");
                Echo(context, "    drawn:  " + (now.HaveBody ? Where(now.Body) : "not drawn on this screen"));
                Echo(context, "    data:   " + (now.HaveData
                    ? $"{Where(now.Data)}  revision {before?.Revision.ToString() ?? "?"} -> {now.Revision}" +
                      $" ({(updating ? "changed" : "unchanged")} over {WatchSeconds:F0}s)  owner {now.Owner}"
                    : "no copy held on this machine"));
                Echo(context, "    server: " + (now.Public ? Where(now.Server) : "hidden - 'Visible to other players' is off"));
                Echo(context, "    verdict: " + Verdict(now, updating));
            }
        }

        /// <summary>
        /// Reads the three answers against each other in the terms TESTING.md sets out. Worded as what the
        /// numbers show rather than as a diagnosis, because "unchanged" can also mean standing still.
        /// </summary>
        private static string Verdict(Sample now, bool updating)
        {
            if (!now.HaveBody && !now.HaveData)
            {
                return "not held or drawn here - out of range, which is normal";
            }

            if (!now.Public)
            {
                return "cannot compare without the server's position - have them tick 'Visible to other " +
                       "players' on the map, then run this again";
            }

            float dataToServer = now.HaveData ? Flat(now.Data, now.Server) : 0f;

            if (now.You)
            {
                return now.HaveData && dataToServer > Apart
                    ? $"YOUR OWN data is {dataToServer:F0}m from where the server says you are - your game has " +
                      "not reported where you are. Straight after a teleport, that points at the teleport path"
                    : "consistent - your data and the server agree";
            }

            float bodyToServer = now.HaveBody ? Flat(now.Body, now.Server) : 0f;
            float bodyToData = now.HaveBody && now.HaveData ? Flat(now.Body, now.Data) : 0f;

            if (now.HaveBody && bodyToServer > Apart)
            {
                if (now.HaveData && dataToServer > Apart)
                {
                    return $"STALE COPY - drawn and held {bodyToServer:F0}m from where the server says" +
                           (updating ? ", though the data is still changing" : ", and the data has stopped changing") +
                           ". This machine is no longer being sent this player's position: the game's area " +
                           "streaming, which Stavebound does not touch";
                }

                if (now.HaveData && bodyToData > Apart)
                {
                    return $"BODY NOT MOVED - the data agrees with the server, but the body is drawn " +
                           $"{bodyToData:F0}m away from it";
                }

                return $"drawn {bodyToServer:F0}m from where the server says";
            }

            return "consistent - drawn where the server says";
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static string Where(Vector3 position) => $"({position.x:F0}, {position.z:F0})";
    }
}
