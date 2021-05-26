// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Harmony;
using Newtonsoft.Json;
using PeterHan.PLib;
using PeterHan.PLib.Options;
using PeterHan.PLib.UI;
using UnityEngine;

namespace ONIForcedExit
{
    [ModInfo("Forced Exit")]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class Options : POptions.SingletonOptions<Options>
    {
        [Option("Exit after (enable)", "When enabled, exit after a certain number of hours played.")]
        [JsonProperty]
        public bool ExitAfterMode { get; set; }

        [Option("Exit after (hours)", "Force your game to save and exit after this many hours.")]
        [Limit(0, 48)]
        [JsonProperty]
        public int ExitAfter { get; set; }

        [Option("Exit at (enable)", "When enabled, exit at a certain time of day.")]
        [JsonProperty]
        public bool ExitAtMode { get; set; }

        [Option("Exit at (hour of day)", "Force your game to save and exit at this time of day (0 is midnight).")]
        [Limit(0, 23)]
        [JsonProperty]
        public int ExitAt { get; set; }

        public enum ClockFormatType
        {
            [Option("System locale", "Use the clock format defined by your operating system's locale.")]
            FollowLocale,
            [Option("24 hour", "Use a 24 hour clock.")]
            TwentyFourHour,
            [Option("12 hour", "Use a 12 hour clock.")]
            TwelveHour,
        }

        [Option("Clock format")]
        [JsonProperty]
        public ClockFormatType ClockFormat { get; set; }

        [Option("In-game clock", "Display the current time in the top left status area.")]
        [JsonProperty]
        public bool IngameClock { get; set; }

        public Options()
        {
            ExitAfterMode = true;
            ExitAfter = 12;
            ExitAtMode = false;
            ExitAt = 0;
            ClockFormat = ClockFormatType.FollowLocale;
            IngameClock = false;
        }

        public string RenderTime(System.DateTime time) => ClockFormat switch
        {
            ClockFormatType.FollowLocale => time.ToShortTimeString(),
            ClockFormatType.TwentyFourHour => time.ToString("H:mm"),
            ClockFormatType.TwelveHour => time.ToString("h:mm tt"),
            _ => throw new System.ArgumentException(nameof(ClockFormat), "invalid enum value for ClockFormat")
        };
    }

    public class ForcedExit
    {
        public static System.DateTime GameStarted = System.DateTime.Now;

        public static void ExitNow(string message)
        {
            PUtil.LogDebug("Play time exceeded; game is being forcibly quit.");
            if (PauseScreen.Instance.IsActive())
            {
                PauseScreen.Instance.Deactivate();
                Game.Instance.StartDelayed(10, () => ExitDialog(message));
            }
            else
            {
                ExitDialog(message);
            }
        }

        public static void ExitDialog(string message)
        {
            Game.Instance.gameObject.SetActive(false);

            var dialog = new PDialog("Forced Exit")
            {
                Title = "Forced Exit",
                DialogBackColor = PUITuning.Colors.DialogDarkBackground,
                DialogClosed = (option) => OnConfirm(),
                Size = new Vector2(320.0f, 200.0f),
                MaxSize = new Vector2(800.0f, 600.0f),
                SortKey = 150.0f,
            }.AddButton(PDialog.DIALOG_KEY_CLOSE, STRINGS.UI.CONFIRMDIALOG.OK, null, PUITuning.Colors.ButtonPinkStyle);
            dialog.Body.Margin = new RectOffset(20, 20, 20, 20);
            dialog.Body.AddChild(new PLabel("ExitReason")
            {
                Text = message,
                TextStyle = PUITuning.Fonts.UILightStyle,
                Margin = new RectOffset(0, 0, 0, 20)
            });
            dialog.Body.AddChild(new PLabel("ExitReason2")
            {
                Text = "The game is now going to quit. Your game has been saved.",
                TextStyle = PUITuning.Fonts.UILightStyle
            });
            dialog.Show();
        }

        public static void OnConfirm() => App.Quit();

        public static void CheckForcedExit()
        {
            PUtil.LogDebug("Game was saved; checking forced exit.");
            if (Options.Instance.ExitAfterMode)
            {
                var exitTime = GameStarted.AddHours(Options.Instance.ExitAfter);
                if (System.DateTime.Now > exitTime)
                {
                    Game.Instance.StartDelayed(10, () => ExitNow($"You have played for {Options.Instance.ExitAfter} hours."));
                    return;
                }
            }
            if (Options.Instance.ExitAtMode)
            {
                if (System.DateTime.Now.Hour == Options.Instance.ExitAt)
                {
                    var currentTime = Options.Instance.RenderTime(System.DateTime.Now);
                    Game.Instance.StartDelayed(10, () => ExitNow($"It is now {currentTime}."));
                    return;
                }
            }
        }
    }

