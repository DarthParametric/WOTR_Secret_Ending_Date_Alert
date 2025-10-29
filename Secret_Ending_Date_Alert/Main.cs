using Secret_Ending_Date_Alert.Events;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Root.Strings.GameLog;
using Kingmaker.Kingdom;
using Kingmaker.PubSubSystem;
using Kingmaker.Settings;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.Models.Log.CombatLog_ThreadSystem;
using Kingmaker.UI.Models.Log.CombatLog_ThreadSystem.LogThreads.Common;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using System.Globalization;
using System.Text;
using UnityModManagerNet;

namespace Secret_Ending_Date_Alert;

public static class Main
{
    internal static Harmony HarmonyInstance;
    internal static UnityModManager.ModEntry.ModLogger log;
    private static OnDayChanged m_day_changed_handler;

    public static bool Load(UnityModManager.ModEntry modEntry)
    {
        log = modEntry.Logger;
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        m_day_changed_handler = new OnDayChanged();
        EventBus.Subscribe(m_day_changed_handler);
        return true;
    }

    public static void LogDebug(string message)
    {
#if DEBUG
        log.Log($"DEBUG: {message}");
#endif
    }

    // Stolen from https://stackoverflow.com/a/40940304

    /// <summary>
    /// Pluralize: takes a word, inserts a number in front, and makes the word plural if the number is not exactly 1.
    /// </summary>
    /// <example>"{n.Pluralize("maid")} a-milking</example>
    /// <param name="word">The word to make plural</param>
    /// <param name="number">The number of objects</param>
    /// <param name="pluralSuffix">An optional suffix; "s" is the default.</param>
    /// <param name="singularSuffix">An optional suffix if the count is 1; "" is the default.</param>
    /// <returns>Formatted string: "number word[suffix]", pluralSuffix (default "s") only added if the number is not 1, otherwise singularSuffix (default "") added</returns>
    internal static string Pluralize(this int number, string word, string pluralSuffix = "s", string singularSuffix = "")
    {
        return $@"{number} {word}{(number != 1 ? pluralSuffix : singularSuffix)}";
    }

