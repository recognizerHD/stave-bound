using System.Collections.Generic;
using System.Linq;
using Stavebound.Portals;

namespace Stavebound.Tiers
{
    /// <summary>
    /// <c>stave_items</c> — what the game says it refuses to teleport, and which stave we have
    /// assigned each one to.
    /// <para>
    /// This is how DESIGN.md §12's "get the blocked list from ObjectDB, not from a wiki" actually gets
    /// done. Run it once and the unclassified list is the exact set of prefab names that need placing
    /// in config — no decompiler, no guessing at what the Ashlands added.
    /// </para>
    /// </summary>
    internal sealed class BlockedItemsCommand : StaveboundCommand
    {
        public override string Name => "stave_items";

        public override string Help =>
            "List every item the game refuses to teleport, grouped by the stave that will permit it. " +
            "Anything unrecognised is listed separately and defaults to the highest tier.";

        protected override void Execute(string[] args, Terminal context)
        {
            if (!TierMap.Built)
            {
                Echo(context, "Stavebound: the tier map has not been built yet. Load a world first — " +
                              "the main menu's item database is only a partial one.");
                return;
            }

            Echo(context, $"Stavebound tiers - {TierMap.BlockedCount} blocked item(s):");

            foreach (Clearance tier in new[]
                     {
                         Clearance.Elder, Clearance.Bonemass, Clearance.Moder,
                         Clearance.Yagluth, Clearance.Ashen,
                     })
            {
                List<string> items = TierMap.AtTier(tier).Select(TierMap.Describe).ToList();
                Echo(context, $"  {tier.StaveName()} ({items.Count}): " +
                              (items.Count == 0 ? "nothing" : string.Join(", ", items)));
            }

            IReadOnlyList<string> unknown = TierMap.UnclassifiedItems;
            if (unknown.Count == 0)
            {
                Echo(context, "  Every blocked item is classified.");
                return;
            }

            // The actionable part. These are already enforced at the highest tier, so this is a
            // request to place them deliberately rather than a fault to fix.
            Echo(context, $"  UNCLASSIFIED ({unknown.Count}), defaulting to " +
                          $"{ClearanceExtensions.Highest.StaveName()}:");
            foreach (string name in unknown.OrderBy(n => n))
            {
                Echo(context, $"    {TierMap.Describe(name)}");
            }
            Echo(context, "  Add these to the 2 - Clearance config lists to place them properly.");
        }
    }
}
