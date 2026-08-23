using Stavebound.Config;
using HarmonyLib;
using UnityEngine;

namespace Stavebound.Travel
{
    /// <summary>
    /// Stops the trip taking longer than the loading actually does (DESIGN.md §7).
    /// <para>
    /// This owns no zone loading and reimplements no loading screen. It changes <em>when vanilla
    /// stops waiting</em>, and nothing else — so it cannot make the game load anything sooner, and
    /// cannot leave a player standing somewhere that has not finished arriving.
    /// </para>
    /// <para>
    /// Reading <c>Player.UpdateTeleport</c> is what made this small. A distant trip is not slow
    /// because loading is slow; it is slow because the method refuses to even <em>check</em> whether
    /// the destination is ready until eight seconds have passed:
    /// <code>
    /// if (m_teleportTimer &lt;= 8f &amp;&amp; m_distantTeleport) return;
    /// if (!ZNetScene.instance.IsAreaReady(m_teleportTargetPos)) return;
    /// </code>
    /// Most of that screen is a fixed wait rather than work. Two things follow, and neither argues
    /// with the game about what "ready" means:
    /// </para>
    /// <para>
    /// <b>If the destination is already loaded when you step in</b> — the other end of your base, a
    /// place you just came from — there is nothing to wait for, so there is no screen. That case is
    /// common and entirely wasted time today.
    /// </para>
    /// <para>
    /// <b>If it is not loaded</b>, the screen appears exactly as vanilla intends and stays up while
    /// the zone genuinely loads. The only change is that it ends the moment
    /// <c>IsAreaReady</c> turns true instead of on the eight-second timer. Slow connections and
    /// far-flung destinations get the wait they need; nobody gets the wait they do not.
    /// </para>
    /// </summary>
    internal static class SeamlessTransit
    {
        /// <summary>Vanilla holds the player this long before moving them at all.</summary>
        private const float VanillaPause = 2f;

        private static readonly AccessTools.FieldRef<Player, float> TeleportTimer =
            AccessTools.FieldRefAccess<Player, float>("m_teleportTimer");

        internal static bool Enabled => StaveboundConfig.SeamlessTransit.Value;

        /// <summary>
        /// Is the far side already in memory? Asked at the moment of departure, and the only thing
        /// that decides whether a loading screen is warranted.
        /// </summary>
        internal static bool DestinationIsLoaded(Vector3 destination)
        {
            return ZNetScene.instance != null && ZNetScene.instance.IsAreaReady(destination);
        }

        /// <summary>
        /// Whether this trip needs the loading screen and the wait that goes with it.
        /// <para>
        /// This is the whole judgement, and it is deliberately conservative: anything other than a
        /// destination that is demonstrably ready gets stock behaviour.
        /// </para>
        /// </summary>
        internal static bool NeedsLoadingScreen(Vector3 destination)
        {
            return !Enabled || !DestinationIsLoaded(destination);
        }

        /// <summary>
        /// Trims vanilla's two-second hold for a trip that needs no loading, by starting the clock
        /// partway through rather than by rewriting the method that reads it.
        /// <para>
        /// A pause is kept rather than removed: arriving in another biome with no beat at all is
        /// disorienting, and §7 asks for a fade rather than a cut. Only applied when the screen is
        /// being skipped — a trip that is genuinely loading has its own reason to take time.
        /// </para>
        /// </summary>
        internal static void ShortenPause(Player player, Vector3 destination)
        {
            if (player == null || NeedsLoadingScreen(destination))
            {
                return;
            }

            float pause = Mathf.Clamp(StaveboundConfig.TransitPause.Value, 0f, VanillaPause);
            TeleportTimer(player) = VanillaPause - pause;
        }

        /// <summary>
        /// Ends a loading screen as soon as the destination has actually arrived.
        /// <para>
        /// Called every frame of a trip that <em>did</em> need the screen. Clearing
        /// <c>m_distantTeleport</c> once the area is ready lets vanilla's own next tick finish the
        /// teleport — the condition it was going to wait for is already true, and only the timer was
        /// still holding it. Nothing is skipped that was doing anything.
        /// </para>
        /// </summary>
        internal static bool ArrivalIsReady(Vector3 destination)
        {
            return Enabled && DestinationIsLoaded(destination);
        }
    }
}
