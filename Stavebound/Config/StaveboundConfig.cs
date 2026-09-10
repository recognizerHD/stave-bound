using System.Collections.Generic;
using BepInEx.Configuration;
using Stavebound.Tiers;

namespace Stavebound.Config
{
    /// <summary>
    /// Which portals a stave grants its clearance to. See DESIGN.md R2.
    /// <para>
    /// Both options are resolved from position on the server's sweep, so neither stores a reference
    /// that can go stale when a portal is rebuilt.
    /// </para>
    /// </summary>
    internal enum PortalBinding
    {
        /// <summary>
        /// The single closest portal in range. Re-aiming reaches every destination from one portal,
        /// so a site only ever needs one and there is nothing to disambiguate — and two portals at one
        /// location can carry different clearance.
        /// </summary>
        Nearest,

        /// <summary>
        /// Every portal in range, for a base spread across more than one portal.
        /// </summary>
        AllInRadius,
    }

    /// <summary>Who is allowed to change where a portal points. See DESIGN.md §5.</summary>
    internal enum ReaimPermission
    {
        /// <summary>Any player may re-aim any portal.</summary>
        Anyone,

        /// <summary>
        /// Inside a guard stone's protected area, only the players it permits; outside any guard
        /// stone, anyone. Reuses the guard stone's existing permitted-players list rather than
        /// inventing a second access-control system. The guard stone is the vanilla piece the game
        /// labels "Ward" — it is unrelated to staves, which carry clearance and have no player list.
        /// </summary>
        GuardStonePermitted,

        /// <summary>Admins only.</summary>
        Admin,
    }

    /// <summary>
    /// Which end of a trip a site's clearance counts for. See DESIGN.md R3.
    /// <para>
    /// One rule for the whole world, synced from the server. Every option resolves to the same
    /// question — <em>does either end of this trip vouch for the tier being carried?</em> — and
    /// differs only in which ends are allowed to answer:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="Receive"/> — the destination's staves, and only those.</item>
    /// <item><see cref="Deliver"/> — the departure portal's staves, and only those.</item>
    /// <item><see cref="Both"/> — either end's, so a tier travels if one end has it.</item>
    /// </list>
    /// <para>
    /// Because each option is a union of the masks it counts, and the server already trims each mask
    /// to a prefix of the ladder when <c>StrictLadder</c> is on, combining two masks cannot
    /// manufacture a gap: the union of two prefixes is the longer prefix. The two settings therefore
    /// do not interact, which is why neither has to know about the other.
    /// </para>
    /// </summary>
    internal enum MaterialFlow
    {
        /// <summary>
        /// Only the destination is asked. A site accepts what its own staves permit, from anywhere,
        /// and can send those metals on only to somewhere that permits them too.
        /// <para>
        /// This is the asymmetry the mod was designed around: an outpost with no staves can send ore
        /// to your base forever and never receive any, because arriving is checked and departing is
        /// not. Ore flows inward, toward the places you have invested in.
        /// </para>
        /// </summary>
        Receive,

        /// <summary>
        /// Only the portal you leave from is asked — <see cref="Receive"/> reflected. A site sends
        /// what its staves permit anywhere, and takes those metals back only from somewhere that
        /// permits them too.
        /// <para>
        /// Ore flows <em>outward</em>: a fully equipped base can supply bare outposts with anything,
        /// while an outpost cannot ship its own ore home until it has staves of its own. The reverse
        /// of the shipped rule, for a group that would rather kit out a frontier than feed a capital.
        /// </para>
        /// </summary>
        Deliver,

        /// <summary>
        /// Either end is enough, and the default. A site's staves cover both arriving and departing,
        /// so metals move freely between it and anywhere else; only two sites that <em>both</em> lack
        /// the tier cannot pass it between them.
        /// <para>
        /// Investment still gates everything — nothing moves that neither end has paid for — but the
        /// one-way outpost of <see cref="Receive"/> becomes something you opt into rather than the
        /// law. The gentlest of the three, and the one that punishes a forgotten stave least.
        /// </para>
        /// </summary>
        Both,
    }

