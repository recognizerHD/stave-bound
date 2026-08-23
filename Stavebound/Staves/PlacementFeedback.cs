using System.Collections.Generic;
using System.Linq;
using Stavebound.Config;
using Stavebound.Portals;
using UnityEngine;

namespace Stavebound.Staves
{
    /// <summary>
    /// Answers the two questions a player has while holding a stave, before they commit to placing
    /// it (DESIGN.md §5): what its range is, and which portal it would actually reach.
    /// <para>
    /// The spec calls this required rather than polish, and it is right. Everything else in the mod is
    /// enforced silently: a rune eleven metres from a portal does nothing, looks identical to one that
    /// works, and the only way to discover the difference is to haul ore across the map and be refused
    /// at the far end. That is a rule the player cannot see, which makes it feel like a bug.
    /// </para>
    /// <para>
    /// Which visual appears follows what the answer actually is. Under <c>Nearest</c> exactly one
    /// portal matters, so it draws vanilla's station-connection beam straight to it — the same effect
    /// the game already uses to say "this piece belongs to that station", which players can read
    /// without being taught. Under <c>AllInRadius</c> there is no single portal to point at, so it
    /// falls back to the coverage circle, which is the honest shape of that answer.
    /// </para>
    /// </summary>
    internal static class PlacementFeedback
    {
        /// <summary>
        /// How often the portal search runs. The build ghost updates every frame and scanning for
        /// portals every frame is wasteful for an answer that changes at walking pace.
        /// </summary>
        private const float SearchInterval = 0.2f;

        /// <summary>Lifts both ends of the beam clear of the ground, as vanilla does with its own offset.</summary>
        private static readonly Vector3 BeamLift = Vector3.up * 1.2f;

        private static GameObject _circle;
        private static CircleProjector _projector;
        private static GameObject _beam;
        private static bool _beamUnavailable;

        private static float _nextSearch;
        private static readonly List<TeleportWorld> InRange = new List<TeleportWorld>();

        /// <summary>What the last search concluded, so the message fires on change rather than always.</summary>
        private static string _lastAnswer;

        /// <summary>Called every frame the build ghost updates, from the <c>Player</c> patch.</summary>
        internal static void Update(GameObject ghost)
        {
            if (ghost == null || !IsOurs(ghost))
            {
                Hide();
                return;
            }

            Vector3 at = ghost.transform.position;
            float radius = StaveboundConfig.StaveRadius.Value;

            Search(at, radius);

            bool all = StaveboundConfig.Binding.Value == PortalBinding.AllInRadius;
            TeleportWorld nearest = InRange.Count > 0 ? InRange[0] : null;

            if (!all && nearest != null)
            {
                ShowBeam(at + BeamLift, nearest.transform.position + BeamLift);
                HideCircle();
            }
            else
            {
                // Either every portal in range counts, or none does. In both cases there is no single
                // portal to point at — and when nothing is in range the circle is the more useful of
                // the two anyway, because it shows how much further you would have to move.
                ShowCircle(at, radius);
                HideBeam();
            }

            Announce(all);
        }

        /// <summary>Tears everything down with the world, so nothing outlives the game it belongs to.</summary>
        internal static void Reset()
        {
            if (_circle != null)
            {
                Object.Destroy(_circle);
            }

            if (_beam != null)
            {
                Object.Destroy(_beam);
            }

            _circle = null;
            _projector = null;
            _beam = null;
            _beamUnavailable = false;
            _lastAnswer = null;
            _nextSearch = 0f;
            InRange.Clear();
        }

        /// <summary>
        /// Is the thing being placed one of ours? The ghost is a clone, so its name carries a suffix.
        /// </summary>
        private static bool IsOurs(GameObject ghost)
        {
            string name = ghost.name.Replace("(Clone)", string.Empty).Trim();
            return StavePieces.ClearanceOf(name) != Tiers.Clearance.None;
        }

        private static void Search(Vector3 at, float radius)
        {
            if (Time.time < _nextSearch)
            {
                return;
            }

            _nextSearch = Time.time + SearchInterval;

            float limit = radius * radius;
            InRange.Clear();
            InRange.AddRange(Object
                .FindObjectsByType<TeleportWorld>(FindObjectsSortMode.None)
                .Where(p => p != null && (p.transform.position - at).sqrMagnitude <= limit)
                .OrderBy(p => (p.transform.position - at).sqrMagnitude));
        }

        // -- The beam ----------------------------------------------------------------------------

