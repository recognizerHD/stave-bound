using Stavebound.Tiers;
using HarmonyLib;

namespace Stavebound.Patches
{
    /// <summary>
    /// Builds the tier map whenever the item database changes.
    /// <para>
    /// Both hooks are needed and neither is redundant. <c>Awake</c> covers the database the main menu
    /// builds; <c>CopyOtherDB</c> is how the game merges in the real one when a world loads, and it is
    /// also where other mods' items have arrived by. Scanning once at startup would miss both.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(ObjectDB))]
    internal static class ObjectDbPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        private static void BuildOnAwake()
        {
            TierMap.Build();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ObjectDB.CopyOtherDB))]
        private static void BuildOnCopy()
        {
            TierMap.Build();
        }
    }
}
