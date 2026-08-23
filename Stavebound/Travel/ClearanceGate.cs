using Stavebound.Config;
using System.Collections.Generic;
using Stavebound.Portals;
using Stavebound.Tiers;
using UnityEngine;

namespace Stavebound.Travel
{
    /// <summary>
    /// Decides whether a player may carry what they are carrying into a particular destination.
    /// <para>
    /// The rule the whole mod exists for (R3): <b>only the destination is checked</b>, never the
    /// portal you are standing in. That asymmetry is what makes ore flow inward — an outpost with no
    /// staves can send iron to your base all day and can never receive any, because sending asks
    /// nothing of the place you set out from.
    /// </para>
    /// <para>
    /// Client-trusting by necessity: player inventories are client-side in Valheim, so this runs on
    /// the traveller's machine and a determined cheat can bypass it. The server owns <em>clearance</em>
    /// and never <em>cargo</em>. This is a rule system for a co-op server, not anti-cheat (§6).
    /// </para>
    /// </summary>
    internal static class ClearanceGate
    {
        /// <summary>
        /// Where a portal sends you: our one-way target if it has been re-aimed, otherwise whatever
        /// vanilla's tag pairing connected it to.
        /// <para>
        /// Shared by the travel gate and the glow on purpose. They answer the same question — may this
        /// player travel from here — and the moment they resolve destinations differently, the portal
        /// starts lying about what walking into it will do.
        /// </para>
        /// </summary>
        internal static bool TryResolveDestination(ZDO portal, out ZDO destination, out long pendingPid)
        {
            destination = null;
            pendingPid = PortalTarget.NoPid;

            if (portal == null || ZDOMan.instance == null)
            {
                return false;
            }

            long targetPid = PortalTarget.GetDestination(portal);
            if (targetPid != PortalTarget.NoPid)
            {
                if (!PortalRegistry.TryGet(targetPid, out PortalRecord target))
                {
                    // Re-aimed at something that no longer exists, or at something this client has not
                    // been told about yet. Either way there is no destination to check.
                    return false;
                }

                pendingPid = targetPid;
                destination = ZDOMan.instance.GetZDO(target.Id);
                return true;
            }

            ZDOID connected = portal.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal);
            if (connected.IsNone())
            {
                return false;
            }

            destination = ZDOMan.instance.GetZDO(connected);
            return true;
        }

        /// <summary>
        /// May this player carry what they are holding into <paramref name="destination"/>? Used by the
        /// portal glow, which asks the question every frame and only wants a yes or no.
        /// </summary>
        internal static bool Allows(Player player, ZDO destination, bool allowAllItems)
        {
            if (allowAllItems)
            {
                return true;
            }

            return player != null && FirstRefusal(player.GetInventory(), MaskOf(destination)) == null;
        }

        /// <summary>Why a trip was refused, in the terms R6 asks a refusal to be phrased in.</summary>
        internal sealed class Refusal
        {
            /// <summary>The item that caused it, by display name.</summary>
            internal string Item;

            /// <summary>The stave the destination is missing.</summary>
            internal Clearance Missing;

            /// <summary>How many further stacks would also be refused, for a hint rather than a list.</summary>
            internal int OtherStacks;
        }

        /// <summary>
        /// The first thing in <paramref name="inventory"/> the destination will not accept, or null if
        /// the whole load may travel.
        /// <para>
        /// Only blocked items are considered at all: everything the game is happy to teleport passes
        /// without ever consulting a mask, so a site with no staves still behaves like a vanilla
        /// portal for wood, food and tools.
        /// </para>
        /// </summary>
        internal static Refusal FirstRefusal(Inventory inventory, Clearance destinationMask)
        {
            if (inventory == null)
            {
                return null;
            }

            Refusal first = null;
            var alsoRefused = 0;

            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item?.m_shared == null || item.m_shared.m_teleportable)
                {
                    continue;
                }

                // The tier map is keyed by prefab name, which is what ObjectDB is keyed on and what
                // survives a language change; m_shared.m_name is a localisation token.
                string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
                Clearance required = TierMap.RequiredFor(prefab);

                if (destinationMask.Permits(required))
                {
                    continue;
                }

                if (first == null)
                {
                    first = new Refusal
                    {
                        Item = Localization.instance != null
                            ? Localization.instance.Localize(item.m_shared.m_name)
                            : prefab,
                        Missing = required,
                    };

                    continue;
                }

                alsoRefused++;
            }

            if (first != null)
            {
                first.OtherStacks = alsoRefused;
            }

            return first;
        }

        /// <summary>
        /// R6, in one line: name the resource, name the place, name the missing stave.
        /// <para>
        /// "You cannot teleport with that" tells a player nothing they can act on. This tells them
        /// which of the twelve things in their inventory is the problem, and precisely what to go and
        /// build — which is the entire difference between a rule and an obstacle.
        /// </para>
        /// </summary>
        internal static string Explain(Refusal refusal, string destinationName)
        {
            string place = string.IsNullOrEmpty(destinationName)
                ? Translations.Get(Translations.ThatPortal)
                : $"\"{destinationName}\"";

            string message = Translations.Format(
                Translations.Refusal, refusal.Item, place, refusal.Missing.StaveName());

            if (refusal.OtherStacks > 0)
            {
                // A count rather than a list: naming twelve stacks in a HUD message helps nobody, and
                // the player only needs to know that fixing this one will not be the end of it.
                //
                // Phrased so it needs no plural form. English would want "stack" and "stacks", and
                // languages with three or six plural classes would want more than a translator should
                // have to encode in one token.
                message += Translations.Format(Translations.RefusalMore, refusal.OtherStacks);
            }

            return message;
        }

        /// <summary>
        /// The clearance a destination grants, read from the portal's own ZDO.
        /// <para>
        /// The server mirrors each site's mask onto its portal, so this works for a portal the
        /// traveller has never been near — which is the whole reason the mirror exists (§6).
        /// </para>
        /// </summary>
        internal static Clearance MaskOf(ZDO portal)
        {
            return portal == null ? Clearance.None : (Clearance)portal.GetInt(ZdoKeys.ClearanceMask, 0);
        }
    }
}
