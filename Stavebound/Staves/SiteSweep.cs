using System.Collections;
using System.Collections.Generic;
using Stavebound.Config;
using Stavebound.Portals;
using Stavebound.Tiers;
using UnityEngine;

namespace Stavebound.Staves
{
    /// <summary>
    /// Works out what each portal permits, and writes it onto that portal.
    /// <para>
    /// This is the piece the whole mod exists for. Every stave standing near a portal contributes
    /// its tier to that portal's mask, and the mask travels in the registry — so a traveller can be
    /// refused by a place they have never seen (DESIGN.md §6).
    /// </para>
    /// <para>
    /// One relationship, not two. Staves bind straight to the nearest portal in range; there is no
    /// anchor in between. A site is simply a portal and the runes standing around it, which is what a
    /// player would have assumed anyway.
    /// </para>
    /// <para>
    /// <b>Server only, and recomputed from positions every time.</b> Nothing is stored that can rot:
    /// no stave remembers its portal, no portal remembers its runes. Destroy a rune while its site
    /// is unloaded and the next sweep simply finds it gone, which is what §9 means by the binding
    /// being self-healing. It also means the answer cannot drift out of step with what is actually
    /// standing there.
    /// </para>
    /// </summary>
    internal static class SiteSweep
    {
        /// <summary>
        /// Slower than the portal sweep on purpose. Building a stave is a rare, deliberate act, and
        /// this walks every ZDO in the world once per rune type — cheap per frame, but not free.
        /// </summary>
        private const float SweepSeconds = 10f;

        private static Coroutine _sweep;

        /// <summary>Scratch, reused so a permanent timer doesn't allocate permanently.</summary>
        private static readonly List<ZDO> Found = new List<ZDO>();
        private static readonly Dictionary<ZDO, Clearance> PortalMasks = new Dictionary<ZDO, Clearance>();

        internal static void Start()
        {
            Stop();

            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                // Clients read masks off the registry; they never author one.
                return;
            }

            _sweep = Plugin.Instance.StartCoroutine(Run());
        }

        internal static void Stop()
        {
            if (_sweep != null && Plugin.Instance != null)
            {
                Plugin.Instance.StopCoroutine(_sweep);
            }

            _sweep = null;
            Found.Clear();
            PortalMasks.Clear();
        }

        private static IEnumerator Run()
        {
            var wait = new WaitForSeconds(SweepSeconds);

            while (true)
            {
                yield return Recompute();
                yield return wait;
            }
        }

        private static IEnumerator Recompute()
        {
            if (ZDOMan.instance == null)
            {
                yield break;
            }

            PortalMasks.Clear();

            float radius = StaveboundConfig.StaveRadius.Value;
            bool all = StaveboundConfig.Binding.Value == PortalBinding.AllInRadius;

            foreach (KeyValuePair<string, Clearance> stave in StavePieces.Staves)
            {
                yield return Collect(stave.Key, Found);

                foreach (ZDO rune in Found)
                {
                    Bind(rune.GetPosition(), stave.Value, radius, all);
                }
            }

            if (StaveboundConfig.StrictLadder.Value)
            {
                var portals = new List<ZDO>(PortalMasks.Keys);
                foreach (ZDO portal in portals)
                {
                    PortalMasks[portal] = ClearanceExtensions.UpToFirstGap(PortalMasks[portal]);
                }
            }

            WriteChangedMasks();
        }

        /// <summary>
        /// Grants one stave's tier to the portal or portals it stands near (R2).
        /// <para>
        /// Under <c>Nearest</c> a rune between two portals picks the closer one, so a rune can only
        /// ever serve one portal — otherwise a single rune would clear a whole hub, which is a cheaper
        /// network than R4 intends. <c>AllInRadius</c> is the deliberate opt-out for a base spread
        /// across more than one portal.
        /// </para>
        /// </summary>
        private static void Bind(Vector3 runePosition, Clearance tier, float radius, bool all)
        {
            List<ZDO> portals = ZDOMan.instance?.GetPortalList();
            if (portals == null)
            {
                return;
            }

            float limit = radius * radius;
            ZDO nearest = null;
            float nearestDistance = limit;

            foreach (ZDO portal in portals)
            {
                if (portal == null || !portal.IsValid())
                {
                    continue;
                }

                float distance = (portal.GetPosition() - runePosition).sqrMagnitude;
                if (distance > limit)
                {
                    continue;
                }

                if (all)
                {
                    Grant(portal, tier);
                    continue;
                }

                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = portal;
                }
            }

            if (!all && nearest != null)
            {
                Grant(nearest, tier);
            }
        }

        /// <summary>
        /// Every ZDO of a prefab, gathered across frames.
        /// <para>
        /// The game's own iterative walk, used the way the game uses it: a slice per frame rather than
        /// a full scan in one hitch. On a world of thirty thousand ZDOs, doing this six times in a
        /// single frame is a visible stutter every ten seconds.
        /// </para>
        /// </summary>
        private static IEnumerator Collect(string prefab, List<ZDO> into)
        {
            into.Clear();

            int index = 0;
            bool complete = false;

            while (!complete)
            {
                if (ZDOMan.instance == null)
                {
                    into.Clear();
                    yield break;
                }

                complete = ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, into, ref index);
                yield return null;
            }
        }

        private static void Grant(ZDO portal, Clearance tier)
        {
            PortalMasks[portal] = PortalMasks.TryGetValue(portal, out Clearance already)
                ? already | tier
                : tier;
        }

        /// <summary>
        /// Writes the computed masks, and zeroes any portal no rune reaches any more.
        /// <para>
        /// Only where the value actually differs. The sweep runs forever; writing an unchanged mask
        /// would take ownership of every portal ZDO in the world every ten seconds and push it to
        /// every client for nothing.
        /// </para>
        /// </summary>
        private static void WriteChangedMasks()
        {
            List<ZDO> portals = ZDOMan.instance?.GetPortalList();
            if (portals == null)
            {
                return;
            }

            foreach (ZDO portal in portals)
            {
                if (portal == null || !portal.IsValid())
                {
                    continue;
                }

                PortalMasks.TryGetValue(portal, out Clearance wanted);
                Write(portal, wanted);
            }
        }

        private static void Write(ZDO portal, Clearance mask)
        {
            var wanted = (int)mask;
            if (portal.GetInt(ZdoKeys.ClearanceMask, 0) == wanted)
            {
                return;
            }

            PortalTarget.Claim(portal);
            portal.Set(ZdoKeys.ClearanceMask, wanted);
            PortalTarget.Publish(portal);

            string name = portal.GetString(ZDOVars.s_tag, string.Empty);
            Jotunn.Logger.LogInfo(
                $"[site] Clearance of {(string.IsNullOrEmpty(name) ? "an unnamed portal" : $"\"{name}\"")} " +
                $"is now {Describe(mask)}.");
        }

        private static string Describe(Clearance mask)
        {
            if (mask == Clearance.None)
            {
                return "nothing";
            }

            var parts = new List<string>();
            foreach (Clearance tier in ClearanceExtensions.Ladder)
            {
                if ((mask & tier) == tier)
                {
                    parts.Add(tier.ToString());
                }
            }

            return string.Join(" + ", parts);
        }
    }
}
