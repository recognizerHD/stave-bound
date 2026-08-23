using Stavebound.Config;
using System;
using System.Collections.Generic;
using Stavebound.Tiers;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Stavebound.Staves
{
    /// <summary>
    /// The six staves (DESIGN.md §4).
    /// <para>
    /// All six are clones of <c>Pickable_BlackCoreStand</c> with its <c>Pickable</c> stripped and the
    /// core tinted, so nothing is shipped as art and nothing needs relicensing (§11). Sharing one
    /// silhouette is a design choice rather than a shortcut: six stands differing only in the colour
    /// of what they hold read as a matched set, and a player learns the shape once.
    /// </para>
    /// </summary>
    internal static class StavePieces
    {
        /// <summary>The prefab every piece is cloned from, verified present in game 0.221.12.</summary>
        private const string CloneSource = "Pickable_BlackCoreStand";

        // Trophy prefab names, read off a running game rather than remembered.
        private const string TrophyElder = "TrophyTheElder";
        private const string TrophyBonemass = "TrophyBonemass";
        private const string TrophyModer = "TrophyDragonQueen";
        private const string TrophyYagluth = "TrophyGoblinKing";
        private const string TrophyQueen = "TrophySeekerQueen";
        private const string TrophyFader = "TrophyFader";

        /// <summary>
        /// One buildable piece: what it is called, what it costs, and what colour its core burns.
        /// <para>
        /// Costs follow R4 — the boss's trophy plus a little of the metal that stave unlocks, so
        /// you always make the haul the hard way once before the shortcut exists. The numbers are
        /// placeholders in the sense §10 means: the shape is settled, the values want real play.
        /// </para>
        /// </summary>
        private sealed class PieceSpec
        {
            internal string Name;
            internal string Display;
            internal string Description;
            internal Color Tint;
            internal RequirementConfig[] Requirements;
        }

        private static readonly List<PieceSpec> Specs = new List<PieceSpec>
        {
            new PieceSpec
            {
                Name = "stave_elder",
                Display = Translations.RuneElder,
                Description = Translations.DescElder,
                // Yellow, for the brass-and-bronze end of the Black Forest rather than its trees.
                Tint = new Color(0.95f, 0.80f, 0.15f),
                Requirements = new[]
                {
                    new RequirementConfig { Item = TrophyElder, Amount = 1, Recover = true },
                    // Copper, not bronze: R4 asks for the metal the stave unlocks, and bronze is an
                    // alloy that drags tin in with it. Copper is the Black Forest's own haul.
                    new RequirementConfig { Item = "Copper", Amount = 10, Recover = true },
                    new RequirementConfig { Item = "Stone", Amount = 20, Recover = true },
                },
            },
            new PieceSpec
            {
                Name = "stave_bonemass",
                Display = Translations.RuneBonemass,
                Description = Translations.DescBonemass,
                // The green the Elder used to wear, which suits a swamp far better than a forest.
                Tint = new Color(0.25f, 0.85f, 0.30f),
                Requirements = new[]
                {
                    new RequirementConfig { Item = TrophyBonemass, Amount = 1, Recover = true },
                    new RequirementConfig { Item = "Iron", Amount = 10, Recover = true },
                    new RequirementConfig { Item = "Stone", Amount = 20, Recover = true },
                },
            },
            new PieceSpec
            {
                Name = "stave_moder",
                Display = Translations.RuneModer,
                Description = Translations.DescModer,
                Tint = new Color(0.75f, 0.85f, 1.00f),
                Requirements = new[]
                {
                    new RequirementConfig { Item = TrophyModer, Amount = 1, Recover = true },
                    new RequirementConfig { Item = "Silver", Amount = 10, Recover = true },
                    new RequirementConfig { Item = "Stone", Amount = 20, Recover = true },
                },
            },
            new PieceSpec
            {
                Name = "stave_yagluth",
                Display = Translations.RuneYagluth,
                Description = Translations.DescYagluth,
                Tint = new Color(0.45f, 0.15f, 0.65f),
                Requirements = new[]
                {
                    new RequirementConfig { Item = TrophyYagluth, Amount = 1, Recover = true },
                    new RequirementConfig { Item = "BlackMetal", Amount = 10, Recover = true },
                    new RequirementConfig { Item = "Stone", Amount = 20, Recover = true },
                },
            },
            new PieceSpec
            {
                Name = "stave_queen",
                Display = Translations.RuneQueen,
                Description = Translations.DescQueen,
                // Cyan-white, the colour of wisplight and eitr rather than of a metal - the Mistlands
                // has no ore, and pretending otherwise would make this rune look like a sixth smelter.
                Tint = new Color(0.40f, 0.95f, 0.95f),
                Requirements = new[]
                {
                    new RequirementConfig { Item = TrophyQueen, Amount = 1, Recover = true },
                    // Extractors are themselves one of the things this rune unlocks, so building it
                    // means carrying three of them here by boat and cart first. R4 asks you to make
                    // the haul the hard way once; nowhere else on the ladder is the cost the cargo.
                    new RequirementConfig { Item = "DvergrNeedle", Amount = 3, Recover = true },
                    new RequirementConfig { Item = "Stone", Amount = 20, Recover = true },
                },
            },
            new PieceSpec
            {
                Name = "stave_ashen",
                Display = Translations.RuneAshen,
                Description = Translations.DescAshen,
                Tint = new Color(1.00f, 0.35f, 0.10f),
                Requirements = new[]
                {
                    new RequirementConfig { Item = TrophyFader, Amount = 1, Recover = true },
                    new RequirementConfig { Item = "FlametalNew", Amount = 10, Recover = true },
                    new RequirementConfig { Item = "Stone", Amount = 20, Recover = true },
                },
            },
        };

        /// <summary>Maps a built piece back to the clearance it grants.</summary>
        private static readonly Dictionary<string, Clearance> Granted = new Dictionary<string, Clearance>
        {
            { "stave_elder", Clearance.Elder },
            { "stave_bonemass", Clearance.Bonemass },
            { "stave_moder", Clearance.Moder },
            { "stave_yagluth", Clearance.Yagluth },
            { "stave_queen", Clearance.Queen },
            { "stave_ashen", Clearance.Ashen },
        };

        /// <summary>Each stave prefab and the clearance it grants, for the site sweep to gather.</summary>
        internal static IEnumerable<KeyValuePair<string, Clearance>> Staves => Granted;

        /// <summary>What clearance a prefab grants, or None if it is not one of ours.</summary>
        internal static Clearance ClearanceOf(string prefabName)
        {
            return Granted.TryGetValue(prefabName, out Clearance tier) ? tier : Clearance.None;
        }

        /// <summary>
        /// Registers all six once the vanilla prefabs exist. The event fires before any world loads,
        /// which is while a piece table can still be extended.
        /// </summary>
        internal static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
        }

        private static void Create()
        {
            // One-shot: the event fires again on later world loads, and registering a piece twice is
            // an error rather than a no-op.
            PrefabManager.OnVanillaPrefabsAvailable -= Create;

            if (PrefabManager.Instance.GetPrefab(CloneSource) == null)
            {
                Jotunn.Logger.LogError(
                    $"'{CloneSource}' is missing, so no staves can be built. A game update may have " +
                    "renamed it — run stave_prefabs corestand to find its new name.");
                return;
            }

            foreach (PieceSpec spec in Specs)
            {
                try
                {
                    CreateOne(spec);
                }
                catch (Exception exception)
                {
                    // One bad piece should not cost the other five.
                    Jotunn.Logger.LogError($"Could not create {spec.Name}: {exception}");
                }
            }
        }

        /// <summary>
        /// Complains about any requirement whose prefab does not exist.
        /// <para>
        /// A mistyped requirement does not throw — it produces a piece that simply cannot be built,
        /// with nothing in the log and no way to tell it apart from one you have not earned yet. Every
        /// name here came from a running game, but game updates rename things, and this is how that
        /// gets noticed on the first launch rather than the first complaint.
        /// </para>
        /// </summary>
        private static void CheckRequirements(PieceSpec spec)
        {
            foreach (RequirementConfig requirement in spec.Requirements)
            {
                if (PrefabManager.Instance.GetPrefab(requirement.Item) == null)
                {
                    Jotunn.Logger.LogError(
                        $"{spec.Name} needs '{requirement.Item}', which does not exist. That piece " +
                        "will be unbuildable until the name is corrected - try stave_prefabs to find it.");
                }
            }
        }

        private static void CreateOne(PieceSpec spec)
        {
            CheckRequirements(spec);

            // Cloned by hand rather than through CustomPiece(name, baseName, config), because that
            // overload assumes the base is already a build piece and rejects anything without a Piece
            // component. The source here is loot, so the clone has to be made buildable first.
            GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(spec.Name, CloneSource);
            if (prefab == null)
            {
                Jotunn.Logger.LogError($"Could not clone {CloneSource} for {spec.Name}.");
                return;
            }

            // Pickable is what makes it harvestable and what puts "Pick up" on the hover text; with it
            // gone the core simply stays lit, because the mesh it hides is a separate child object
            // that nothing else touches.
            var pickable = prefab.GetComponent<Pickable>();
            if (pickable != null)
            {
                UnityEngine.Object.DestroyImmediate(pickable);
            }

            MakeBuildable(prefab);
            CoreTint.Apply(prefab, spec.Tint, spec.Name);

            var piece = new CustomPiece(prefab, fixReference: false, new PieceConfig
            {
                // Tokens, not text: Jotunn localises a PieceConfig name beginning with $, so the
                // build menu shows these in the player's language rather than in mine.
                Name = $"${spec.Display}",
                Description = $"${spec.Description}",
                PieceTable = PieceTables.Hammer,
                Category = "Misc",
                Icon = RenderIcon(prefab, spec.Name),
                Requirements = spec.Requirements,
            });

            if (!piece.IsValid())
            {
                Jotunn.Logger.LogError($"{spec.Name} did not come out valid and will not be registered.");
                return;
            }

            PieceManager.Instance.AddPiece(piece);
            Jotunn.Logger.LogInfo($"Registered piece {spec.Name}.");
        }

        /// <summary>
        /// Photographs the finished piece for its build-menu icon.
        /// <para>
        /// Jotunn refuses to register a piece without one, and rendering the prefab beats drawing six
        /// icons by hand for two reasons: no art ships, so §11 stays intact, and the icon is generated
        /// <em>after</em> tinting, so each rune's icon shows its own colour. Hand-drawn icons would
        /// have to be redrawn every time a tint changed.
        /// </para>
        /// </summary>
        private static Sprite RenderIcon(GameObject prefab, string describe)
        {
            try
            {
                Sprite icon = RenderManager.Instance.Render(new RenderManager.RenderRequest(prefab)
                {
                    Rotation = RenderManager.IsometricRotation,
                    Width = 128,
                    Height = 128,

                    // Uncached. The icon is a photograph of the tinted prefab, so a cached one
                    // outlives the tint it was taken of - change a rune's colour and its icon keeps
                    // the old one, which looks like the tint failed. Seven small renders at startup
                    // is a price worth paying to never debug that.
                    UseCache = false,
                });

                if (icon == null)
                {
                    Jotunn.Logger.LogWarning($"No icon could be rendered for {describe}.");
                }

                return icon;
            }
            catch (Exception exception)
            {
                // An icon is cosmetic; losing one should not cost the piece it belongs to.
                Jotunn.Logger.LogWarning($"Icon render failed for {describe}: {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// Turns a cloned prop into something the hammer can place and remove.
        /// <para>
        /// <c>Piece</c> is what the build system looks for. <c>WearNTear</c> is what lets it be
        /// repaired and taken down again, and it is pointed at the existing <c>visual</c> child so the
        /// damage states have something to hide rather than a null to trip over.
        /// </para>
        /// </summary>
        private static void MakeBuildable(GameObject prefab)
        {
            Piece piece = prefab.GetComponent<Piece>() ?? prefab.AddComponent<Piece>();
            piece.m_canBeRemoved = true;
            piece.m_canRotate = true;
            piece.m_allowedInDungeons = false;

            // Placeable on anything: terrain, a stone floor, a wooden deck. m_groundPiece and
            // m_groundOnly are both read by UpdatePlacementGhost, and setting the first was enough to
            // make every surface that was not raw terrain read as an invalid placement.
            //
            // There is no reason for a stave to insist on soil. A site is often a built-up base
            // with a paved floor, and telling a player they may not put a rune on their own hall is
            // a rule with nothing behind it.
            piece.m_groundPiece = false;
            piece.m_groundOnly = false;
            piece.m_noInWater = true;

            if (prefab.GetComponent<WearNTear>() != null)
            {
                return;
            }

            WearNTear wear = prefab.AddComponent<WearNTear>();
            wear.m_health = 500f;
            wear.m_burnable = false;

            // Stone standing in the open: rain and lack of a roof should not rot it, and a site is
            // often a bare outcrop with nothing to support anything.
            wear.m_noRoofWear = true;
            wear.m_noSupportWear = true;
            wear.m_materialType = WearNTear.MaterialType.Stone;

            // m_new, m_worn and m_broken stay null, all three of them. SetHealthVisual returns early
            // only when every one is null; set just m_new — the obvious thing — and it walks straight
            // into m_worn.SetActive on a null reference, once per placement and once per load.
            //
            // A stone stand has no damaged mesh to swap to, so there is nothing to lose here: it takes
            // damage, it holds its shape, and it disappears when destroyed.
        }
    }
}