    public class Clock : PTextComponent
    {
        public static Clock? Instance = null;

        public static void Instantiate(MeterScreen parentScreen)
        {
            Instance = new Clock();
            PUIUtils.AddTo(Instance, parentScreen.gameObject);
        }

        public static void DestroyInstance() { Instance = null; }

        public Clock() : base("ClockDisplay")
        {
            TextAlignment = TextAnchor.MiddleLeft;
        }

        private LocText? text;
        public ToolTip? tooltip;

        public override GameObject Build()
        {
            var label = PUIElements.CreateUI(null, base.Name);
            text = PTextComponent.TextChildHelper(label, PUITuning.Fonts.UILightStyle, Options.Instance.RenderTime(System.DateTime.Now));
            tooltip = EntityTemplateExtensions.AddOrGet<ToolTip>(label);
            tooltip.OnToolTip = OnToolTip;
            label.SetActive(true);
            var layout = label.AddComponent<RelativeLayoutGroup>();
            layout.Margin = new RectOffset(10, 10, 8, 8);
            ArrangeComponent(layout, text.gameObject, base.TextAlignment);
            if (!DynamicSize) layout.LockLayout();
            layout.flexibleWidth = FlexSize.x;
            layout.flexibleHeight = FlexSize.y;
            DestroyLayoutIfPossible(label);
            InvokeRealize(label);
            return label;
        }

        public void Update()
        {
            if (text is not null)
            {
                text.text = Options.Instance.RenderTime(System.DateTime.Now);
            }
        }

        public string OnToolTip()
        {
            if (tooltip is not null)
            {
                tooltip.ClearMultiStringTooltip();

                var timePlayed = System.DateTime.Now - ForcedExit.GameStarted;
                if (Options.Instance.ExitAfterMode)
                {
                    var exitTime = ForcedExit.GameStarted.AddHours(Options.Instance.ExitAfter);
                    tooltip.AddMultiStringTooltip($"You have been playing for <color=#F0B310FF>{timePlayed.Hours}</color> of <color=#F0B310FF>{Options.Instance.ExitAfter}</color> hours.", PUITuning.Fonts.TextLightStyle);
                }
                else
                {
                    tooltip.AddMultiStringTooltip($"You have been playing for <color=#F0B310FF>{timePlayed.Hours}</color> hours.", PUITuning.Fonts.TextLightStyle);
                }
                if (Options.Instance.ExitAtMode)
                {
                    var exitTime = Options.Instance.RenderTime(System.DateTime.Today.AddHours(Options.Instance.ExitAt));
                    tooltip.AddMultiStringTooltip($"Your game will exit at <color=#F0B310FF>{exitTime}</color>.", PUITuning.Fonts.TextLightStyle);
                }
            }
            return "";
        }
    }

    public class Patches
    {
        public static class Mod_OnLoad
        {
            public static void OnLoad()
            {
                PUtil.InitLibrary();
                POptions.RegisterOptions(typeof(Options));

                foreach (var img in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    var name = img?.name?.ToLower();
                    if (name is not null)
                    {
                        if (name.Contains("schedule") || name.Contains("time") || name.Contains("clock")) PUtil.LogDebug($"Sprite: {name}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Game), "Save")]
        public static class Game_Save_Patch
        {
            public static void Postfix() => ForcedExit.CheckForcedExit();
        }

        [HarmonyPatch(typeof(MeterScreen), "OnPrefabInit")]
        public static class MeterScreen_OnPrefabInit_Patch
        {
            public static void Postfix(MeterScreen __instance)
            {
                if (Options.Instance.IngameClock)
                {
                    Clock.Instantiate(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(MeterScreen), "DestroyInstance")]
        public static class MeterScreen_DestroyInstance_Patch
        {
            public static void Postfix()
            {
                if (Options.Instance.IngameClock)
                {
                    Clock.DestroyInstance();
                }
            }
        }

        [HarmonyPatch(typeof(MeterScreen), "Refresh")]
        public static class MeterScreen_Refresh_Patch
        {
            public static void Postfix()
            {
                if (Options.Instance.IngameClock)
                {
                    Clock.Instance?.Update();
                }
            }
        }
    }
}
