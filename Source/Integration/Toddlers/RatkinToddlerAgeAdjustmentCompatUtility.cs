using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	internal static class RatkinToddlerAgeAdjustmentCompatUtility
	{
		private const string SettingsTypeName = "SuperToddlerAgeAdjustment.SuperToddlerAgeAdjustmentSettings";

		private static bool _resolved;
		private static bool _loaded;
		private static bool _warned;
		private static Type _settingsType;
		private static FieldInfo _humanToddChange;
		private static FieldInfo _ratkinToddChange;
		private static FieldInfo _kiiroToddChange;
		private static FieldInfo _wolfeinToddChange;
		private static FieldInfo _miliraToddChange;
		private static FieldInfo _moelotlToddChange;
		private static FieldInfo _cinderToddChange;

		public static bool IsLoaded
		{
			get
			{
				EnsureResolved();
				return _loaded;
			}
		}

		public static bool IsRaceHandled(ThingDef raceDef)
		{
			return IsRaceHandled(raceDef?.defName);
		}

		public static bool IsAlienInfoHandled(object alienRaceToddlerInfo)
		{
			if (!TryGetAlienRaceDefName(alienRaceToddlerInfo, out string defName))
			{
				return false;
			}

			return IsRaceHandled(defName);
		}

		private static bool IsRaceHandled(string defName)
		{
			if (defName.NullOrEmpty())
			{
				return false;
			}

			EnsureResolved();
			if (!_loaded)
			{
				return false;
			}

			if (defName == "Human")
			{
				return GetBool(_humanToddChange);
			}

			if (defName.IndexOf("Ratkin", StringComparison.Ordinal) >= 0)
			{
				return GetBool(_ratkinToddChange);
			}

			if (defName == "Kiiro_Race")
			{
				return GetBool(_kiiroToddChange);
			}

			if (defName == "Wolfein_Race")
			{
				return GetBool(_wolfeinToddChange);
			}

			if (defName == "Milira_Race")
			{
				return GetBool(_miliraToddChange);
			}

			if (defName == "Axolotl")
			{
				return GetBool(_moelotlToddChange);
			}

			if (defName == "Alien_Cinder")
			{
				return GetBool(_cinderToddChange);
			}

			return false;
		}

		private static bool TryGetAlienRaceDefName(object alienRaceToddlerInfo, out string defName)
		{
			defName = null;
			if (alienRaceToddlerInfo == null)
			{
				return false;
			}

			try
			{
				FieldInfo alienRaceField = AccessTools.Field(alienRaceToddlerInfo.GetType(), "alienRace");
				if (alienRaceField?.GetValue(alienRaceToddlerInfo) is Def raceDef)
				{
					defName = raceDef.defName;
					return !defName.NullOrEmpty();
				}
			}
			catch (Exception ex)
			{
				WarnOnce("read AlienRaceToddlerInfo.alienRace", ex);
			}

			return false;
		}

		private static void EnsureResolved()
		{
			if (_resolved)
			{
				return;
			}

			_resolved = true;
			_settingsType = AccessTools.TypeByName(SettingsTypeName);
			if (_settingsType == null)
			{
				_loaded = false;
				return;
			}

			_loaded = true;
			_humanToddChange = AccessTools.Field(_settingsType, "humanToddChange");
			_ratkinToddChange = AccessTools.Field(_settingsType, "ratkinToddChange");
			_kiiroToddChange = AccessTools.Field(_settingsType, "kiiroToddChange");
			_wolfeinToddChange = AccessTools.Field(_settingsType, "wolfeinToddChange");
			_miliraToddChange = AccessTools.Field(_settingsType, "miliraToddChange");
			_moelotlToddChange = AccessTools.Field(_settingsType, "moelotlToddChange");
			_cinderToddChange = AccessTools.Field(_settingsType, "cinderToddChange");
		}

		private static bool GetBool(FieldInfo field)
		{
			if (field == null)
			{
				return false;
			}

			try
			{
				return field.GetValue(null) is bool value && value;
			}
			catch (Exception ex)
			{
				WarnOnce($"read {field.Name}", ex);
				return false;
			}
		}

		private static void WarnOnce(string context, Exception ex)
		{
			if (_warned || !Prefs.DevMode)
			{
				return;
			}

			_warned = true;
			Log.Warning($"[RimTalk_ToddlersExpansion][ToddlerAge] RatkinToddlerAgeAdjustment compatibility failed to {context}: {ex.Message}");
		}
	}
}
