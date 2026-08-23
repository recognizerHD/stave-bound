using System;
using System.Collections.Generic;
using System.Linq;
using Stavebound.Config;
using UnityEngine;

namespace Stavebound.Tiers
{
    /// <summary>
    /// Which stave each blocked item needs, built by scanning <c>ObjectDB</c> at runtime.
    /// <para>
    /// The list is never hand-maintained (DESIGN.md §4). The game is asked which items it refuses to
    /// teleport, and config maps the ones we recognise to a tier; anything unrecognised is assigned
    /// the highest tier and logged by name so a player can classify it in one line. That way a game
    /// update, an Ashlands-style content drop, or another mod adding a blocked resource cannot break
    /// the mod — it just gets conservative until someone looks.
    /// </para>
    /// <para>
    /// Reading the game's answer also means we never have to keep a wiki page and a source file in
    /// agreement, which is a losing errand across game updates.
    /// </para>
    /// </summary>
    internal static class TierMap
    {
        private static readonly Dictionary<string, Clearance> Required =
            new Dictionary<string, Clearance>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Blocked items the config did not recognise, kept so <c>stave_items</c> can list them and
        /// somebody can decide where they belong.
        /// </summary>
        private static readonly List<string> Unclassified = new List<string>();

        /// <summary>
        /// Prefab name to the item's display-name token. Kept because a prefab name is often not
        /// enough to classify an item — "Ironpit" and "MechanicalSpring" say nothing about which
        /// biome's haul they belong to, and guessing is how the tier map ends up wrong.
        /// </summary>
        private static readonly Dictionary<string, string> DisplayTokens =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static bool Built { get; private set; }

        internal static int BlockedCount => Required.Count;

        internal static IReadOnlyList<string> UnclassifiedItems => Unclassified;

        /// <summary>
        /// Which stave this prefab needs at the destination, or <see cref="Clearance.None"/> if the
        /// game is happy to teleport it.
        /// </summary>
        internal static Clearance RequiredFor(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return Clearance.None;
            }

            return Required.TryGetValue(prefabName, out Clearance tier) ? tier : Clearance.None;
        }

        /// <summary>Every blocked prefab at a given tier, for reporting.</summary>
        internal static IEnumerable<string> AtTier(Clearance tier)
        {
            return Required.Where(pair => pair.Value == tier).Select(pair => pair.Key).OrderBy(name => name);
        }

        /// <summary>
        /// "PrefabName (In-game Name)", for anything meant to be read by a person deciding where an
        /// item belongs. Localised at call time rather than when scanned, because the item database is
        /// built before the language is settled.
        /// </summary>
        internal static string Describe(string prefabName)
        {
            if (!DisplayTokens.TryGetValue(prefabName, out string token) || string.IsNullOrEmpty(token))
            {
                return prefabName;
            }

            string display = Localization.instance != null ? Localization.instance.Localize(token) : token;
            return string.Equals(display, prefabName, StringComparison.OrdinalIgnoreCase)
                ? prefabName
                : $"{prefabName} ({display})";
        }

        /// <summary>
        /// Rebuilds from the live <c>ObjectDB</c>. Safe to call repeatedly — the database is rebuilt
        /// when a world loads, and mods add items to it after we first look.
        /// </summary>
        internal static void Build()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0)
            {
                // Called too early. The main menu has a partial database; the real one arrives with
                // the world.
                return;
            }

            Required.Clear();
            Unclassified.Clear();
            DisplayTokens.Clear();

            Dictionary<string, Clearance> configured = ReadConfiguredTiers();

            foreach (GameObject prefab in db.m_items)
            {
                if (prefab == null)
                {
                    continue;
                }

                ItemDrop drop = prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData?.m_shared == null)
                {
                    continue;
                }

                // The authoritative question, asked of the game rather than assumed.
                if (drop.m_itemData.m_shared.m_teleportable)
                {
                    continue;
                }

                string name = prefab.name;
                DisplayTokens[name] = drop.m_itemData.m_shared.m_name;

                if (configured.TryGetValue(name, out Clearance tier))
                {
                    Required[name] = tier;
                    continue;
                }

                Required[name] = ClearanceExtensions.Highest;
                Unclassified.Add(name);
            }

            Built = true;

            Jotunn.Logger.LogInfo(
                $"Tier map built: {Required.Count} blocked item(s) across {db.m_items.Count} in ObjectDB.");

            if (Unclassified.Count > 0)
            {
                // Named, not counted. Someone has to be able to act on this without a debugger.
                Jotunn.Logger.LogWarning(
                    $"{Unclassified.Count} blocked item(s) are not in the tier config and default to " +
                    $"{ClearanceExtensions.Highest} ({ClearanceExtensions.Highest.StaveName()}): " +
                    $"{string.Join(", ", Unclassified.OrderBy(n => n))}. " +
                    "Add them to the 2 - Clearance section to place them properly.");
            }
        }

        /// <summary>
        /// Flattens the five config lists into one prefab-to-tier lookup, complaining about anything
        /// listed twice rather than silently letting the last one win.
        /// </summary>
        private static Dictionary<string, Clearance> ReadConfiguredTiers()
        {
            var configured = new Dictionary<string, Clearance>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<Clearance, string> entry in StaveboundConfig.TierPrefabs())
            {
                foreach (string name in entry.Value.Split(','))
                {
                    string trimmed = name.Trim();
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }

                    if (configured.TryGetValue(trimmed, out Clearance already) && already != entry.Key)
                    {
                        Jotunn.Logger.LogWarning(
                            $"'{trimmed}' is listed under both {already} and {entry.Key}. Using {already}; " +
                            "remove one of them.");
                        continue;
                    }

                    configured[trimmed] = entry.Key;
                }
            }

            return configured;
        }
    }
}
