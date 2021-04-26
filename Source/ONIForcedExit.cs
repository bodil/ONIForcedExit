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

        public Options()
        {
            ExitAfterMode = true;
            ExitAfter = 12;
            ExitAtMode = false;
            ExitAt = 0;
        }
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

            // var dialog = (ConfirmDialogScreen)GameScreenManager.Instance.StartScreen(ScreenPrefabs.Instance.ConfirmDialogScreen.gameObject, Game.Instance.transform.parent.gameObject, GameScreenManager.UIRenderTarget.ScreenSpaceOverlay);
            // dialog.PopupConfirmDialog(message, new System.Action(OnConfirm), new System.Action(OnConfirm), null, null, "Forced Exit", "Exit", "Exit");

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
                    Game.Instance.StartDelayed(10, () => ExitNow($"It is now {System.DateTime.Now.ToShortTimeString()}."));
                    return;
                }
            }
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
            }
        }

        [HarmonyPatch(typeof(SaveLoader), "Save")]
        [HarmonyPatch(new[] { typeof(string), typeof(bool), typeof(bool) })]
        public static class SaveLoader_Save_Patch
        {
            public static void Postfix() => ForcedExit.CheckForcedExit();
        }
    }
}
