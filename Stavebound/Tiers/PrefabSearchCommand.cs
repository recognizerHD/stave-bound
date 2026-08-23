using System;
using System.Collections.Generic;
using System.Linq;
using Stavebound.Portals;
using UnityEngine;

namespace Stavebound.Tiers
{
    /// <summary>
    /// <c>stave_prefabs</c> — search the loaded prefabs by name.
    /// <para>
    /// Two of Phase 2's inputs cannot be read from the assemblies, because they live in asset
    /// bundles rather than code: the boss trophy prefab names the stave recipes cost, and the
    /// standing-stone prefabs the pieces are cloned from (DESIGN.md §11 — clone, never ship art).
    /// §12 has had both sitting in "unverified" since the spec was written.
    /// </para>
    /// <para>
    /// Reading them out of a running game is the only honest way to settle it, and one command
    /// beats three rounds of guessing at names.
    /// </para>
    /// </summary>
    internal sealed class PrefabSearchCommand : StaveboundCommand
    {
        /// <summary>Enough to choose from, few enough to read in a console.</summary>
        private const int Limit = 60;

        public override string Name => "stave_prefabs";

        public override string Help =>
            "stave_prefabs <text>[,<text>...] - list loaded prefabs whose name contains each term, " +
            "marking which are buildable pieces and which are items. Comma-separate to search for " +
            "several things at once.";

        protected override void Execute(string[] args, Terminal context)
        {
            if (ZNetScene.instance == null)
            {
                Echo(context, "Stavebound: no world loaded - prefabs only exist once you are in a game.");
                return;
            }

            // Comma-separated, because choosing what to clone means comparing candidates and running
            // one search per idea turns a five-minute question into five round trips.
            string[] terms = string.Join(" ", args)
                .Split(',')
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToArray();

            if (terms.Length == 0)
            {
                Echo(context, "Stavebound: give me something to search for, e.g. stave_prefabs trophy,sconce,brazier");
                return;
            }

            foreach (string term in terms)
            {
                Search(context, term);
            }
        }

        private static void Search(Terminal context, string term)
        {
            List<GameObject> hits = ZNetScene.instance.m_prefabs
                .Where(p => p != null && p.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(p => p.name)
                .ToList();

            if (hits.Count == 0)
            {
                Echo(context, $"\"{term}\": nothing.");
                return;
            }

            Echo(context, $"\"{term}\": {hits.Count}" +
                          (hits.Count > Limit ? $" (showing {Limit})" : string.Empty));

            foreach (GameObject prefab in hits.Take(Limit))
            {
                // What a prefab *is* decides whether we can clone it into a build piece or cost it in
                // a recipe, so the kind matters as much as the name.
                var kinds = new List<string>();
                if (prefab.GetComponent<Piece>() != null)
                {
                    kinds.Add("piece");
                }

                ItemDrop drop = prefab.GetComponent<ItemDrop>();
                if (drop?.m_itemData?.m_shared != null)
                {
                    kinds.Add($"item \"{Localization.instance.Localize(drop.m_itemData.m_shared.m_name)}\"");
                }

                Echo(context, $"  {prefab.name}{(kinds.Count > 0 ? "  [" + string.Join(", ", kinds) + "]" : string.Empty)}");
            }
        }
    }
}
