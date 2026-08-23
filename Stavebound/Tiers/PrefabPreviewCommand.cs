using System;
using System.Collections.Generic;
using System.Linq;
using Stavebound.Portals;
using UnityEngine;

namespace Stavebound.Tiers
{
    /// <summary>
    /// <c>stave_preview</c> — put a prefab in front of you to look at, without it becoming part of
    /// the world.
    /// <para>
    /// Vanilla's <c>spawn</c> covers anything with a <c>ZNetView</c>, and this deliberately refuses
    /// those: a real spawn is networked, saved and owned, and there is no reason to duplicate it.
    /// What it exists for is the rest — dungeon dressing, the stands and props that decorate a crypt,
    /// which carry no <c>ZNetView</c> at all and so cannot be spawned by any normal means.
    /// </para>
    /// <para>
    /// Those are exactly the prefabs worth evaluating as clone sources (DESIGN.md §11), and being
    /// unable to look at one before building a piece out of it is how you end up writing code to find
    /// out what something looks like.
    /// </para>
    /// </summary>
    internal sealed class PrefabPreviewCommand : StaveboundCommand
    {
        /// <summary>Purely visual copies, tracked so they can all be cleared again.</summary>
        private static readonly List<GameObject> Previews = new List<GameObject>();

        public override string Name => "stave_preview";

        public override string Help =>
            "stave_preview <prefab> - place a look-at-only copy in front of you. It is not saved, " +
            "not networked and vanishes on reload. 'stave_preview clear' removes them. " +
            "For anything spawnable, use vanilla spawn instead.";

        protected override void Execute(string[] args, Terminal context)
        {
            string wanted = string.Join(" ", args).Trim();

            if (string.Equals(wanted, "clear", StringComparison.OrdinalIgnoreCase))
            {
                Clear(context);
                return;
            }

            if (wanted.Length == 0)
            {
                Echo(context, "Stavebound: name a prefab, or 'clear' to remove the previews.");
                return;
            }

            if (ZNetScene.instance == null || Player.m_localPlayer == null)
            {
                Echo(context, "Stavebound: no world loaded.");
                return;
            }

            GameObject prefab = ZNetScene.instance.GetPrefab(wanted);
            if (prefab == null)
            {
                Echo(context, $"Stavebound: no prefab called \"{wanted}\". Try stave_prefabs {wanted}");
                return;
            }

            if (prefab.GetComponent<ZNetView>() != null)
            {
                // Refused on purpose. Instantiating one of these would register a ZDO and quietly add
                // an object to the save that nobody meant to build.
                Echo(context, $"Stavebound: {prefab.name} is a networked object - use 'spawn {prefab.name}' " +
                              "instead, which places it properly and can be removed with the hammer.");
                return;
            }

            Transform player = Player.m_localPlayer.transform;
            Vector3 where = player.position + player.forward * 3f;

            GameObject preview = UnityEngine.Object.Instantiate(prefab, where, player.rotation);
            preview.name = $"stave_preview_{prefab.name}";
            Previews.Add(preview);

            Echo(context, $"Stavebound: previewing {prefab.name} in front of you " +
                          $"({Previews.Count} up). This is scenery only - nothing was added to the world.");
        }

        private static void Clear(Terminal context)
        {
            int count = Previews.Count(p => p != null);

            foreach (GameObject preview in Previews.Where(p => p != null))
            {
                UnityEngine.Object.Destroy(preview);
            }

            Previews.Clear();
            Echo(context, count == 0 ? "Stavebound: nothing was being previewed." : $"Stavebound: removed {count} preview(s).");
        }
    }
}
