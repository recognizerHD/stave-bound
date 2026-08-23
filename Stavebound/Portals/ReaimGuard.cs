using Stavebound.Config;
using Jotunn.Managers;
using UnityEngine;

namespace Stavebound.Portals
{
    /// <summary>
    /// Answers "may this player point this portal somewhere else?" — the bound on the contention
    /// that shared destinations create (DESIGN.md §5).
    /// <para>
    /// Re-aiming is the only guarded action. Travelling never is: walking into a portal is the
    /// frequent, harmless verb, and gating it would make a shared destination unbearable rather than
    /// merely contentious.
    /// </para>
    /// </summary>
    internal static class ReaimGuard
    {
        /// <summary>
        /// <paramref name="refusal"/> is a localisation token where vanilla has one, so the player
        /// reads it in their own language.
        /// </summary>
        internal static bool MayReaim(Vector3 position, out string refusal)
        {
            refusal = null;

            switch (StaveboundConfig.Reaim.Value)
            {
                case ReaimPermission.Admin:
                    if (!SynchronizationManager.Instance.PlayerIsAdmin)
                    {
                        refusal = Translations.Get(Translations.ReaimAdminOnly);
                        return false;
                    }

                    return true;

                case ReaimPermission.GuardStonePermitted:
                    // Returns true outside any guard stone, which is the rule §5 describes: the
                    // restriction applies where somebody has claimed the ground, and nowhere else.
                    // Vanilla already gates portal interaction on exactly this call, so we are
                    // reusing the player's existing mental model rather than adding a second one.
                    if (!PrivateArea.CheckAccess(position, 0f, true, false))
                    {
                        refusal = "$piece_noaccess";
                        return false;
                    }

                    return true;

                default:
                    return true;
            }
        }
    }
}
