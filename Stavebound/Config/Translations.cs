using System;
using System.Collections.Generic;
using Jotunn.Managers;

namespace Stavebound.Config
{
    /// <summary>
    /// Every string this mod shows a player, as localisation tokens.
    /// <para>
    /// English only for now, but routed properly: a mod that hardcodes its text cannot be translated
    /// without patching it, and Valheim's audience is not mostly English-speaking. Doing this while
    /// there are thirty-odd strings is a great deal cheaper than doing it later.
    /// </para>
    /// <para>
    /// Parameters go through <see cref="Format"/> rather than Valheim's own word substitution, whose
    /// placeholder convention is not visible from the assemblies. An ordinary .NET format string is
    /// something a translator can read, and keeping the substitution here means a mistranslated
    /// template degrades to English rather than throwing in the middle of a refusal.
    /// </para>
    /// <para>
    /// Console commands and the sync log are deliberately <em>not</em> localised. They are developer
    /// output, and a bug report is more useful in the language the code was written in.
    /// </para>
    /// </summary>
    internal static class Translations
    {
        // -- Travel and refusals -----------------------------------------------------------------

        internal const string Refusal = "stave_refusal";
        internal const string RefusalMore = "stave_refusal_more";
        internal const string ThatPortal = "stave_that_portal";
        internal const string TargetGone = "stave_target_gone";
        internal const string FarSideWaiting = "stave_far_side_waiting";
        internal const string ReaimAdminOnly = "stave_reaim_admin_only";
        internal const string HoverAim = "stave_hover_aim";

        // -- Selector ----------------------------------------------------------------------------

        internal const string SelectorTitle = "stave_sel_title";
        internal const string SelectorByDistance = "stave_sel_by_distance";
        internal const string SelectorByName = "stave_sel_by_name";
        internal const string SelectorFiltered = "stave_sel_filtered";
        internal const string SelectorCarryingNothing = "stave_sel_carrying_nothing";
        internal const string SelectorTakes = "stave_sel_takes";
        internal const string SelectorRefuses = "stave_sel_refuses";
        internal const string SelectorTally = "stave_sel_tally";
        internal const string SelectorMoreAbove = "stave_sel_more_above";
        internal const string SelectorMoreBelow = "stave_sel_more_below";
        internal const string SelectorEmpty = "stave_sel_empty";
        internal const string SelectorShowAll = "stave_sel_show_all";
        internal const string SelectorNowhere = "stave_sel_nowhere";
        internal const string SelectorAimed = "stave_sel_aimed";
        internal const string Confirm = "stave_sel_confirm";
        internal const string Cancel = "stave_sel_cancel";
        internal const string Change = "stave_sel_change";
        internal const string Sort = "stave_sel_sort";
        internal const string Filter = "stave_sel_filter";
        internal const string UnnamedPortal = "stave_unnamed_portal";
        internal const string PortalAt = "stave_portal_at";

        // -- Placing a stave ------------------------------------------------------------------

        internal const string PlaceNoPortal = "stave_place_no_portal";
        internal const string PlaceBinds = "stave_place_binds";
        internal const string PlaceBindsAll = "stave_place_binds_all";
        internal const string PlaceBindsNearest = "stave_place_binds_nearest";
        internal const string AnUnnamedPortal = "stave_an_unnamed_portal";

        // -- The pieces --------------------------------------------------------------------------

        internal const string RuneElder = "stave_rune_elder";
        internal const string RuneBonemass = "stave_rune_bonemass";
        internal const string RuneModer = "stave_rune_moder";
        internal const string RuneYagluth = "stave_rune_yagluth";
        internal const string RuneQueen = "stave_rune_queen";
        internal const string RuneAshen = "stave_rune_ashen";
        internal const string NoStave = "stave_rune_none";

