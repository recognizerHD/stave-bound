using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stavebound.Config;
using Stavebound.Portals;
using Stavebound.Tiers;
using Stavebound.Travel;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Stavebound.UI
{
    /// <summary>
    /// Picking where a portal points, on the world map and in the list beside it.
    /// <para>
    /// There is one highlighted destination and two renderings of it (DESIGN.md §5). The map answers
    /// <em>where is it</em>; the list answers <em>what are my options, in an order I chose</em>, and
    /// stays useful when the destination is off the visible map or has no name worth recognising.
    /// </para>
    /// <para>
    /// That both views come almost free is the payoff for making selection a <em>highlight</em> the
    /// mouse and the stick both move, rather than a click target with keyboard support bolted on. A
    /// second view of one highlight costs a rendering loop; two independent selections would have
    /// cost a reconciliation problem. It is also why the gamepad works without a single Unity UI
    /// navigation component — nothing has focus, so nothing has to be told where focus goes next.
    /// </para>
    /// <para>
    /// Confirming is always a separate keypress from highlighting, including with the mouse. A
    /// re-aim changes everyone's route — possibly someone's mid-haul — so a stray click on a map
    /// should not be able to do it.
    /// </para>
    /// </summary>
    internal static class DestinationSelector
    {
        private static readonly List<PortalRecord> Candidates = new List<PortalRecord>();
        private static readonly List<Minimap.PinData> Pins = new List<Minimap.PinData>();

        /// <summary>
        /// How the list is ordered. Favourites are the third one §5 asks for; they need somewhere
        /// per-player to persist, so they wait until there is a reason to build that.
        /// </summary>
        private enum SortOrder
        {
            Distance,
            Name,
        }

        /// <summary>
        /// How many destinations the list shows at once. The window scrolls to keep the highlight in
        /// view rather than paging, so the entries either side stay visible and moving through the
        /// list feels continuous.
        /// <para>
        /// Seven rather than nine since each row grew a line of clearance chips. This is a budget as
        /// much as a layout: an outlined UI Text costs about twenty mesh vertices per character and
        /// Unity discards the entire mesh past 65000, so overrunning it blanks the panel instead of
        /// truncating it.
        /// </para>
        /// </summary>
        private const int VisibleRows = 7;

        private static ZDOID _sourceId;
        private static long _sourcePid;
        private static Vector3 _sourcePosition;
        private static string _sourceName;
        private static SortOrder _order = SortOrder.Distance;
        private static bool _onlyWhatAcceptsMyCargo;
        private static int _highlight;

        /// <summary>
        /// The tiers the player is currently carrying something for, worked out once when the selector
        /// opens. Recomputing it per row would ask the same question of the same inventory dozens of
        /// times a frame for an answer that cannot change while a modal panel is up.
        /// </summary>
        private static Clearance _carrying;

        /// <summary>
        /// The departure portal's own clearance, read once when the selector opens. Under
        /// <c>MaterialFlow.Deliver</c> and <c>Both</c> it is half of every row's verdict, and it
        /// cannot change while a modal panel is up.
        /// </summary>
        private static Clearance _sourceMask;
        private static GameObject _panel;
        private static Text _text;
        private static bool _updateSeen;

        internal static bool IsOpen { get; private set; }

        /// <summary>
        /// Opens the selector for a portal the player is standing at. Refuses, with a reason, rather
        /// than opening an empty map.
        /// </summary>
        internal static void Open(TeleportWorld portal, Humanoid who)
        {
            ZDO source = PortalTarget.ZdoOf(portal);
            if (source == null || Minimap.instance == null)
            {
                return;
            }

            if (!ReaimGuard.MayReaim(portal.transform.position, out string refusal))
            {
                who?.Message(MessageHud.MessageType.Center, refusal);
                return;
            }

            _sourceId = source.m_uid;
            long sourcePid = PortalTarget.GetPid(source);
            string tag = source.GetString(ZDOVars.s_tag, string.Empty);
            _sourceName = string.IsNullOrEmpty(tag) ? "this portal" : $"\"{tag}\"";

            _sourcePosition = portal.transform.position;
            _sourcePid = sourcePid;
            _sourceMask = ClearanceGate.MaskOf(source);
            _carrying = CarriedTiers(who as Player);
            _onlyWhatAcceptsMyCargo = false;

            Rebuild(PortalTarget.NoPid);

            if (Candidates.Count == 0)
            {
                who?.Message(MessageHud.MessageType.Center, Translations.Get(Translations.SelectorNowhere));
                return;
            }

            // Start on wherever it already points, so re-opening the selector shows you the current
            // answer instead of making you find it.
            long current = PortalTarget.GetDestination(source);
            _highlight = Candidates.FindIndex(p => p.Pid == current);
            if (_highlight < 0)
            {
                _highlight = 0;
            }

            AddPins();
            BuildPanel();

            IsOpen = true;
            _updateSeen = false;
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            ShowHighlight();

            Jotunn.Logger.LogInfo($"Selector opened for {_sourceName} with {Candidates.Count} destination(s).");
        }

        /// <summary>Driven from the <c>Minimap.Update</c> postfix, so it stops when the map does.</summary>
        internal static void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (!_updateSeen)
            {
                // Says out loud that the selector is being driven at all. Whether this line appears
                // separates "the keys are not reaching us" from "nothing is running", and guessing
                // between those two cost several rounds of testing.
                _updateSeen = true;
                Jotunn.Logger.LogInfo("Selector is receiving frames.");
            }

            // The player closed the map out from under us — Escape, the map key, anything. Treat it
            // as a cancel rather than leaving a selector running behind a closed map.
            //
            // m_mode, not m_mapLarge: SetMapMode toggles m_largeRoot and leaves m_mapLarge alone, so
            // watching that one meant this never fired and the panel could not be dismissed at all.
            if (Minimap.instance == null || Minimap.instance.m_mode != Minimap.MapMode.Large)
            {
                Close();
                return;
            }

            if (Cancelled())
            {
                Close();
                return;
            }

            if (Confirmed())
            {
                Commit();
                return;
            }

            if (SelectorKeys.Pressed(SelectorKeys.Filter))
            {
                _onlyWhatAcceptsMyCargo = !_onlyWhatAcceptsMyCargo;
                Rebuild(Held());
                ShowHighlight();
                return;
            }

            if (SelectorKeys.Pressed(SelectorKeys.Sort))
            {
                _order = _order == SortOrder.Distance ? SortOrder.Name : SortOrder.Distance;
                Rebuild(Held());
                ShowHighlight();
                return;
            }

            int step = Stepped();
            if (step != 0)
            {
                // Wraps, because a list you can fall off the end of is worse than one you can loop.
                _highlight = (_highlight + step + Candidates.Count) % Candidates.Count;
                ShowHighlight();
            }
        }

        /// <summary>The pid currently highlighted, so a rebuild can put the selection back on it.</summary>
        private static long Held()
        {
            return _highlight >= 0 && _highlight < Candidates.Count
                ? Candidates[_highlight].Pid
                : PortalTarget.NoPid;
        }

        /// <summary>
        /// Refills the candidate list from the registry, applying the filter and the sort, then puts
        /// the highlight back on <paramref name="keep"/> if it survived.
        /// <para>
        /// Filtering and sorting share this one path deliberately. Both reorder the list under a fixed
        /// index, and an index that silently comes to mean a different portal is how somebody re-aims
        /// a portal they did not mean to.
        /// </para>
        /// </summary>
        private static void Rebuild(long keep)
        {
            Candidates.Clear();
            Candidates.AddRange(PortalRegistry.All.Where(p => p.Pid != _sourcePid));

            if (_onlyWhatAcceptsMyCargo && _carrying != Clearance.None)
            {
                Candidates.RemoveAll(p => !Accepts(p));
            }

            Sort();

            _highlight = keep == PortalTarget.NoPid ? 0 : Candidates.FindIndex(p => p.Pid == keep);
            if (_highlight < 0)
            {
                // The one you were on is filtered out. Start again rather than land somewhere arbitrary.
                _highlight = 0;
            }
        }

        private static void Sort()
        {
            if (_order == SortOrder.Name)
            {
                Candidates.Sort((a, b) => string.Compare(Describe(a), Describe(b), StringComparison.CurrentCultureIgnoreCase));
                return;
            }

            Candidates.Sort((a, b) => Vector3.Distance(_sourcePosition, a.Position)
                .CompareTo(Vector3.Distance(_sourcePosition, b.Position)));
        }

        /// <summary>
        /// A click on the map moves the highlight to the nearest destination. It deliberately does
        /// not confirm — see the note on the class.
        /// </summary>
        internal static void HighlightNearest(Vector3 worldPoint)
        {
            if (!IsOpen || Candidates.Count == 0)
            {
                return;
            }

            int nearest = 0;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < Candidates.Count; i++)
            {
                float distance = (Candidates[i].Position - worldPoint).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            _highlight = nearest;
            ShowHighlight();
        }

        private static void Commit()
        {
            ZDO source = ZDOMan.instance?.GetZDO(_sourceId);
            if (source == null)
            {
                // The portal was destroyed while its own selector was open. Rare, but free to handle.
                Close();
                return;
            }

            PortalRecord destination = Candidates[_highlight];
            PortalTarget.Set(source, destination.Pid);

            Player.m_localPlayer?.Message(
                MessageHud.MessageType.Center,
                Translations.Format(Translations.SelectorAimed, _sourceName, Describe(destination)));

            Close();
        }

        /// <summary>
        /// Tears the selector down unconditionally, for the world ending underneath it. Without this
        /// a panel could outlive the world it belongs to and follow the player to the main menu.
        /// </summary>
        internal static void Reset() => Close();

        private static void Close()
        {
            IsOpen = false;
            RemovePins();

            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
                _text = null;
            }

            Candidates.Clear();

            if (Minimap.instance != null && Minimap.instance.m_mapLarge != null &&
                Minimap.instance.m_mapLarge.activeSelf)
            {
                Minimap.instance.SetMapMode(Minimap.MapMode.Small);
            }
        }

        // -- Presentation ------------------------------------------------------------------------

        private static void AddPins()
        {
            RemovePins();

            foreach (PortalRecord portal in Candidates)
            {
                Minimap.PinData pin = Minimap.instance.AddPin(
                    portal.Position,
                    Minimap.PinType.Icon3,
                    Describe(portal),
                    save: false,
                    isChecked: false,
                    ownerID: 0L,
                    author: default);

                Pins.Add(pin);
            }
        }

        private static void RemovePins()
        {
            foreach (Minimap.PinData pin in Pins)
            {
                if (pin != null)
                {
                    Minimap.instance?.RemovePin(pin);
                }
            }

            Pins.Clear();
        }

        private static void ShowHighlight()
        {
            if (_text == null)
            {
                return;
            }

            if (Candidates.Count == 0)
            {
                // Only reachable with the cargo filter on: there are destinations, just none that
                // would take what you are holding. Saying so beats an empty box.
                var empty = new StringBuilder();
                empty.AppendLine(Translations.Format(Translations.SelectorTitle, _sourceName));
                empty.AppendLine();
                empty.AppendLine($"<color=#E06C4A>{Translations.Get(Translations.SelectorEmpty)}</color>");
                empty.AppendLine();
                empty.Append($"<size=13>[{Bound(SelectorKeys.Filter)}] {Translations.Get(Translations.SelectorShowAll)}   " +
                             $"[{Bound(SelectorKeys.Cancel)}] {Translations.Get(Translations.Cancel)}</size>");

                SetPanelText(empty.ToString());
                return;
            }

            PortalRecord destination = Candidates[_highlight];

            // Only on change, so the map still pans under the player's own hand between steps.
            Minimap.instance?.ShowPointOnMap(destination.Position);

            var panel = new StringBuilder();
            panel.AppendLine(Translations.Format(Translations.SelectorTitle, _sourceName));
            string ordering = Translations.Get(_order == SortOrder.Distance
                ? Translations.SelectorByDistance
                : Translations.SelectorByName);
            string filtered = _onlyWhatAcceptsMyCargo ? Translations.Get(Translations.SelectorFiltered) : string.Empty;
            panel.AppendLine($"<size=13>{ordering}{filtered}</size>");

            // Under Receive the chips on each row are the whole answer, so saying anything would be
            // noise. Under the other two they are not, and a player watching a chipless destination
            // read as green deserves to be told why rather than left to guess.
            string flow = FlowNote();
            if (flow != null)
            {
                panel.AppendLine($"<size=13><color=#B9A67A>{flow}</color></size>");
            }

            panel.AppendLine(Verdict(destination));
            panel.AppendLine();

            // A window onto the list rather than the whole thing: clamped so it never runs off
            // either end, and shifted to keep the highlight inside it.
            int first = Mathf.Clamp(_highlight - VisibleRows / 2, 0, Mathf.Max(0, Candidates.Count - VisibleRows));
            int last = Mathf.Min(first + VisibleRows, Candidates.Count);

            panel.AppendLine(first > 0 ? $"<size=13>{Translations.Format(Translations.SelectorMoreAbove, first)}</size>" : " ");

            for (int i = first; i < last; i++)
            {
                PortalRecord row = Candidates[i];
                float distance = Vector3.Distance(_sourcePosition, row.Position);
                // Name and distance on one line, chips indented beneath, so a row reads as a heading
                // and its detail rather than one long strip the eye has to parse.
                string line = $"{Describe(row)}  <size=13>{distance:F0}m</size>\n     {Chips(row)}";

                panel.AppendLine(i == _highlight
                    ? $"<color=#FFB726>» {line}</color>"
                    : $"<color=#C9C0AC>   {line}</color>");
            }

            panel.AppendLine(last < Candidates.Count ? $"<size=13>{Translations.Format(Translations.SelectorMoreBelow, Candidates.Count - last)}</size>" : " ");
            panel.AppendLine();
            // Confirm and cancel first. The footer sits at the bottom of a fixed-height text box, so
            // if anything is ever clipped it should be the line you can do without — and losing the
            // two keys that commit or escape a modal panel is the worst possible thing to lose.
            panel.AppendLine($"<size=13>[{Bound(SelectorKeys.Confirm)}] {Translations.Get(Translations.Confirm)}   " +
                             $"[{Bound(SelectorKeys.Cancel)}] {Translations.Get(Translations.Cancel)}</size>");
            panel.Append($"<size=13>[{Bound(SelectorKeys.Previous)} / {Bound(SelectorKeys.Next)}] " +
                         $"{Translations.Get(Translations.Change)}   " +
                         $"[{Bound(SelectorKeys.Sort)}] {Translations.Get(Translations.Sort)}   " +
                         $"[{Bound(SelectorKeys.Filter)}] {Translations.Get(Translations.Filter)}</size>");

            SetPanelText(panel.ToString());
        }

        /// <summary>
        /// Assigns the panel text, complaining loudly if it has grown past what a UI Text can draw.
        /// <para>
        /// Unity discards a text mesh that exceeds 65000 vertices, which shows up as a panel that is
        /// simply <em>blank</em> — no missing row, no error in the panel, nothing to suggest the text
        /// was ever built. That is a miserable thing to debug from a screenshot, and it has already
        /// happened once. The budget is roughly twenty vertices per drawn character with the outline
        /// on, so this warns well before the cliff rather than at it.
        /// </para>
        /// </summary>
        private static void SetPanelText(string text)
        {
            const int Budget = 2400;

            if (text.Length > Budget)
            {
                Jotunn.Logger.LogWarning(
                    $"Selector text is {text.Length} characters, past the {Budget} this panel can " +
                    "safely draw. Expect it to render blank. Shorten a row, or drop VisibleRows.");
            }

            _text.text = text;
        }

        /// <summary>
        /// The per-tier chips §5 asks for: granted tiers named, missing ones dashed.
        /// <para>
        /// A tier you are actually carrying something for and the destination lacks is drawn in the
        /// refusal colour, so scanning the list finds the portal that will turn you away without
        /// reading a word.
        /// </para>
        /// </summary>
        private static string Chips(PortalRecord portal)
        {
            var mask = (Clearance)portal.ClearanceMask;
            var chips = new StringBuilder("<size=12>");

            foreach (Clearance tier in ClearanceExtensions.Ladder)
            {
                bool granted = (mask & tier) == tier;
                bool needed = (_carrying & tier) == tier;

                // Markup is rationed here, and it is not fussiness: a UI Text with an outline costs
                // roughly twenty mesh vertices per character, and Unity throws away the whole mesh
                // past 65000 — which renders as an empty panel rather than as an error. One size tag
                // wraps the strip, and colour is spent only on the case that has to shout.
                if (granted)
                {
                    chips.Append(tier.Symbol());
                }
                else if (needed)
                {
                    chips.Append($"<color=#E06C4A>{tier.Symbol()}</color>");
                }
                else
                {
                    // Absence reads as absence without needing a colour to say so.
                    chips.Append("··");
                }

                chips.Append(' ');
            }

            return chips.ToString().TrimEnd() + "</size>";
        }

        /// <summary>
        /// One line on whether the highlighted destination will take what you are holding, and how
        /// many of the others would.
        /// </summary>
        private static string Verdict(PortalRecord destination)
        {
            if (_carrying == Clearance.None)
            {
                return $"<size=13>{Translations.Get(Translations.SelectorCarryingNothing)}</size>";
            }

            int accepting = Candidates.Count(Accepts);
            string tally = $"<size=13>{Translations.Format(Translations.SelectorTally, accepting, Candidates.Count)}</size>";

            return Accepts(destination)
                ? $"<size=13><color=#8FC97A>{Translations.Get(Translations.SelectorTakes)}</color></size>  {tally}"
                : $"<size=13><color=#E06C4A>{Translations.Get(Translations.SelectorRefuses)}</color></size>  {tally}";
        }

        /// <summary>
        /// One line explaining where clearance is being read from, or null under
        /// <c>MaterialFlow.Receive</c>, where each row's chips already say it.
        /// </summary>
        private static string FlowNote()
        {
            switch (StaveboundConfig.Flow?.Value ?? MaterialFlow.Both)
            {
                case MaterialFlow.Both:
                    return Translations.Get(Translations.SelectorFlowBoth);
                case MaterialFlow.Deliver:
                    return Translations.Get(Translations.SelectorFlowDeliver);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Whether this trip may carry what the player is holding — the same question the travel gate
        /// answers, asked through the same <see cref="ClearanceGate.EffectiveMask"/> so the list can
        /// never promise a trip the portal then refuses.
        /// </summary>
        private static bool Accepts(PortalRecord portal)
        {
            Clearance permitted = ClearanceGate.EffectiveMask(_sourceMask, (Clearance)portal.ClearanceMask);
            return (permitted & _carrying) == _carrying;
        }

        /// <summary>
        /// Which tiers the player is carrying something for.
        /// <para>
        /// Blocked items only — everything the game teleports happily needs no clearance, so a load of
        /// wood and food answers <see cref="Clearance.None"/> and every destination reads as fine.
        /// </para>
        /// </summary>
        private static Clearance CarriedTiers(Player player)
        {
            Clearance carried = Clearance.None;
            Inventory inventory = player?.GetInventory();

            if (inventory == null)
            {
                return carried;
            }

            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item?.m_shared == null || item.m_shared.m_teleportable)
                {
                    continue;
                }

                string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
                carried |= TierMap.RequiredFor(prefab);
            }

            return carried;
        }

        /// <summary>
        /// Reads the binding back out rather than hardcoding the prompt, so rebinding a key does not
        /// leave the panel telling you to press something else.
        /// </summary>
        private static string Bound(string button) => SelectorKeys.KeyLabel(button);

        /// <summary>
        /// Honours <c>HidePortalNames</c>, which is why nothing formats a portal name itself.
        /// </summary>
        private static string Describe(PortalRecord portal)
        {
            if (StaveboundConfig.HidePortalNames.Value)
            {
                return Translations.Format(Translations.PortalAt, portal.Position.x.ToString("F0"), portal.Position.z.ToString("F0"));
            }

            return string.IsNullOrEmpty(portal.Name) ? Translations.Get(Translations.UnnamedPortal) : portal.Name;
        }

        private static void BuildPanel()
        {
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
            }

            _panel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(-600f, -60f),
                width: 380f,
                height: 540f);

            GameObject text = GUIManager.Instance.CreateText(
                text: string.Empty,
                parent: _panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: Vector2.zero,
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: 16,
                color: GUIManager.Instance.ValheimOrange,
                outline: true,
                outlineColor: Color.black,
                width: 340f,
                height: 500f,
                addContentSizeFitter: false);

            _text = text.GetComponent<Text>();
            _text.alignment = TextAnchor.UpperLeft;
            _text.supportRichText = true;
        }

        // -- Input -------------------------------------------------------------------------------
        //
        // Every key is a registered, rebindable button rather than a raw read — see SelectorKeys for
        // why. One name covers both keyboard and gamepad, so the two inputs cannot drift apart.

        private static int Stepped()
        {
            if (SelectorKeys.Pressed(SelectorKeys.Next))
            {
                return 1;
            }

            return SelectorKeys.Pressed(SelectorKeys.Previous) ? -1 : 0;
        }

        private static bool Confirmed() => SelectorKeys.Pressed(SelectorKeys.Confirm);

        private static bool Cancelled() => SelectorKeys.Pressed(SelectorKeys.Cancel);
    }
}