        /// <summary>
        /// Positions vanilla's connection effect between two points, the way vanilla does: anchored at
        /// the source, turned to face the target, and stretched along Z to reach it.
        /// </summary>
        private static void ShowBeam(Vector3 from, Vector3 to)
        {
            if (_beam == null && !BuildBeam(from))
            {
                return;
            }

            Vector3 delta = to - from;
            if (delta.sqrMagnitude < 0.001f)
            {
                return;
            }

            _beam.transform.position = from;
            _beam.transform.rotation = Quaternion.LookRotation(delta.normalized);
            _beam.transform.localScale = new Vector3(1f, 1f, delta.magnitude);

            if (!_beam.activeSelf)
            {
                _beam.SetActive(true);
            }
        }

        /// <summary>
        /// Borrows the connection effect from whatever piece already extends a crafting station.
        /// <para>
        /// Found rather than named, for the same reason as the circle: the prefab lives on the pieces
        /// that use it, and hunting for it by name would break the first time one was renamed.
        /// </para>
        /// </summary>
        private static bool BuildBeam(Vector3 at)
        {
            if (_beamUnavailable)
            {
                return false;
            }

            StationExtension template = Resources.FindObjectsOfTypeAll<StationExtension>()
                .FirstOrDefault(e => e != null && e.m_connectionPrefab != null);

            if (template == null)
            {
                // Not fatal: the circle and the naming both still work, and a missing beam should not
                // stop anyone placing a piece.
                _beamUnavailable = true;
                Jotunn.Logger.LogWarning(
                    "No station-connection effect to borrow, so stave binding will be shown as a " +
                    "circle instead of a beam.");
                return false;
            }

            _beam = Object.Instantiate(template.m_connectionPrefab, at, Quaternion.identity);
            _beam.name = "stave_binding_beam";
            return true;
        }

        private static void HideBeam()
        {
            if (_beam != null && _beam.activeSelf)
            {
                _beam.SetActive(false);
            }
        }

        // -- The circle --------------------------------------------------------------------------

        private static void ShowCircle(Vector3 at, float radius)
        {
            if (_projector == null && !BuildCircle())
            {
                return;
            }

            _circle.transform.position = at;
            _projector.m_radius = radius;

            if (!_circle.activeSelf)
            {
                _circle.SetActive(true);
            }
        }

        /// <summary>
        /// Borrows a circle from whatever vanilla piece already draws one — workbench coverage, guard
        /// stone area, anything. They all draw the same dotted ring.
        /// </summary>
        private static bool BuildCircle()
        {
            CircleProjector template = Resources.FindObjectsOfTypeAll<CircleProjector>()
                .FirstOrDefault(c => c != null && c.m_prefab != null);

            if (template == null)
            {
                Jotunn.Logger.LogWarning(
                    "No CircleProjector to borrow, so stave range will not be drawn. " +
                    "The portal it binds to is still named on placement.");
                return false;
            }

            _circle = new GameObject("stave_range");
            _projector = _circle.AddComponent<CircleProjector>();
            _projector.m_prefab = template.m_prefab;
            _projector.m_mask = template.m_mask;
            _projector.m_nrOfSegments = template.m_nrOfSegments;

            return true;
        }

        private static void HideCircle()
        {
            if (_circle != null && _circle.activeSelf)
            {
                _circle.SetActive(false);
            }
        }

        private static void Hide()
        {
            HideCircle();
            HideBeam();
            _lastAnswer = null;
        }

        // -- Words -------------------------------------------------------------------------------

        /// <summary>
        /// Names the portal this rune would bind to, and says so only when the answer changes. Kept
        /// alongside the beam because a beam shows you <em>that</em> something is connected; the name
        /// tells you <em>which</em>, and it is the only thing that can say "nothing at all".
        /// </summary>
        private static void Announce(bool all)
        {
            string answer = Describe(all);
            if (answer == _lastAnswer)
            {
                return;
            }

            _lastAnswer = answer;
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, answer);
        }

        private static string Describe(bool all)
        {
            if (InRange.Count == 0)
            {
                return Translations.Get(Translations.PlaceNoPortal);
            }

            if (all)
            {
                return InRange.Count == 1
                    ? Translations.Format(Translations.PlaceBinds, Name(InRange[0]))
                    : Translations.Format(Translations.PlaceBindsAll, InRange.Count);
            }

            return InRange.Count == 1
                ? Translations.Format(Translations.PlaceBinds, Name(InRange[0]))
                : Translations.Format(Translations.PlaceBindsNearest, Name(InRange[0]), InRange.Count);
        }

        private static string Name(TeleportWorld portal)
        {
            ZDO zdo = PortalTarget.ZdoOf(portal);
            string tag = zdo != null ? zdo.GetString(ZDOVars.s_tag, string.Empty) : string.Empty;
            return string.IsNullOrEmpty(tag) ? Translations.Get(Translations.AnUnnamedPortal) : $"\"{tag}\"";
        }
    }
}
