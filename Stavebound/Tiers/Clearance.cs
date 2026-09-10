using Stavebound.Config;
using System;

namespace Stavebound.Tiers
{
    /// <summary>
    /// What a site permits through its portal — independent per-tier flags, not a level (R1).
    /// <para>
    /// A site with an Elder's and a Moder's Stave accepts copper and silver and still refuses
    /// iron. Nothing forces you up the ladder in order, which is the point: you build the stave
    /// for the haul you actually make, not the one that comes next.
    /// </para>
    /// <para>
    /// Stored as an int on a ZDO and mirrored onto every portal at the site, so the values here are
    /// part of the save format. <b>Never renumber them</b> — a flag's meaning is written into worlds.
    /// </para>
    /// </summary>
    [Flags]
    internal enum Clearance
    {
        /// <summary>No staves. A Wayfarer's Anchor alone grants exactly this (tier 0).</summary>
        None = 0,

        /// <summary>Elder's Stave — copper ore and bar, tin ore and bar, bronze.</summary>
        Elder = 1 << 0,

        /// <summary>Bonemass's Stave — iron scrap and iron. The rule the whole idea started from.</summary>
        Bonemass = 1 << 1,

        /// <summary>Moder's Stave — silver ore, silver, dragon eggs.</summary>
        Moder = 1 << 2,

        /// <summary>Yagluth's Stave — black metal scrap and black metal.</summary>
        Yagluth = 1 << 3,

        /// <summary>
        /// Queen's Stave — the Mistlands' guarded things. Added after the ObjectDB scan proved the
        /// Mistlands does block resources, which §4 had assumed it did not.
        /// </summary>
        Queen = 1 << 4,

        /// <summary>Ashen Stave — flametal, and whatever else the Ashlands blocks.</summary>
        Ashen = 1 << 5,

        /// <summary>
        /// No stave carries this, and none ever will — the Deep North's Bloodgold and Petrified
        /// Tissue.
        /// <para>
        /// <b>Not a rung.</b> It is absent from <see cref="ClearanceExtensions.Ladder"/> and
        /// <see cref="ClearanceExtensions.All"/> deliberately, so no site can hold it, no chip shows
        /// it, and <c>StrictLadder</c> never counts it. Because a mask is built only from staves
        /// standing in the world, and no stave grants this, <c>Permits</c> answers false everywhere,
        /// forever.
        /// </para>
        /// <para>
        /// What that amounts to is <em>leaving vanilla alone</em>. The game already refuses to
        /// teleport these; every other tier is this mod handing back something vanilla withheld, and
        /// this one declines to. The stone portal remains the way to move them, which is what the
        /// base game intends — and it works without our help, because a portal with
        /// <c>m_allowAllItems</c> bypasses the gate entirely (see TeleportWorldPatches).
        /// </para>
        /// </summary>
        Sealed = 1 << 6,
    }

    internal static class ClearanceExtensions
    {
        /// <summary>
        /// The tier an unrecognised blocked item is assigned to. Deliberately the most restrictive
        /// one: a game update or another mod adding a blocked resource should make Stavebound
        /// conservative, never permissive. Being wrong here means someone cannot carry a new ore
        /// until a line of config is edited; the other way round means the mod silently stops
        /// enforcing the thing it exists to enforce. See DESIGN.md §4.
        /// </summary>
        internal const Clearance Highest = Clearance.Ashen;

        /// <summary>Every tier, for the ladder-complete case.</summary>
        internal const Clearance All = Clearance.Elder | Clearance.Bonemass | Clearance.Moder |
                                       Clearance.Yagluth | Clearance.Queen | Clearance.Ashen;

        /// <summary>
        /// The tiers in the order a player earns them. Only <c>StrictLadder</c> and display care about
        /// the order — the flags themselves are deliberately independent (R1).
        /// </summary>
        internal static readonly Clearance[] Ladder =
        {
            Clearance.Elder, Clearance.Bonemass, Clearance.Moder,
            Clearance.Yagluth, Clearance.Queen, Clearance.Ashen,
        };

        /// <summary>
        /// The mask truncated at its first missing rung, for <c>StrictLadder</c>.
        /// <para>
        /// A site with Elder's and Moder's but no Bonemass's keeps copper and loses silver: under a
        /// strict ladder a higher rune means nothing without the ones below it. Off by default,
        /// because independent flags are the shipped rule (R1) — this exists for servers that want
        /// the ladder climbed in order.
        /// </para>
        /// </summary>
        internal static Clearance UpToFirstGap(Clearance mask)
        {
            Clearance kept = Clearance.None;

            foreach (Clearance tier in Ladder)
            {
                if ((mask & tier) != tier)
                {
                    break;
                }

                kept |= tier;
            }

            return kept;
        }

        /// <summary>Does this mask permit <paramref name="required"/> through?</summary>
        internal static bool Permits(this Clearance mask, Clearance required)
        {
            // None is not a tier any item needs, so an unblocked item is permitted everywhere.
            return required == Clearance.None || (mask & required) == required;
        }

        /// <summary>
        /// A two-letter chip for the selector, naming the <em>resource</em> rather than the boss.
        /// <para>
        /// Cu, Fe, Ag and Fl are what §5 asked for, and they work because a player scanning a list is
        /// asking "can this take my iron", not "have I killed Bonemass". Dv stands for the dvergr
        /// machinery the Mistlands blocks, which has no metal to be named after.
        /// </para>
        /// </summary>
        internal static string Symbol(this Clearance tier)
        {
            switch (tier)
            {
                case Clearance.Elder: return "Cu";
                case Clearance.Bonemass: return "Fe";
                case Clearance.Moder: return "Ag";
                case Clearance.Yagluth: return "Bm";
                case Clearance.Queen: return "Dv";
                case Clearance.Ashen: return "Fl";
                default: return "--";
            }
        }

        /// <summary>
        /// The stave a player would have to build, named as they would recognise it. Used in
        /// refusals, where naming the missing piece is the whole difference between R6 and vanilla's
        /// "you cannot teleport with that".
        /// </summary>
        internal static string StaveName(this Clearance tier) => Translations.Get(NameToken(tier));

        /// <summary>The localisation token for a tier's piece name, shared by the piece and the refusal.</summary>
        internal static string NameToken(this Clearance tier)
        {
            switch (tier)
            {
                case Clearance.Elder: return Translations.RuneElder;
                case Clearance.Bonemass: return Translations.RuneBonemass;
                case Clearance.Moder: return Translations.RuneModer;
                case Clearance.Yagluth: return Translations.RuneYagluth;
                case Clearance.Queen: return Translations.RuneQueen;
                case Clearance.Ashen: return Translations.RuneAshen;
                default: return Translations.NoStave;
            }
        }
    }
}