    /// <summary>
    /// Every config entry the mod owns, bound once from <see cref="Plugin"/>.
    /// <para>
    /// Entries marked synced are admin-only: Jotunn pushes the server's value to every client and
    /// locks the local one, so tiers and clearance rules cannot be edited client-side. Entries that
    /// only change what a player sees stay local, because forcing a display preference across a
    /// server is rude and pointless.
    /// </para>
    /// </summary>
    internal static class StaveboundConfig
    {
        private const string SectionTravel = "1 - Travel";
        private const string SectionClearance = "2 - Clearance";
        private const string SectionCargoPreview = "3 - Cargo preview";
        private const string SectionCompatibility = "4 - Compatibility";
        private const string SectionTransit = "6 - Transit";
        private const string SectionDiagnostics = "7 - Diagnostics";

        // -- Travel ----------------------------------------------------------------------------

        // There is no SelectionMode entry: rewire is the only travel model being built. Station is
        // recorded in DESIGN.md §13 as a future idea, and a config switch with one working value is
        // just a trap for whoever flips it.
        internal static ConfigEntry<bool> HidePortalNames { get; private set; }
        internal static ConfigEntry<ReaimPermission> Reaim { get; private set; }

        // -- Clearance -------------------------------------------------------------------------

        // There is no EnforceAtSource entry. Checking the source as well would mean an outpost with
        // no staves could not send ore anywhere, which kills the one-way outpost the whole design is
        // built on (R3). A setting that can switch off the central mechanic is not worth having.
        internal static ConfigEntry<bool> StrictLadder { get; private set; }
        internal static ConfigEntry<float> StaveRadius { get; private set; }
        internal static ConfigEntry<PortalBinding> Binding { get; private set; }
        internal static ConfigEntry<MaterialFlow> Flow { get; private set; }

        // Which blocked item belongs to which stave. The *list* of blocked items is never
        // configured — it is read from ObjectDB at runtime (§4) — but the mapping has to be, because
        // only a human can decide that a new ore belongs with iron rather than with silver.
        internal static ConfigEntry<string> ElderItems { get; private set; }
        internal static ConfigEntry<string> BonemassItems { get; private set; }
        internal static ConfigEntry<string> ModerItems { get; private set; }
        internal static ConfigEntry<string> YagluthItems { get; private set; }
        internal static ConfigEntry<string> QueenItems { get; private set; }
        internal static ConfigEntry<string> AshenItems { get; private set; }

        /// <summary>The tier lists, paired with the tier they grant. Read by <c>TierMap</c>.</summary>
        internal static IEnumerable<KeyValuePair<Clearance, string>> TierPrefabs()
        {
            yield return new KeyValuePair<Clearance, string>(Clearance.Elder, ElderItems.Value);
            yield return new KeyValuePair<Clearance, string>(Clearance.Bonemass, BonemassItems.Value);
            yield return new KeyValuePair<Clearance, string>(Clearance.Moder, ModerItems.Value);
            yield return new KeyValuePair<Clearance, string>(Clearance.Yagluth, YagluthItems.Value);
            yield return new KeyValuePair<Clearance, string>(Clearance.Queen, QueenItems.Value);
            yield return new KeyValuePair<Clearance, string>(Clearance.Ashen, AshenItems.Value);
        }

        // -- Cargo preview ---------------------------------------------------------------------

        internal static ConfigEntry<bool> ShowBlockedCargoOverlay { get; private set; }
        internal static ConfigEntry<bool> WarnOnApproach { get; private set; }
        internal static ConfigEntry<float> CargoPreviewRange { get; private set; }

        // -- Transit -----------------------------------------------------------------------------

        internal static ConfigEntry<bool> SeamlessTransit { get; private set; }
        internal static ConfigEntry<float> TransitPause { get; private set; }

        // -- Compatibility ---------------------------------------------------------------------

        internal static ConfigEntry<bool> WarnOnConflictingMods { get; private set; }
        internal static ConfigEntry<string> IgnoredConflictGuids { get; private set; }

        // -- Diagnostics -------------------------------------------------------------------------

        internal static ConfigEntry<bool> LogNetworkSync { get; private set; }