    public static void SecretEndingAlert()
    {
		var CurrentChapter = Game.Instance.Player.Chapter;
		var IsFinale = false;

		if (CurrentChapter == 5 || CurrentChapter == 6)
			IsFinale = true;

		// Skip altogether during chapter 1. After beta testing, switch to only trigger during the final run to the endgame.
		if (!KingdomState.Founded)
        {
			return;
        }
		/*
		if (!IsFinale)
			return;
		*/

		var Start = BlueprintRoot.Instance.Calendar.GetStartDate();
		var TimePF = Game.Instance.TimeController.GameTime;
		var TimeReal = Start.AddYears(2700) + TimePF;
		var CurrentDay = TimeReal.Day;
		
		// The secret ending requires entering Threshold in the third week of Gozran (April). The year doesn't matter, despite the report from the Pulura Falls
		// research saying 4717:
		//		"The study of the stargazers' observations made it possible to ascertain some interesting patterns in the "life" of the Worldwound — namely, discover
		//		a date when the connection between the two planes, Golarion and the Abyss, is the strongest and energy freely flows both ways through the rifts. The
		//		date is the third week of Gozran 4717."
		// Per \World\Dialogs\c6\TrueEnding\Suggest\Answer_0023.jbp, the game only checks the month and date being between the 15th and 21st, not the year.

		// Check if the current date is within Gozran/April.
		if (TimeReal.Month == 4)
		{
			// Since trying to adopt Owlcat's use of CultureInfo.InvariantCulture/DateTimeStyles.AssumeUniversal wasn't working,
			// first day of the week differences require an offset to report the Gregorian day name that properly matches the PF day.
			CultureInfo CI = CultureInfo.CurrentCulture;
			DayOfWeek WeekStart = CI.DateTimeFormat.FirstDayOfWeek;
			int DayOffset = 0;

			if (WeekStart != DayOfWeek.Sunday)
			{
				DayOffset = (int)WeekStart;
				LogDebug($"SecretEndingAlert: System culture is {CI}, first day of week is {WeekStart}, DayOffset = -{DayOffset}");
			}

			LogDebug($"SecretEndingAlert: Current chapter is {CurrentChapter}, current date is {BlueprintRoot.Instance.Calendar.GetDateText(TimePF, GameDateFormat.Extended, false)} / {TimeReal - TimeSpan.FromDays(DayOffset):dddd}, {TimeReal:d MMMM, yyyy}");

			// Have a pop-up notification. SendWarning is a small non-interactive notice that fades after a few seconds. ShowMessageBox requires user interaction
			// and blocks further play until acknowledged. Allow the user to set their preference for either (or neither) via ModMenu.
			// Available modal types are Message, Dialog, TextField. Dialog accounts for yes/no player input, message only has a single player input(?).
			// TextField is similar to Dialog, but allows for a second string input. An example is the overwriting a save confirmation pop-up.

			// Temp variables for testing until ModMenu implementation.
			var MMAllowPopUps = true;
			var MMDialogue = true;
			var MMMessage = true;

			if (MMDialogue)
			{
				// Given this notification type is very intrusive, only fire on specific occasions: start of April and on the final valid day.

				if (CurrentDay == 1)
				{
					// Pop a notice at the start of the month to give the player a few weeks advance notice.
					UIUtility.ShowMessageBox("Access to the secret ending is approaching! You must enter Threshold during the third week of this month.", MessageModalBase.ModalType.Message, null, null, 0, "OK");
				}
			}

			// Check if the current date lies inside the valid window.
			if (CurrentDay >= 15 && CurrentDay <= 21)
			{
				if ((bool)(SettingsEntity<bool>)SettingsRoot.Difficulty.AutoCrusade)
				{
					log.Log("Warning! AutoCrusade has been enabled, the secret ending may not be achievable!");
				}

				if (MMDialogue && CurrentDay == 21)
				{
					// Warn user today is their final chance for this calendar year.
					UIUtility.ShowMessageBox("Warning! Today is the last day you can complete the secret ending until next year!", MessageModalBase.ModalType.Message, null, null, 0, "OK");
				}

				var DaysRemain = (int)(21 - CurrentDay + 1);
				string sMsg = $@"{DaysRemain.Pluralize("day")} remaining this year to enter Threshold for the secret ending!";
				string sPopUp = string.Empty;

				StringBuilder sPopTmp = GameLogUtility.StringBuilder;
				sPopTmp.Append($"You must enter Threshold during the third week of Gorzan/April (any year).");
				sPopTmp.AppendLine();
				sPopTmp.AppendLine();
				sPopTmp.Append($"Current PF Date: {BlueprintRoot.Instance.Calendar.GetDateText(TimePF, GameDateFormat.Extended, false)}");
				sPopTmp.AppendLine();
				sPopTmp.AppendLine();
				sPopTmp.Append($"Gregorian Date: {TimeReal - TimeSpan.FromDays(DayOffset):dddd}, {TimeReal:d MMMM, yyyy}");
				sPopTmp.AppendLine();
				sPopTmp.AppendLine();
				sPopTmp.Append($@"Remaining Time: {DaysRemain.Pluralize("Day")}");

				sPopUp = sPopTmp.ToString();
				sPopTmp.Clear();

				UnityEngine.Color MsgColour = new(0f, 0.157f, 0.494f);
				TooltipTemplateCombatLogMessage CountdownTooltip = new("Secret Ending Date Alert", sPopUp);
				CombatLogMessage message = new(sMsg, MsgColour, PrefixIcon.RightArrow, CountdownTooltip, true);
				var messageLog = LogThreadService.Instance.m_Logs[LogChannelType.Common].First(x => x is MessageLogThread);

				messageLog.AddMessage(message);

				if (MMAllowPopUps && MMMessage)
				{
					// Since this message type is non-interactive and fairly unintrusive, it can safely be fired every time if enabled.
					UIUtility.SendWarning(sMsg, false);
				}
			}
		}
	}
}
