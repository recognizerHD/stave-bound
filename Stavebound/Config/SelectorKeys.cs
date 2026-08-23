using System.Collections.Generic;
using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Managers;
using UnityEngine;

namespace Stavebound.Config
{
    /// <summary>
    /// The destination selector's own keys, registered with the game's input system rather than read
    /// raw.
    /// <para>
    /// This started as a bug: Enter confirmed a destination <em>and</em> opened the chat window,
    /// because <c>Chat.Update</c> reads the same press and nothing we did from outside the input
    /// system reliably took it away first. Reaching for a quieter key would have moved the problem
    /// rather than fixed it — the next collision would land on whoever's mod or keyboard layout
    /// disagreed with our choice.
    /// </para>
    /// <para>
    /// Registered buttons fix it properly. <see cref="ButtonConfig.BlockOtherInputs"/> makes the game
    /// suppress competing readers while ours is pressed, every key is config-backed so it can be
    /// rebound, and each carries a gamepad button so the pad is a first-class binding rather than a
    /// translation layer — which is what DESIGN.md §5 means by gamepad support from the first commit.
    /// </para>
    /// </summary>
    internal static class SelectorKeys
    {
        private const string Section = "5 - Selector keys";

        /// <summary>
        /// The bound key per button, kept so the selector can label its own prompts.
        /// <para>
        /// <c>ZInput.GetBoundKeyString</c> looked like the right way to do that and returns an empty
        /// string for these, leaving the panel advertising "[stave_confirm]" as though it were a
        /// key you could press. Our own config entry is the authority on what the key is; asking the
        /// game to tell us what we just told it was the mistake.
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, ConfigEntry<KeyCode>> BoundKeys =
            new Dictionary<string, ConfigEntry<KeyCode>>();

        /// <summary>A printable name for a button's current key, for on-screen prompts.</summary>
        internal static string KeyLabel(string button)
        {
            return BoundKeys.TryGetValue(button, out ConfigEntry<KeyCode> entry)
                ? entry.Value.ToString()
                : button;
        }

        /// <summary>
        /// Was this button just pressed, by either route?
        /// <para>
        /// The keyboard is read straight from the configured <see cref="KeyCode"/> and the gamepad
        /// through the registered button. Going through <c>ZInput</c> for both would be tidier and
        /// was tried: the buttons register without complaint and then never fire, and an input path
        /// that cannot be observed failing is not one to stake the only exit from a modal panel on.
        /// The registration still earns its keep for the pad, where there is no equivalent of reading
        /// a raw key.
        /// </para>
        /// </summary>
        internal static bool Pressed(string button)
        {
            if (BoundKeys.TryGetValue(button, out ConfigEntry<KeyCode> entry) &&
                entry.Value != KeyCode.None && Input.GetKeyDown(entry.Value))
            {
                return true;
            }

            return ZInput.GetButtonDown(button);
        }

        internal const string Confirm = "stave_confirm";
        internal const string Cancel = "stave_cancel";
        internal const string Next = "stave_next";
        internal const string Previous = "stave_previous";
        internal const string Sort = "stave_sort";
        internal const string Filter = "stave_filter";

        internal static void Bind(ConfigFile config)
        {
            // P for portal. Enter was the obvious choice and the wrong one: it is the chat key, and
            // a confirm that also opens a chat window is the kind of default that makes a mod feel
            // broken before anyone reads its config.
            Register(config, Confirm, "Confirm the highlighted destination.",
                KeyCode.P, InputManager.GamepadButton.ButtonSouth);

            Register(config, Cancel, "Close the selector without changing anything.",
                KeyCode.Escape, InputManager.GamepadButton.ButtonEast);

            Register(config, Next, "Highlight the next destination.",
                KeyCode.RightArrow, InputManager.GamepadButton.RightShoulder);

            Register(config, Previous, "Highlight the previous destination.",
                KeyCode.LeftArrow, InputManager.GamepadButton.LeftShoulder);

            // O rather than Tab: Tab opens the inventory, and picking a key with no vanilla binding
            // avoids relying on input blocking to save us from a collision we chose.
            Register(config, Sort, "Cycle how the destination list is ordered.",
                KeyCode.O, InputManager.GamepadButton.ButtonWest);

            Register(config, Filter, "Show only destinations that will accept what you are carrying.",
                KeyCode.K, InputManager.GamepadButton.ButtonNorth);
        }

        private static void Register(ConfigFile config, string name, string description,
            KeyCode defaultKey, InputManager.GamepadButton defaultPad)
        {
            ConfigEntry<KeyCode> key = config.Bind(Section, name, defaultKey,
                new ConfigDescription($"{description} Local to you."));

            ConfigEntry<InputManager.GamepadButton> pad = config.Bind(Section, $"{name}_gamepad", defaultPad,
                new ConfigDescription($"{description} Gamepad. Local to you."));

            BoundKeys[name] = key;

            InputManager.Instance.AddButton(BuildInfo.Guid, new ButtonConfig
            {
                Name = name,

                // Key and GamepadButton are the plain defaults; Config and GamepadConfig override
                // them with the player's choice. Setting both means the selector still has working
                // keys if the config file is missing, unreadable, or has yet to be written — it is
                // never left with nothing bound.
                Key = defaultKey,
                GamepadButton = defaultPad,
                Config = key,
                GamepadConfig = pad,

                // The selector draws a Jotunn panel and runs over the map, so without these the
                // button is treated as inactive exactly when we need it.
                ActiveInGUI = true,
                ActiveInCustomGUI = true,

                // The whole point: while this is pressed, the game's own readers do not see it.
                BlockOtherInputs = true,
            });
        }
    }
}
