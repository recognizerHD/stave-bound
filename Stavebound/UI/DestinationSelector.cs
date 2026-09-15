using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stavebound.Config;
using Stavebound.Portals;
using Stavebound.Tiers;
using Stavebound.Travel;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
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
    /// That every view comes almost free is the payoff for making selection a <em>highlight</em>
    /// that everything moves — the keys, the stick, the wheel, the previous and next buttons, a hover
    /// over a row — rather than a focus each control owns. Another view of one highlight costs a
    /// rendering loop; independent selections would have cost a reconciliation problem. It is also
    /// why the gamepad works without Unity UI navigation: every control has navigation switched off
    /// and every click hands focus straight back, so nothing holds focus and nothing has to be told
    /// where it goes next.
    /// </para>
    /// <para>
    /// Keys and mouse confirm differently, on purpose. The keys, the stick and the wheel only move the
    /// highlight, and the confirm key re-aims: stepping through a list is browsing, and browsing should
    /// never commit. A mouse click is already a choice, so clicking a row, a portal's pin on the map, or
    /// an entry in the dropdown re-aims at once. Hovering stays a preview and never commits. Each mouse
    /// behaviour can be switched off per player, under <c>8 - Selector</c> in the config.
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
        /// Purely a layout number now, sized to the panel. It used to be a vertex budget as well: the
        /// whole panel was one outlined Text, and Unity discards a text mesh past 65000 vertices,
        /// which rendered as a blank panel. Each row is its own pair of Texts now, so that cliff is
        /// far out of reach.
        /// </para>
        /// </summary>
        private const int VisibleRows = 9;

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
        private static Text _title;
        private static Text _status;
        private static Dropdown _picker;
        private static Text _moreAbove;
        private static Text _moreBelow;
        private static readonly List<RowView> Rows = new List<RowView>();

        /// <summary>Controls that mean nothing with an empty list, greyed out when the filter empties it.</summary>
        private static readonly List<Selectable> NeedsCandidates = new List<Selectable>();

        /// <summary>
        /// Set whenever the candidate list is rebuilt, so the dropdown's options are regenerated only
        /// when the list itself changed rather than on every step through it.
        /// </summary>
        private static bool _pickerStale;

        /// <summary>
        /// The wheel as the game read it this frame, captured on its way to the map by
        /// <c>SelectorWheelPatches</c>, and the frame it was read on.
        /// </summary>
        private static float _wheel;
        private static int _wheelFrame = -1;
        private static float _lastWheelStep;

        /// <summary>
        /// The shortest gap between two steps from the wheel. A notched wheel sends one reading per
        /// click; a trackpad sends a stream of small ones, and without a floor a single swipe would
        /// race the highlight to the end of the list. Being a time rather than an amount also means
        /// it does not depend on how the game scales the reading, which it does.
        /// </summary>
        private const float WheelInterval = 0.06f;

        /// <summary>
        /// The portal nearest the player's bed — their home — drawn in its own colour in the list and on
        /// the map, and that portal's pin while the selector is showing it.
        /// </summary>
        private static long _homePid = PortalTarget.NoPid;
        private static Minimap.PinData _homePin;
        private static readonly Color HomeColour = new Color(0.50f, 0.83f, 1f);
        private const string HomeHex = "#7FD4FF";

        /// <summary>The row under the pointer, if any, and whether the map is showing it instead of the highlight.</summary>
        private static RowView _hovered;
        private static bool _previewing;
        private static bool _updateSeen;

        /// <summary>
        /// Unity's legacy Dropdown has no public "is open" flag. The full-screen blocker it creates on
        /// Show and destroys on Hide is the one reliable tell, and it lives in UnityEngine.UI rather
        /// than the game assembly, so reading it is not the publicised-member hazard §12 describes.
        /// </summary>
        private static readonly AccessTools.FieldRef<Dropdown, GameObject> PickerBlocker =
            AccessTools.FieldRefAccess<Dropdown, GameObject>("m_Blocker");

        /// <summary>One clickable line of the list, reused as the window scrolls.</summary>
        internal sealed class RowView
        {
            internal GameObject Root;
            internal Button Button;
            internal Text Name;
            internal Text Chips;

            /// <summary>Which candidate this row is showing right now, or -1 while hidden.</summary>
            internal int Candidate = -1;
        }

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
            _homePid = NearestToHome();
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

            TintPins();

            if (PickerIsOpen())
            {
                // The dropdown's own list has the keyboard while it is open: arrows move within it and
                // Enter picks. Acting on the same keys here too would move a highlight the player
                // cannot see behind the list, so the selector's keys wait. Cancel folds the list.
                if (Cancelled())
                {
                    _picker.Hide();
                }

                return;
            }

            // The dropdown reselects itself as its list closes, and a focused control turns Enter and
            // the arrow keys into UI navigation. Hand focus back whenever one of ours is holding it.
            GameObject focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (focused != null && _panel != null && focused.transform.IsChildOf(_panel.transform))
            {
                Unfocus();
            }

            // A hover preview ends when the pointer leaves the list, and the map goes back to the
            // highlight. Checked here rather than in the exit handler, so sliding from one row to the
            // next — an exit and an enter in the same event pass — never bounces the map in between.
            if (_previewing && _hovered == null)
            {
                _previewing = false;
                if (Candidates.Count > 0)
                {
                    Minimap.instance?.ShowPointOnMap(Candidates[_highlight].Position);
                }
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
                ToggleFilter();
                return;
            }

            if (SelectorKeys.Pressed(SelectorKeys.Sort))
            {
                ToggleSort();
                return;
            }

            int step = Stepped();
            if (step != 0)
            {
                Step(step);
                return;
            }

            ScrollFromWheel();
        }

        /// <summary>
        /// Whether the wheel belongs to the selector rather than the map this frame: while the pointer
        /// is over the panel, or while the dropdown's list is open and scrolling itself.
        /// </summary>
        internal static bool OwnsWheel() =>
            IsOpen && _panel != null &&
            (PickerIsOpen() || (StaveboundConfig.WheelScrollsList.Value && PointerOverPanel()));

        internal static void ReceiveWheel(float delta)
        {
            _wheel = delta;
            _wheelFrame = Time.frameCount;
        }

        /// <summary>
        /// One step per notch, towards the top of the list for a wheel rolled away from you.
        /// <para>
        /// Clamped at either end, where the keys wrap. A wheel is rarely moved one notch at a time, and
        /// rolling past the last row to land on the first is disorienting in a way that pressing a key
        /// once too often is not.
        /// </para>
        /// </summary>
        private static void ScrollFromWheel()
        {
            if (!StaveboundConfig.WheelScrollsList.Value ||
                _wheelFrame != Time.frameCount || _wheel == 0f || Candidates.Count == 0)
            {
                return;
            }

            if (Time.unscaledTime - _lastWheelStep < WheelInterval || !PointerOverPanel())
            {
                return;
            }

            _lastWheelStep = Time.unscaledTime;

            int next = Mathf.Clamp(_highlight + (_wheel > 0f ? -1 : 1), 0, Candidates.Count - 1);
            if (next != _highlight)
            {
                _highlight = next;
                ShowHighlight();
            }
        }

        private static bool PointerOverPanel()
        {
            if (_panel == null)
            {
                return false;
            }

            Canvas canvas = _panel.GetComponentInParent<Canvas>();
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            Camera camera = root == null || root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

            // The game's own pointer, not UnityEngine.Input: 1.0 reads input through the Input System,
            // and the map's own click handler asks ZInput for the pointer for exactly that reason.
            return RectTransformUtility.RectangleContainsScreenPoint(
                _panel.GetComponent<RectTransform>(), ZInput.pointerPosition, camera);
        }

        /// <summary>
        /// Pans the map to the row under the pointer without moving the highlight.
        /// <para>
        /// A preview, deliberately not a selection. Hovering is how a player looks, and looking should
        /// not change what confirming would do; the map returns to the highlight when the pointer
        /// leaves the list (see <see cref="Update"/>).
        /// </para>
        /// </summary>
        internal static void HoverEntered(RowView row)
        {
            if (!IsOpen || !StaveboundConfig.HoverPreviewsOnMap.Value ||
                row == null || row.Candidate < 0 || row.Candidate >= Candidates.Count)
            {
                return;
            }

            _hovered = row;
            _previewing = true;
            Minimap.instance?.ShowPointOnMap(Candidates[row.Candidate].Position);
        }

        internal static void HoverLeft(RowView row)
        {
            if (_hovered == row)
            {
                _hovered = null;
            }
        }

        /// <summary>Moves the highlight. Shared by the keys and the previous/next buttons.</summary>
        private static void Step(int step)
        {
            if (Candidates.Count == 0)
            {
                // Reachable with the cargo filter emptying the list. The modulo below would divide by
                // zero, and it always could have from the keys alone.
                return;
            }

            // Wraps, because a list you can fall off the end of is worse than one you can loop.
            _highlight = (_highlight + step + Candidates.Count) % Candidates.Count;
            ShowHighlight();
        }

        private static void ToggleFilter()
        {
            _onlyWhatAcceptsMyCargo = !_onlyWhatAcceptsMyCargo;
            Rebuild(Held());

            // Pins follow the filter. Clicking a pin re-aims now, and a pin for a destination the list
            // has filtered out would be a way to choose something the panel says is not on offer.
            AddPins();
            ShowHighlight();
        }

        private static void ToggleSort()
        {
            _order = _order == SortOrder.Distance ? SortOrder.Name : SortOrder.Distance;
            Rebuild(Held());
            ShowHighlight();
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
            _pickerStale = true;

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
        /// A click on the map re-aims at the destination whose pin was clicked, and does nothing on a
        /// miss.
        /// <para>
        /// "Clicked" means within <paramref name="radius"/>, which the caller takes from the map's own
        /// pin-click distance, so a pin is as easy to hit here as anywhere else on the map. A miss
        /// deliberately does not fall back to the nearest destination: every click is nearest to
        /// <em>something</em>, and a click in open sea should not re-aim a portal at whatever happens to
        /// lie a thousand metres away.
        /// </para>
        /// </summary>
        internal static void SelectNear(Vector3 worldPoint, float radius)
        {
            if (!IsOpen || Candidates.Count == 0)
            {
                return;
            }

            int hit = -1;
            float best = radius * radius;

            for (int i = 0; i < Candidates.Count; i++)
            {
                Vector3 offset = Candidates[i].Position - worldPoint;
                offset.y = 0f;

                float distance = offset.sqrMagnitude;
                if (distance <= best)
                {
                    best = distance;
                    hit = i;
                }
            }

            if (hit < 0)
            {
                return;
            }

            _highlight = hit;

            if (StaveboundConfig.MapClickPicksPortal.Value)
            {
                Commit();
            }
            else
            {
                ShowHighlight();
            }
        }

        private static void Commit()
        {
            if (Candidates.Count == 0)
            {
                // The Confirm button, or the key, with the cargo filter emptying the list. There is
                // nothing to aim at, and indexing the empty list would throw.
                return;
            }

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
                // Destroying the panel disables the dropdown, and Dropdown.OnDisable tears down an
                // open list and its full-screen click blocker — so closing mid-pick strands neither.
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
            }

            _title = null;
            _status = null;
            _picker = null;
            _moreAbove = null;
            _moreBelow = null;
            Rows.Clear();
            NeedsCandidates.Clear();
            _hovered = null;
            _previewing = false;
            _homePid = PortalTarget.NoPid;

            Candidates.Clear();

            // m_mode, the same flag Update watches, and for the same reason: SetMapMode toggles
            // m_largeRoot and leaves m_mapLarge alone, so checking m_mapLarge could leave the map open
            // behind a selector that had already closed — after a click on a pin, most visibly.
            if (Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large)
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

                if (portal.Pid == _homePid)
                {
                    _homePin = pin;
                }
            }
        }

        /// <summary>
        /// Colours the selector's own pins: the home portal in its colour, every other destination
        /// orange.
        /// <para>
        /// Every frame, because the map repaints every pin's icon colour each frame in <c>UpdatePins</c>,
        /// which runs earlier in the same <c>Minimap.Update</c> this is driven from — a colour laid on
        /// once is gone before it is ever drawn. Only pins this selector added are touched; the player's
        /// own pins, which use the same icon, keep the map's colours, and the orange is what tells the
        /// two apart.
        /// </para>
        /// </summary>
        private static void TintPins()
        {
            bool orange = StaveboundConfig.ColourPortalPins.Value;
            Color pinColour = GUIManager.Instance.ValheimOrange;

            foreach (Minimap.PinData pin in Pins)
            {
                if (pin?.m_iconElement == null)
                {
                    // Not drawn yet, or scrolled out of the map's view and released.
                    continue;
                }

                if (pin == _homePin)
                {
                    pin.m_iconElement.color = HomeColour;
                }
                else if (orange)
                {
                    pin.m_iconElement.color = pinColour;
                }
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
            _homePin = null;
        }

        private static void ShowHighlight()
        {
            if (_panel == null)
            {
                return;
            }

            RefreshPicker();

            bool any = Candidates.Count > 0;
            foreach (Selectable control in NeedsCandidates)
            {
                control.interactable = any;
            }

            _title.text = Translations.Format(Translations.SelectorTitle, _sourceName);

            if (!any)
            {
                // Only reachable with the cargo filter on: there are destinations, just none that
                // would take what you are holding. Saying so beats an empty box, and the filter
                // button stays live so the way back out is one click.
                _status.text = $"<color=#E06C4A>{Translations.Get(Translations.SelectorEmpty)}</color>";
                _moreAbove.text = string.Empty;
                _moreBelow.text = string.Empty;

                foreach (RowView row in Rows)
                {
                    row.Root.SetActive(false);
                    row.Candidate = -1;
                }

                _hovered = null;

                return;
            }

            PortalRecord destination = Candidates[_highlight];

            // Only on change, so the map still pans under the player's own hand between steps.
            Minimap.instance?.ShowPointOnMap(destination.Position);

            string ordering = Translations.Get(_order == SortOrder.Distance
                ? Translations.SelectorByDistance
                : Translations.SelectorByName);
            string filtered = _onlyWhatAcceptsMyCargo ? Translations.Get(Translations.SelectorFiltered) : string.Empty;

            var status = new StringBuilder();
            status.AppendLine($"{ordering}{filtered}");

            // Under Receive the chips on each row are the whole answer, so saying anything would be
            // noise. Under the other two they are not, and a player watching a chipless destination
            // read as green deserves to be told why rather than left to guess.
            string flow = FlowNote();
            if (flow != null)
            {
                status.AppendLine($"<color=#B9A67A>{flow}</color>");
            }

            status.Append(Verdict(destination));
            _status.text = status.ToString();

            // A window onto the list rather than the whole thing: clamped so it never runs off
            // either end, and shifted to keep the highlight inside it.
            int first = Mathf.Clamp(_highlight - VisibleRows / 2, 0, Mathf.Max(0, Candidates.Count - VisibleRows));
            int last = Mathf.Min(first + VisibleRows, Candidates.Count);

            _moreAbove.text = first > 0 ? Translations.Format(Translations.SelectorMoreAbove, first) : string.Empty;
            _moreBelow.text = last < Candidates.Count
                ? Translations.Format(Translations.SelectorMoreBelow, Candidates.Count - last)
                : string.Empty;

            for (int slot = 0; slot < Rows.Count; slot++)
            {
                RowView row = Rows[slot];
                int index = first + slot;

                if (index >= last)
                {
                    row.Root.SetActive(false);
                    row.Candidate = -1;
                    if (_hovered == row)
                    {
                        _hovered = null;
                    }

                    continue;
                }

                PortalRecord portal = Candidates[index];
                bool current = index == _highlight;
                string label = $"{Describe(portal)}  <size=12>{Vector3.Distance(_sourcePosition, portal.Position):F0}m</size>";

                row.Candidate = index;
                row.Root.SetActive(true);
                // The home portal keeps its colour even when highlighted, with the marker and the band
                // saying which row is current — otherwise it would stop being findable the moment it
                // was the one you were on.
                string colour = portal.Pid == _homePid ? HomeHex : current ? "#FFB726" : "#C9C0AC";
                row.Name.text = current
                    ? $"<color=#FFB726>» </color><color={colour}>{label}</color>"
                    : $"<color={colour}>   {label}</color>";
                row.Chips.text = Chips(portal);
                row.Button.colors = RowColours(current);
            }
        }

        /// <summary>
        /// Keeps the dropdown listing the same destinations, in the same order, as the rows beneath
        /// it — regenerated only when the list itself changed, and otherwise just moved to the
        /// highlight.
        /// </summary>
        private static void RefreshPicker()
        {
            if (_picker == null)
            {
                return;
            }

            if (_pickerStale)
            {
                _pickerStale = false;
                _picker.ClearOptions();

                var options = new List<string>(Candidates.Count);
                foreach (PortalRecord portal in Candidates)
                {
                    // Plain text: an option is drawn by the template's own Text, which is not set up
                    // for rich text, so markup here would be printed rather than obeyed.
                    options.Add($"{Describe(portal)}   {Vector3.Distance(_sourcePosition, portal.Position):F0}m");
                }

                if (options.Count == 0)
                {
                    options.Add(Translations.Get(Translations.SelectorEmpty));
                }

                _picker.AddOptions(options);
            }

            // Without notify: this is the dropdown catching up with a highlight that moved some other
            // way, not the player choosing, and firing the callback would loop straight back here.
            _picker.SetValueWithoutNotify(Candidates.Count == 0 ? 0 : _highlight);
        }

        /// <summary>
        /// A row's background through each state: a faint band behind the highlight, a fainter one
        /// under the pointer, and nothing otherwise.
        /// </summary>
        private static ColorBlock RowColours(bool current)
        {
            var clear = new Color(1f, 1f, 1f, 0f);
            var band = new Color(1f, 0.72f, 0.15f, 0.16f);

            return new ColorBlock
            {
                normalColor = current ? band : clear,
                highlightedColor = current ? new Color(1f, 0.72f, 0.15f, 0.26f) : new Color(1f, 1f, 1f, 0.08f),
                pressedColor = new Color(1f, 0.72f, 0.15f, 0.32f),
                selectedColor = current ? band : clear,
                disabledColor = clear,
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }

        private static bool PickerIsOpen() => _picker != null && PickerBlocker(_picker) != null;

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
        /// The portal nearest the player's bed — the spawn point a bed sets, where they respawn — or none
        /// when they have not set one in this world, or have switched the colouring off.
        /// <para>
        /// Deliberately not the world's starting spawn. "Home" is where a player chose to sleep, and the
        /// start temple is somewhere every character leaves on day one. The bed is kept in the local
        /// player profile, so each player sees their own home portal and nobody else's.
        /// </para>
        /// <para>
        /// Every portal counts, including the one being re-aimed: if that is the nearest, no destination
        /// is coloured, which is simply true.
        /// </para>
        /// </summary>
        private static long NearestToHome()
        {
            PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;

            if (!StaveboundConfig.ColourHomePortal.Value || profile == null || !profile.HaveCustomSpawnPoint())
            {
                return PortalTarget.NoPid;
            }

            Vector3 home = profile.GetCustomSpawnPoint();
            long nearest = PortalTarget.NoPid;
            float best = float.MaxValue;

            foreach (PortalRecord portal in PortalRegistry.All)
            {
                Vector3 offset = portal.Position - home;
                offset.y = 0f;

                float distance = offset.sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = portal.Pid;
                }
            }

            return nearest;
        }

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

        // The panel's layout in its own units, top to bottom. Kept in one place so that moving anything
        // means reading one column of numbers rather than hunting offsets through the builder.
        private const float PanelWidth = 440f;
        private const float PanelHeight = 530f;
        private const float Inner = 400f;
        private const float RowHeight = 28f;
        private const float ChipsWidth = 132f;

        private static void BuildPanel()
        {
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
            }

            Rows.Clear();
            NeedsCandidates.Clear();

            // Anchored to the left edge rather than offset from the centre, so the panel sits beside
            // the map at every resolution instead of sliding off the side of a narrow screen.
            _panel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0f, 0.5f),
                anchorMax: new Vector2(0f, 0.5f),
                position: new Vector2(40f + PanelWidth / 2f, -20f),
                width: PanelWidth,
                height: PanelHeight);

            float top = 18f;

            _title = AddText(top, 24f, 16, TextAnchor.MiddleLeft);
            top += 28f;

            GameObject picker = GUIManager.Instance.CreateDropDown(
                _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, 14, Inner, 32f);
            Place(picker.GetComponent<RectTransform>(), top, 32f, Inner);
            _picker = picker.GetComponent<Dropdown>();
            _picker.navigation = new Navigation { mode = Navigation.Mode.None };
            _picker.onValueChanged.AddListener(OnPicked);
            _pickerStale = true;
            NeedsCandidates.Add(_picker);
            top += 38f;

            _status = AddText(top, 56f, 13, TextAnchor.UpperLeft);
            top += 58f;

            _moreAbove = AddText(top, 18f, 12, TextAnchor.MiddleLeft);
            top += 18f;

            for (int slot = 0; slot < VisibleRows; slot++)
            {
                Rows.Add(AddRow(top, slot));
                top += RowHeight;
            }

            _moreBelow = AddText(top, 18f, 12, TextAnchor.MiddleLeft);
            top += 24f;

            // What the footer used to only describe, now pressable. Each label still names its key: a
            // button that hides its binding teaches the mouse and nothing else.
            float quarter = (Inner - 12f) / 4f;
            float x = -Inner / 2f + quarter / 2f;
            NeedsCandidates.Add(AddButton($"[{Bound(SelectorKeys.Previous)}]", top, 30f, quarter, x, () => Step(-1)));
            NeedsCandidates.Add(AddButton($"[{Bound(SelectorKeys.Next)}]", top, 30f, quarter, x + (quarter + 4f), () => Step(1)));
            AddButton($"[{Bound(SelectorKeys.Sort)}] {Translations.Get(Translations.Sort)}", top, 30f, quarter, x + 2f * (quarter + 4f), ToggleSort);
            AddButton($"[{Bound(SelectorKeys.Filter)}] {Translations.Get(Translations.Filter)}", top, 30f, quarter, x + 3f * (quarter + 4f), ToggleFilter);
            top += 36f;

            float half = (Inner - 6f) / 2f;
            NeedsCandidates.Add(AddButton($"[{Bound(SelectorKeys.Confirm)}] {Translations.Get(Translations.Confirm)}", top, 34f, half, -half / 2f - 3f, Commit));
            AddButton($"[{Bound(SelectorKeys.Cancel)}] {Translations.Get(Translations.Cancel)}", top, 34f, half, half / 2f + 3f, Close);
        }

        /// <summary>Pins an element to the panel's top edge, <paramref name="top"/> units down.</summary>
        private static void Place(RectTransform rect, float top, float height, float width, float x = 0f)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -top);
        }

        private static Text AddText(float top, float height, int size, TextAnchor alignment)
        {
            Text text = CreateLabel(_panel.transform, size, alignment);
            Place(text.rectTransform, top, height, Inner);
            return text;
        }

        private static Text CreateLabel(Transform parent, int size, TextAnchor alignment)
        {
            GameObject go = GUIManager.Instance.CreateText(
                text: string.Empty,
                parent: parent,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: Vector2.zero,
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: size,
                color: GUIManager.Instance.ValheimOrange,
                outline: true,
                outlineColor: Color.black,
                width: Inner,
                height: RowHeight,
                addContentSizeFitter: false);

            var text = go.GetComponent<Text>();
            text.alignment = alignment;
            text.supportRichText = true;

            // Clicks belong to the row or button underneath, never to the words drawn on it.
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// One list row: an invisible clickable band, with the name on the left and the clearance
        /// chips right-aligned against the far edge.
        /// </summary>
        private static RowView AddRow(float top, int slot)
        {
            // Built inactive, so the button's first colour transition happens on enable with its
            // configuration already in place — otherwise a white band flashes for a frame.
            var root = new GameObject($"StaveboundRow{slot}", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(_panel.transform, false);
            Place((RectTransform)root.transform, top, RowHeight, Inner);

            // Opaque white, tinted to nothing: invisible, still a raycast target, and a colour the
            // button's tint multiplies into the band drawn behind the highlight.
            var band = root.AddComponent<Image>();
            band.color = Color.white;

            var view = new RowView { Root = root, Button = root.AddComponent<Button>() };
            view.Button.targetGraphic = band;
            view.Button.transition = Selectable.Transition.ColorTint;
            view.Button.colors = RowColours(current: false);
            view.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            view.Button.onClick.AddListener(() =>
            {
                OnRowClicked(view);
                Unfocus();
            });

            root.AddComponent<SelectorRowHover>().Row = view;

            view.Name = CreateLabel(root.transform, 15, TextAnchor.MiddleLeft);
            RectTransform name = view.Name.rectTransform;
            name.anchorMin = Vector2.zero;
            name.anchorMax = Vector2.one;
            name.pivot = new Vector2(0f, 0.5f);
            name.offsetMin = new Vector2(6f, 0f);
            name.offsetMax = new Vector2(-(ChipsWidth + 8f), 0f);

            // Wrap, then truncate vertically: a name too long for its column loses its tail rather
            // than spilling across the chips.
            view.Name.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.Name.verticalOverflow = VerticalWrapMode.Truncate;

            view.Chips = CreateLabel(root.transform, 12, TextAnchor.MiddleRight);
            RectTransform chips = view.Chips.rectTransform;
            chips.anchorMin = new Vector2(1f, 0f);
            chips.anchorMax = new Vector2(1f, 1f);
            chips.pivot = new Vector2(1f, 0.5f);
            chips.sizeDelta = new Vector2(ChipsWidth, 0f);
            chips.anchoredPosition = new Vector2(-6f, 0f);

            return view;
        }

        private static Button AddButton(string label, float top, float height, float width, float x, Action action)
        {
            GameObject go = GUIManager.Instance.CreateButton(
                label, _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, width, height);
            Place(go.GetComponent<RectTransform>(), top, height, width, x);

            // Shrink-to-fit rather than a fixed size: "[LeftArrow]" and a translated label have to
            // share a quarter of the panel's width, and clipping a key name would defeat showing it.
            var text = go.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 9;
                text.resizeTextMaxSize = 14;
            }

            var button = go.GetComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                action();
                Unfocus();
            });

            return button;
        }

        // -- Input -------------------------------------------------------------------------------
        //
        // Every key is a registered, rebindable button rather than a raw read — see SelectorKeys for
        // why. One name covers both keyboard and gamepad, so the two inputs cannot drift apart.

        private static void OnRowClicked(RowView row)
        {
            if (row.Candidate < 0 || row.Candidate >= Candidates.Count)
            {
                return;
            }

            // A click is a choice, so it re-aims at once — unless the player has asked for clicks to
            // highlight only. See the class note on keys versus mouse.
            _highlight = row.Candidate;

            if (StaveboundConfig.ClickPicksPortal.Value)
            {
                Commit();
            }
            else
            {
                ShowHighlight();
            }
        }

        private static void OnPicked(int index)
        {
            if (index < 0 || index >= Candidates.Count)
            {
                return;
            }

            // Picking from the dropdown is a choice too, whether it was clicked or chosen with Enter in its
            // open list — it follows the same setting as a click on a row.
            _highlight = index;

            if (StaveboundConfig.ClickPicksPortal.Value)
            {
                Commit();
            }
            else
            {
                ShowHighlight();
                Unfocus();
            }
        }

        /// <summary>
        /// Hands focus back to nobody after a click.
        /// <para>
        /// A clicked control keeps EventSystem focus by default, and a focused Selectable is what turns
        /// Enter, Space and a gamepad's submit into a second click and the arrow keys into UI
        /// navigation. The selector is built on nothing holding focus, so every click gives it back.
        /// </para>
        /// </summary>
        private static void Unfocus()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

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

    /// <summary>
    /// Reports the pointer entering and leaving one row of the selector's list. A component of its own
    /// rather than an <c>EventTrigger</c>, which implements every pointer interface and would quietly
    /// sit in the path of events the row does not care about.
    /// </summary>
    internal sealed class SelectorRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal DestinationSelector.RowView Row;

        public void OnPointerEnter(PointerEventData eventData) => DestinationSelector.HoverEntered(Row);

        public void OnPointerExit(PointerEventData eventData) => DestinationSelector.HoverLeft(Row);
    }
}