        internal const string DescElder = "stave_desc_elder";
        internal const string DescBonemass = "stave_desc_bonemass";
        internal const string DescModer = "stave_desc_moder";
        internal const string DescYagluth = "stave_desc_yagluth";
        internal const string DescQueen = "stave_desc_queen";
        internal const string DescAshen = "stave_desc_ashen";

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { Refusal, "{0} cannot enter {1} — no {2} there." },
            { RefusalMore, " (and {0} more.)" },
            { ThatPortal, "that portal" },
            { TargetGone, "This portal points at somewhere that no longer exists." },
            { FarSideWaiting, "The far side has not answered yet." },
            { ReaimAdminOnly, "Only an admin may re-aim portals on this server." },
            { HoverAim, "Aim portal" },

            { SelectorTitle, "Aim {0} at" },
            { SelectorByDistance, "by distance" },
            { SelectorByName, "by name" },
            { SelectorFiltered, ", only what takes my load" },
            { SelectorCarryingNothing, "Carrying nothing a portal would refuse." },
            { SelectorTakes, "This one takes your load." },
            { SelectorRefuses, "This one would refuse you." },
            { SelectorTally, "{0} of {1} take your load." },
            { SelectorMoreAbove, "{0} more above" },
            { SelectorMoreBelow, "{0} more below" },
            { SelectorEmpty, "Nothing here will take what you are carrying." },
            { SelectorShowAll, "show everything" },
            { SelectorNowhere, "There is nowhere else to point this portal." },
            { SelectorAimed, "{0} now points at {1}." },
            { Confirm, "confirm" },
            { Cancel, "cancel" },
            { Change, "change" },
            { Sort, "sort" },
            { Filter, "filter" },
            { UnnamedPortal, "unnamed portal" },
            { PortalAt, "portal at {0}, {1}" },

            { PlaceNoPortal, "No portal in range — this stave would do nothing here." },
            { PlaceBinds, "Binds to {0}." },
            { PlaceBindsAll, "Binds to all {0} portals in range." },
            { PlaceBindsNearest, "Binds to {0}, the nearest of {1} in range." },
            { AnUnnamedPortal, "an unnamed portal" },

            { RuneElder, "Elder's Stave" },
            { RuneBonemass, "Bonemass's Stave" },
            { RuneModer, "Moder's Stave" },
            { RuneYagluth, "Yagluth's Stave" },
            { RuneQueen, "Queen's Stave" },
            { RuneAshen, "Ashen Stave" },
            { NoStave, "no stave" },

            { DescElder, "Lets copper, tin and bronze arrive at this site." },
            { DescBonemass, "Lets iron arrive at this site." },
            { DescModer, "Lets silver and dragon eggs arrive at this site." },
            { DescYagluth, "Lets black metal arrive at this site." },
            { DescQueen, "Lets the Mistlands' guarded things arrive at this site." },
            { DescAshen, "Lets flametal and the Ashlands' spoils arrive at this site." },
        };

        internal static void Add()
        {
            // GetLocalization hands back this mod's own CustomLocalization, already registered.
            Jotunn.Entities.CustomLocalization localization = LocalizationManager.Instance.GetLocalization();

            foreach (KeyValuePair<string, string> entry in English)
            {
                localization.AddTranslation("English", entry.Key, entry.Value);
            }
        }

        /// <summary>The translated text for a token, in the player's language.</summary>
        internal static string Get(string token)
        {
            return Localization.instance != null
                ? Localization.instance.Localize($"${token}")
                : English.TryGetValue(token, out string fallback) ? fallback : token;
        }

        /// <summary>
        /// A translated template with its arguments filled in.
        /// <para>
        /// A translation carrying a malformed placeholder falls back to English rather than throwing.
        /// A refusal message is exactly the wrong place to discover that somebody typed <c>{0</c>.
        /// </para>
        /// </summary>
        internal static string Format(string token, params object[] args)
        {
            string template = Get(token);

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                Jotunn.Logger.LogWarning(
                    $"Translation '{token}' has a malformed placeholder and was skipped: {template}");

                return English.TryGetValue(token, out string english)
                    ? string.Format(english, args)
                    : template;
            }
        }
    }
}