        internal static void Bind(ConfigFile config)
        {
            HidePortalNames = config.Bind(
                SectionTravel,
                "HidePortalNames",
                false,
                new ConfigDescription("Hide portal names in the map selector. Local to you."));

            Reaim = config.Bind(
                SectionTravel,
                "ReaimPermission",
                ReaimPermission.Anyone,
                Synced("Who may change where a portal points. Anyone: no restriction. " +
                       "GuardStonePermitted: inside a guard stone's area, only the players it permits. " +
                       "Admin: admins only."));

            // -- Clearance ---------------------------------------------------------------------

            StrictLadder = config.Bind(
                SectionClearance,
                "StrictLadder",
                false,
                Synced("A site's clearance stops at its first missing rung: with Elder's and Moder's but " +
                       "no Bonemass's, it accepts copper and refuses silver as well as iron. Off by " +
                       "default, and off is the shipped rule - per-tier flags are independent (R1), so " +
                       "a site can accept silver while refusing iron. Nothing stops you building any " +
                       "rune either way; this only changes what the ones you built are worth."));

            StaveRadius = config.Bind(
                SectionClearance,
                "StaveRadius",
                10f,
                Synced("How far a stave reaches to find the portal it grants clearance to (R2). " +
                       "A rune outside every portal's reach does nothing at all.",
                    new AcceptableValueRange<float>(2f, 64f)));

            Binding = config.Bind(
                SectionClearance,
                "PortalBinding",
                PortalBinding.Nearest,
                Synced("Nearest: a stave grants its clearance to the single closest portal in range. " +
                       "Re-aiming reaches everywhere from one portal, so a site only needs one. " +
                       "AllInRadius: every portal in range, for a base spread across more than one."));

            Flow = config.Bind(
                SectionClearance,
                "MaterialFlow",
                MaterialFlow.Both,
                Synced("Which end of a trip a site's staves count for. Receive: only the destination " +
                       "is checked, so a site takes what its staves permit from anywhere but cannot " +
                       "send those metals somewhere that lacks them - ore flows inward and outposts " +
                       "are one-way. Deliver: the mirror image, only the portal you leave is checked, " +
                       "so a stocked base can supply bare outposts but they cannot ship home. Both " +
                       "(default): either end is enough, and only two sites that both lack a tier " +
                       "cannot pass it between them."));

            // Prefab names, not display names, because prefab names are what ObjectDB is keyed on and
            // what survives a language change. These defaults are a starting guess: whatever they get
            // wrong shows up as an "unclassified" warning naming the prefab, which is the intended way
            // to find out rather than a failure.
            ElderItems = config.Bind(
                SectionClearance,
                "ElderItems",
                "CopperOre,Copper,TinOre,Tin,Bronze,CopperScrap,BronzeScrap,chest_hildir3",
                Synced("Blocked items an Elder's Stave permits. Comma-separated prefab names."));

            BonemassItems = config.Bind(
                SectionClearance,
                "BonemassItems",
                "IronScrap,Iron,IronOre,Ironpit",
                Synced("Blocked items a Bonemass's Stave permits. Comma-separated prefab names."));

            ModerItems = config.Bind(
                SectionClearance,
                "ModerItems",
                "SilverOre,Silver,DragonEgg,chest_hildir2",
                Synced("Blocked items a Moder's Stave permits. Comma-separated prefab names."));

            YagluthItems = config.Bind(
                SectionClearance,
                "YagluthItems",
                "BlackMetalScrap,BlackMetal,chest_hildir1",
                Synced("Blocked items a Yagluth's Stave permits. Comma-separated prefab names."));

            QueenItems = config.Bind(
                SectionClearance,
                "QueenItems",
                "DvergrNeedle,MechanicalSpring",
                Synced("Blocked items a Queen's Stave permits. Comma-separated prefab names. " +
                       "The Mistlands does block resources, which DESIGN.md §4 originally assumed it " +
                       "did not - the ObjectDB scan is what settled it."));

            AshenItems = config.Bind(
                SectionClearance,
                "AshenItems",
                "FlametalOre,Flametal,FlametalOreNew,FlametalNew,CharredCogwheel,Gold,GoldOre",
                Synced("Blocked items an Ashen Stave permits. Comma-separated prefab names. " +
                       "Anything blocked and unlisted lands here anyway, by design. Gold and GoldOre " +
                       "are the Deep North's Bloodgold and Petrified Tissue: game 1.0 gave that biome " +
                       "no summoning stone, so there is no farmable trophy to buy a stave of its own " +
                       "with, and its metals ride on Fader's instead. See DESIGN.md section 4."));

            // -- Cargo preview -----------------------------------------------------------------

            ShowBlockedCargoOverlay = config.Bind(
                SectionCargoPreview,
                "ShowBlockedCargoOverlay",
                true,
                new ConfigDescription("Mark inventory stacks the nearby portal's destination will refuse. " +
                                      "Purely visual — it reads the tier map, never item data. Local to you."));

            WarnOnApproach = config.Bind(
                SectionCargoPreview,
                "WarnOnApproach",
                true,
                new ConfigDescription("Name the offending item and the missing stave as you walk up " +
                                      "to a portal whose destination would refuse you, rather than at " +
                                      "the threshold. The portal's runes go dark either way. Local to you."));

            CargoPreviewRange = config.Bind(
                SectionCargoPreview,
                "CargoPreviewRange",
                8f,
                new ConfigDescription("How close to a portal the overlay switches on. Kept short on purpose: " +
                                      "showing it everywhere would paint your ore red all game and teach you " +
                                      "to ignore it. Local to you.",
                    new AcceptableValueRange<float>(2f, 32f)));

            // -- Transit -------------------------------------------------------------------------

            SeamlessTransit = config.Bind(
                SectionTransit,
                "SeamlessTransit",
                false,
                new ConfigDescription("End a portal trip when the destination has actually loaded, " +
                                      "rather than on vanilla's eight-second timer. A destination " +
                                      "already in memory skips the loading screen entirely; one that " +
                                      "is not shows it for exactly as long as loading takes. Loads " +
                                      "nothing early and waits for the same condition vanilla does. " +
                                      "Local to you."));

            TransitPause = config.Bind(
                SectionTransit,
                "TransitPause",
                0.5f,
                new ConfigDescription("Seconds to hold before moving you on a trip that needs no " +
                                      "loading, in place of vanilla's two. Kept rather than removed " +
                                      "because arriving in another biome with no beat at all is " +
                                      "disorienting. Local to you.",
                    new AcceptableValueRange<float>(0f, 2f)));

            // -- Compatibility -----------------------------------------------------------------

            WarnOnConflictingMods = config.Bind(
                SectionCompatibility,
                "WarnOnConflictingMods",
                true,
                new ConfigDescription("Log a warning at startup when another installed mod also rewrites " +
                                      "portal or teleport rules. Local to you."));

            IgnoredConflictGuids = config.Bind(
                SectionCompatibility,
                "IgnoredConflictGuids",
                string.Empty,
                new ConfigDescription("Comma-separated plugin GUIDs to leave out of the conflict warning, " +
                                      "for when the check flags something harmless. Local to you."));

            // -- Diagnostics -------------------------------------------------------------------

            // Defaults ON while the portal registry is still being proven against a real server.
            // Turn it off before release: it is a development aid, and a shipped mod that narrates
            // itself into everyone's log is a nuisance rather than a help.
            LogNetworkSync = config.Bind(
                SectionDiagnostics,
                "LogNetworkSync",
                false,
                new ConfigDescription("Log every step of the portal registry's sync - sweeps, broadcasts, " +
                                      "joins and receives - so a multiplayer problem can be read off one " +
                                      "log instead of reproduced. Local to you. Off by default now that " +
                                      "clearance has been confirmed across a real network; turn it on " +
                                      "before reporting anything about portals not agreeing between " +
                                      "machines, because the first question will be what this says."));
        }

        /// <summary>
        /// Marks an entry admin-only, which is what makes Jotunn synchronise it from the server and
        /// lock the client's copy.
        /// </summary>
        private static ConfigDescription Synced(string description, AcceptableValueBase acceptableValues = null)
        {
            return new ConfigDescription(
                description,
                acceptableValues,
                new ConfigurationManagerAttributes { IsAdminOnly = true });
        }
    }
}
