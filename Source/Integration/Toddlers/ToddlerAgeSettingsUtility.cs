using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ToddlerAgeSettingsUtility
	{
		private const float DaysPerYear = 60f;
		private const float MinStageGapYears = 1f / DaysPerYear;
		private const float DefaultToddlerMinAge = 1f;
		private const float DefaultChildMinAge = 3f;

		private static int _lastAppliedDays = -1;
		private static readonly Dictionary<LifeStageAge, float> _appliedToddlerAges =
			new Dictionary<LifeStageAge, float>();
		private static FieldInfo _harAlienRaceInfoField;
		private static bool _harReflectionResolved;

		public static float GetConfiguredToddlerMinAgeYears()
		{
			return ToddlersExpansionSettings.GetNewbornToToddlerYears();
		}

		public static bool TryGetConfiguredToddlerMinAge(Pawn pawn, out float minAge)
		{
			minAge = 0f;
			List<LifeStageAge> stages = pawn?.def?.race?.lifeStageAges;
			if (stages == null || stages.Count == 0)
			{
				return false;
			}

			int toddlerIndex = FindToddlerStageIndex(stages);
			if (toddlerIndex < 0)
			{
				return false;
			}

			LifeStageAge stage = stages[toddlerIndex];
			return _appliedToddlerAges.TryGetValue(stage, out minAge) && stage.minAge == minAge;
		}

		public static void ApplyConfiguredToddlerAge(bool refreshExistingPawns)
		{
			int targetDays = ToddlersExpansionSettings.GetNewbornToToddlerDays();
			if (_lastAppliedDays == targetDays && !refreshExistingPawns)
			{
				return;
			}

			float requestedYears = targetDays / DaysPerYear;
			int updatedRaceStages = ApplyRaceLifeStageOverrides(requestedYears);
			_lastAppliedDays = targetDays;
			Log.Message($"[RimTalk_ToddlersExpansion][ToddlerAge] Applied newborn-to-toddler threshold: {targetDays} day(s) ({requestedYears:0.###} years); updated race life stages: {updatedRaceStages}; refreshExistingPawns={refreshExistingPawns}.");

			// Do not force existing pawns to recalculate here. RimWorld's normal age tick path will start
			// the toddler life stage when the pawn naturally crosses the configured threshold.
		}

		private static int ApplyRaceLifeStageOverrides(float requestedYears)
		{
			int updatedCount = 0;
			IDictionary alienRaceInfo = GetHarAlienRaceInfo();
			List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
			HashSet<LifeStageAge> externallyHandledStages = new HashSet<LifeStageAge>();
			foreach (ThingDef race in allDefs)
			{
				if (race.race?.lifeStageAges is List<LifeStageAge> stages
					&& RatkinToddlerAgeAdjustmentCompatUtility.IsRaceHandled(race))
				{
					int toddlerIndex = FindToddlerStageIndex(stages);
					if (toddlerIndex >= 0)
					{
						externallyHandledStages.Add(stages[toddlerIndex]);
					}
				}
			}

			float targetAge = Mathf.Clamp(requestedYears, MinStageGapYears, DefaultChildMinAge - MinStageGapYears);
			for (int i = 0; i < allDefs.Count; i++)
			{
				ThingDef def = allDefs[i];
				List<LifeStageAge> stages = def?.race?.lifeStageAges;
				if (stages == null || stages.Count < 3)
				{
					continue;
				}

				int toddlerIndex = FindToddlerStageIndex(stages);
				if (toddlerIndex < 1 || toddlerIndex + 1 >= stages.Count)
				{
					continue;
				}

				LifeStageAge baby = stages[toddlerIndex - 1];
				LifeStageAge toddler = stages[toddlerIndex];
				LifeStageAge child = stages[toddlerIndex + 1];
				// Only the standard schedule or a value established by this setting may be changed.
				if (baby == null || baby.def != LifeStageDefOf.HumanlikeBaby || baby.minAge != 0f
					|| toddler.def.defName != "HumanlikeToddler" || externallyHandledStages.Contains(toddler)
					|| child == null || child.def != LifeStageDefOf.HumanlikeChild || child.minAge != DefaultChildMinAge)
				{
					continue;
				}

				float oldAge = toddler.minAge;
				float expectedAge = _appliedToddlerAges.TryGetValue(toddler, out float lastAppliedAge)
					? lastAppliedAge : DefaultToddlerMinAge;
				if (oldAge != expectedAge || oldAge == targetAge)
				{
					continue;
				}

				object info = alienRaceInfo?[def];
				if (info != null && !TryUpdateHarToddlerMinAge(info, toddler, oldAge, targetAge))
				{
					continue;
				}

				toddler.minAge = targetAge;
				_appliedToddlerAges[toddler] = targetAge;
				updatedCount++;
			}

			return updatedCount;
		}

		public static bool IsRatkinToddlerAgeAdjustmentLoaded()
		{
			return RatkinToddlerAgeAdjustmentCompatUtility.IsLoaded;
		}

		private static int FindToddlerStageIndex(List<LifeStageAge> stages)
		{
			for (int i = 0; i < stages.Count; i++)
			{
				LifeStageAge stage = stages[i];
				if (stage?.def == null)
				{
					continue;
				}

				if (stage.def.defName == "HumanlikeToddler")
				{
					return i;
				}

				string workerName = stage.def.workerClass?.Name;
				if (!workerName.NullOrEmpty() && workerName == "LifeStageWorker_HumanlikeToddler")
				{
					return i;
				}
			}

			return -1;
		}

		private static bool TryUpdateHarToddlerMinAge(object info, LifeStageAge toddlerStage, float oldAge, float newAge)
		{
			Type type = info.GetType();
			FieldInfo stageField = AccessTools.Field(type, "lsa_Toddler");
			FieldInfo ageField = AccessTools.Field(type, "toddlerMinAge");
			if (!ReferenceEquals(stageField?.GetValue(info), toddlerStage)
				|| ageField?.FieldType != typeof(float) || ageField.IsInitOnly || ageField.IsStatic
				|| !(ageField.GetValue(info) is float cachedAge) || cachedAge != oldAge)
			{
				return false;
			}

			ageField.SetValue(info, newAge);
			return true;
		}

		private static IDictionary GetHarAlienRaceInfo()
		{
			if (!_harReflectionResolved)
			{
				_harReflectionResolved = true;
				Type harCompatType = AccessTools.TypeByName("Toddlers.HARCompat");
				if (harCompatType != null)
				{
					_harAlienRaceInfoField = AccessTools.Field(harCompatType, "alienRaceInfo");
				}
			}

			return _harAlienRaceInfoField?.GetValue(null) as IDictionary;
		}

	}
}
