using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;

namespace Stavebound.Portals
{
    /// <summary>
    /// <c>stave_aim</c> — point the portal you are standing at somewhere else.
    /// <para>
    /// A stand-in for the map selector, so the travel half of Phase 1 can be tested before the UI
    /// half exists. It deliberately picks the portal the same way the real interaction will —
    /// nearest within range — so what it exercises is the path that ships, not a shortcut around it.
    /// </para>
    /// <para>
    /// Worth keeping afterwards: re-aiming from the console is how you set up a travel test without
    /// clicking through a map, and it is the only way to aim a portal at one you have not discovered.
    /// </para>
    /// </summary>
    internal sealed class PortalAimCommand : StaveboundCommand
    {
        /// <summary>
        /// How far the command will look for "the portal you are at". Generous enough to work from
        /// wherever you happen to be standing, short enough not to grab the wrong one in a hub.
        /// </summary>
        private const float Range = 10f;

        public override string Name => "stave_aim";

        public override string Help =>
            "stave_aim <destination name> - point the nearest portal at the portal with that name. " +
            "With no arguments, clears the target and hands the portal back to vanilla tag pairing.";

        protected override void Execute(string[] args, Terminal context)
        {
            if (Player.m_localPlayer == null)
            {
                Echo(context,"Stavebound: no player.");
                return;
            }

            TeleportWorld portal = PortalTarget.FindNearest(Player.m_localPlayer.transform.position, Range);
            if (portal == null)
            {
                Echo(context,$"Stavebound: no portal within {Range:F0}m. Stand at the one you want to re-aim.");
                return;
            }

            ZDO zdo = PortalTarget.ZdoOf(portal);
            if (zdo == null)
            {
                Echo(context,"Stavebound: that portal is not ready yet.");
                return;
            }

            string source = zdo.GetString(ZDOVars.s_tag, string.Empty);
            source = string.IsNullOrEmpty(source) ? "the unnamed portal" : $"\"{source}\"";

            // Portal names contain spaces far more often than not, and the console splits on them.
            string wanted = string.Join(" ", args).Trim();

            if (wanted.Length == 0)
            {
                PortalTarget.Clear(zdo);
                Echo(context,$"Stavebound: {source} now follows vanilla tag pairing again.");
                return;
            }

            List<PortalRecord> matches = PortalRegistry.All
                .Where(p => string.Equals(p.Name, wanted, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                Echo(context,$"Stavebound: no portal named \"{wanted}\". Try stave_portals for the list.");
                return;
            }

            if (matches.Count > 1)
            {
                // Names stop carrying meaning at about a dozen portals, which is the whole argument
                // for selecting on the map (§5). The console cannot disambiguate; the map can.
                Echo(context,$"Stavebound: {matches.Count} portals are named \"{wanted}\". Rename one, or wait for the map selector.");
                return;
            }

            PortalRecord destination = matches[0];
            if (destination.Pid == PortalTarget.GetPid(zdo))
            {
                Echo(context,"Stavebound: a portal cannot point at itself.");
                return;
            }

            PortalTarget.Set(zdo, destination.Pid);
            Echo(context, $"Stavebound: {source} now points at {destination}. Nothing was written to the far side - walk back and you will not return here unless it points at you.");
        }

        /// <summary>Tab completion over the portals we know about, which is most of the point.</summary>
        public override List<string> CommandOptionList()
        {
            return PortalRegistry.All
                .Select(p => p.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }
    }
}
