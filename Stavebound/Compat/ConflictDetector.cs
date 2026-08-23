using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using Stavebound.Config;

namespace Stavebound.Compat
{
    /// <summary>
    /// Warns when another installed mod also rewrites portal or teleport rules.
    /// <para>
    /// Two mods patching the same teleport path produce behaviour neither author designed, and the
    /// resulting bug reports are unreadable. The design's position is to say so loudly at startup
    /// rather than fight over patches — so this only ever logs.
    /// </para>
    /// </summary>
    internal static class ConflictDetector
    {
        /// <summary>
        /// Mods known to rewrite the rules Stavebound owns, keyed by plugin GUID.
        /// <para>
        /// GUIDs here are only useful if they are exactly right — a wrong one fails silently — so
        /// this list holds only the ones that have been confirmed against the mod's own source.
        /// Everything else is left to <see cref="SuspectKeywords"/>. DESIGN.md §12 tracks the GUIDs
        /// still to confirm (Advanced Portals, Progression Portals, Gate of Ore-thority, AnyPortal).
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, string> KnownConflicts =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["org.bepinex.plugins.valheim_plus"] = "rewrites teleport rules wholesale, including what may be carried through a portal",
                ["SpikeHimself.XPortal"] = "owns the portal destination list, which Stavebound also provides",
            };

        /// <summary>
        /// Catches the portal mods whose GUIDs are not confirmed, and the ones nobody has heard of.
        /// Deliberately broad: a false positive costs one log line the player can silence, a false
        /// negative costs a silent behaviour clash.
        /// </summary>
        private static readonly string[] SuspectKeywords = { "portal", "teleport" };

        /// <summary>
        /// Call this from <c>Start</c>, not <c>Awake</c>. BepInEx adds each plugin to
        /// <see cref="Chainloader.PluginInfos"/> as it loads it, so during our own Awake the mods
        /// loaded after us are not in there yet. By the first frame they all are.
        /// </summary>
        internal static void WarnAboutKnownConflicts()
        {
            if (!StaveboundConfig.WarnOnConflictingMods.Value)
            {
                return;
            }

            var ignored = ParseIgnoreList(StaveboundConfig.IgnoredConflictGuids.Value);

            var confirmed = new List<string>();
            var suspected = new List<string>();

            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                var metadata = plugin?.Metadata;
                if (metadata == null || metadata.GUID == BuildInfo.Guid || ignored.Contains(metadata.GUID))
                {
                    continue;
                }

                if (KnownConflicts.TryGetValue(metadata.GUID, out var reason))
                {
                    confirmed.Add($"{metadata.Name} ({metadata.GUID}) — {reason}");
                }
                else if (LooksLikeAPortalMod(metadata.GUID) || LooksLikeAPortalMod(metadata.Name))
                {
                    suspected.Add($"{metadata.Name} ({metadata.GUID})");
                }
            }

            if (confirmed.Count > 0)
            {
                Jotunn.Logger.LogWarning(
                    $"{BuildInfo.Name} found {confirmed.Count} installed mod(s) that conflict with it:");
                foreach (var line in confirmed)
                {
                    Jotunn.Logger.LogWarning($"  - {line}");
                }

                Jotunn.Logger.LogWarning(
                    "Expect portal behaviour neither mod intends. Remove one of them before reporting bugs.");
            }

            if (suspected.Count > 0)
            {
                Jotunn.Logger.LogWarning(
                    $"{BuildInfo.Name} also sees {suspected.Count} mod(s) that look portal-related and may " +
                    "clash: " + string.Join(", ", suspected.ToArray()));
                Jotunn.Logger.LogWarning(
                    "If one of those is harmless, add its GUID to IgnoredConflictGuids to silence this.");
            }
        }

        private static bool LooksLikeAPortalMod(string value)
        {
            return !string.IsNullOrEmpty(value)
                   && SuspectKeywords.Any(keyword => value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static HashSet<string> ParseIgnoreList(string raw)
        {
            var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(raw))
            {
                return ignored;
            }

            foreach (var entry in raw.Split(','))
            {
                var trimmed = entry.Trim();
                if (trimmed.Length > 0)
                {
                    ignored.Add(trimmed);
                }
            }

            return ignored;
        }
    }
}
