using System;
using System.Collections;
using System.Collections.Generic;
using Stavebound.Config;
using Stavebound.Portals;
using UnityEngine;

namespace Stavebound.Travel
{
    /// <summary>
    /// Clears the body a traveller leaves standing at the portal they walked through, on the machines
    /// watching them.
    /// <para>
    /// <b>Not our bug, and worth saying why we fix it anyway.</b> A player's position rides in their
    /// ZDO, which the server streams only to peers near them. Teleport is the one move that leaves an
    /// area without passing through its edge: the watcher's last copy still places the traveller at the
    /// portal, so the game keeps drawing them there and never culls them — while emotes and actions,
    /// which go out as RPCs to every peer regardless of distance, keep playing on the body. Measured
    /// rather than reasoned: <c>stave_players</c> caught a traveller drawn and held at the portal while
    /// the server had them 2,300 m away, their ZDO frozen. See TESTING.md §1.
    /// </para>
    /// <para>
    /// Portals are how a player leaves an area without crossing it, so this mod is where the symptom
    /// shows up even though nothing here causes it.
    /// </para>
    /// <para>
    /// The cure is the game's own machinery, not a trick. <c>ZDOMan.RequestZDO</c> reaches the server's
    /// <c>RPC_RequestZDO</c>, which force-sends that ZDO to the asker <em>whether or not it is in their
    /// area</em> — the portal registry already leans on this to read distant portals. The refreshed copy
    /// carries the traveller's real position, which puts it outside the sectors
    /// <c>ZNetScene.CreateDestroyObjects</c> collects, and the game removes the body itself. Nothing is
    /// faked, nothing is written, and no ZDO this client does not own is touched.
    /// </para>
    /// <para>
    /// Client-side only. The server holds every ZDO first-hand and so can never hold a stale one, and
    /// asking it to resend to itself would be nonsense. That also means this works against a server on
    /// an older build, or none of this mod at all: the request is vanilla.
    /// </para>
    /// </summary>
    internal static class StaleTravellers
    {
        /// <summary>
        /// How often to look. Slow on purpose: a body left behind is a visual annoyance with nothing
        /// waiting on it, and the whole check is a walk over the player list.
        /// </summary>
        private const float SweepSeconds = 2f;

        /// <summary>
        /// How far the server's answer must be from ours before this machine's copy is suspect. Well
        /// past what a player covers between two of the server's player-list updates, and far short of
        /// any portal trip worth taking.
        /// </summary>
        private const float AwayMetres = 100f;

        /// <summary>
        /// How long a copy must sit unchanged to be suspect when the server's position is unavailable —
        /// which it is for anyone who has turned off "Visible to other players".
        /// </summary>
        private const float StaleSeconds = 5f;

        /// <summary>
        /// The least time between two requests for the same player. A player standing still nearby is
        /// indistinguishable from a stale copy when their position is hidden, so this bounds what that
        /// mistake can cost to one small request every ten seconds — and refreshing a copy that was
        /// already right changes nothing.
        /// </summary>
        private const float RequestCooldown = 10f;

        private sealed class Watch
        {
            internal uint Revision;
            internal float Changed;
            internal float Requested;
        }

        private static readonly Dictionary<ZDOID, Watch> Watched = new Dictionary<ZDOID, Watch>();
        private static readonly List<ZDOID> Forgotten = new List<ZDOID>();
        private static Coroutine _sweep;

        internal static void OnWorldStart()
        {
            OnWorldEnd();

            if (ZNet.instance == null || ZNet.instance.IsServer())
            {
                // The server is where positions come from; its copies are the originals.
                return;
            }

            _sweep = Plugin.Instance.StartCoroutine(Sweep());
        }

        internal static void OnWorldEnd()
        {
            if (_sweep != null)
            {
                if (Plugin.Instance != null)
                {
                    Plugin.Instance.StopCoroutine(_sweep);
                }

                _sweep = null;
            }

            Watched.Clear();
        }

        private static IEnumerator Sweep()
        {
            var wait = new WaitForSeconds(SweepSeconds);

            while (true)
            {
                yield return wait;

                try
                {
                    Look();
                }
                catch (Exception exception)
                {
                    // An exception out of a coroutine kills it silently, and the body would quietly
                    // stop being cleaned up with nothing to say why.
                    Jotunn.Logger.LogError($"Stale-traveller sweep failed, and has stopped: {exception}");
                    _sweep = null;
                    yield break;
                }
            }
        }

        private static void Look()
        {
            if (!StaveboundConfig.ClearLeftBehindBodies.Value ||
                ZNet.instance == null || ZDOMan.instance == null || ZNetScene.instance == null)
            {
                return;
            }

            ZDOID me = LocalPlayer();
            float now = Time.unscaledTime;

            Forgotten.Clear();
            Forgotten.AddRange(Watched.Keys);

            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                ZDOID id = info.m_characterID;
                if (id.IsNone() || id.Equals(me))
                {
                    continue;
                }

                Forgotten.Remove(id);

                ZDO zdo = ZDOMan.instance.GetZDO(id);
                if (zdo == null || ZNetScene.instance.FindInstance(id) == null)
                {
                    // Nothing held, or nothing drawn: there is no body to be left behind.
                    Watched.Remove(id);
                    continue;
                }

                if (!Watched.TryGetValue(id, out Watch watch))
                {
                    watch = new Watch { Revision = zdo.DataRevision, Changed = now };
                    Watched[id] = watch;
                }
                else if (watch.Revision != zdo.DataRevision)
                {
                    watch.Revision = zdo.DataRevision;
                    watch.Changed = now;
                }

                if (now - watch.Requested < RequestCooldown || !Suspect(info, zdo, watch, now, out string why))
                {
                    continue;
                }

                watch.Requested = now;
                ZDOMan.instance.RequestZDO(id);
                SyncLog.Say($"[stale] {info.m_name} {why} - asked the server for a fresh copy. If they have " +
                            "gone, the body they left here goes with it.");
            }

            foreach (ZDOID gone in Forgotten)
            {
                Watched.Remove(gone);
            }
        }

        /// <summary>
        /// Whether this machine's copy of a player looks like it stopped being updated.
        /// <para>
        /// The server's own player list is the better test, because it is sent to everyone regardless of
        /// distance — but it carries a position only while that player shares one. For the rest, a copy
        /// that has not changed in a while is the only signal there is, and it is a guess: a player
        /// standing still looks the same. That guess is safe because being wrong costs one request and
        /// changes nothing.
        /// </para>
        /// </summary>
        private static bool Suspect(ZNet.PlayerInfo info, ZDO zdo, Watch watch, float now, out string why)
        {
            if (info.m_publicPosition)
            {
                Vector3 offset = zdo.GetPosition() - info.m_position;
                offset.y = 0f;

                float apart = offset.magnitude;
                why = $"is drawn {apart:F0}m from where the server says they are";
                return apart > AwayMetres;
            }

            why = $"has not changed in {now - watch.Changed:F0}s and hides their position";
            return now - watch.Changed > StaleSeconds;
        }

        private static ZDOID LocalPlayer()
        {
            ZNetView view = Player.m_localPlayer != null ? Player.m_localPlayer.GetComponent<ZNetView>() : null;
            ZDO zdo = view != null ? view.GetZDO() : null;
            return zdo != null ? zdo.m_uid : ZDOID.None;
        }
    }
}
