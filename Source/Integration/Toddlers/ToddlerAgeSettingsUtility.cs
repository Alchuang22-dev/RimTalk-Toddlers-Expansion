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

		private static int _lastAppliedDays = -1;
		private static FieldInfo _harAlienRaceInfoField;
		private static bool _harReflectionResolved;

		public static float GetConfiguredToddlerMinAgeYears()
		{
			return ToddlersExpansionSettings.GetNewbornToToddlerYears();
		}

		public static void ApplyConfiguredToddlerAge(bool refreshExistingPawns)
		{
			int targetDays = ToddlersExpansionSettings.GetNewbornToToddlerDays();
			if (_lastAppliedDays == targetDays)
			{
				if (refreshExistingPawns)
				{
					RefreshExistingPawnLifeStages();
				}

				return;
			}

			float requestedYears = targetDays / DaysPerYear;
			ApplyRaceLifeStageOverrides(requestedYears);
			ApplyHarToddlerInfoOverrides(requestedYears);
			_lastAppliedDays = targetDays;

			if (refreshExistingPawns)
			{
				RefreshExistingPawnLifeStages();
			}
		}

		private static void ApplyRaceLifeStageOverrides(float requestedYears)
		{
			List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < allDefs.Count; i++)
			{
				ThingDef def = allDefs[i];
				List<LifeStageAge> stages = def?.race?.lifeStageAges;
				if (stages == null || stages.Count == 0)
				{
					continue;
				}

				int toddlerIndex = FindToddlerStageIndex(stages);
				if (toddlerIndex < 0)
				{
					continue;
				}

				float lowerBound = toddlerIndex > 0 ? stages[toddlerIndex - 1].minAge + MinStageGapYears : 0f;
				float upperBound = toddlerIndex + 1 < stages.Count ? stages[toddlerIndex + 1].minAge - MinStageGapYears : requestedYears;
				if (upperBound < lowerBound)
				{
					upperBound = lowerBound;
				}

				stages[toddlerIndex].minAge = Mathf.Clamp(requestedYears, lowerBound, upperBound);
			}
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

		private static void ApplyHarToddlerInfoOverrides(float requestedYears)
		{
			IDictionary alienRaceInfo = GetHarAlienRaceInfo();
			if (alienRaceInfo == null)
			{
				return;
			}

			foreach (DictionaryEntry entry in alienRaceInfo)
			{
				object toddlerInfo = entry.Value;
				if (toddlerInfo == null)
				{
					continue;
				}

				FieldInfo lsaToddlerField = toddlerInfo.GetType().GetField("lsa_Toddler", BindingFlags.Instance | BindingFlags.Public);
				FieldInfo toddlerMinAgeField = toddlerInfo.GetType().GetField("toddlerMinAge", BindingFlags.Instance | BindingFlags.Public);
				if (lsaToddlerField == null || toddlerMinAgeField == null)
				{
					continue;
				}

				LifeStageAge toddlerStage = lsaToddlerField.GetValue(toddlerInfo) as LifeStageAge;
				if (toddlerStage == null)
				{
					continue;
				}

				float clampedYears = toddlerStage.minAge;
				toddlerMinAgeField.SetValue(toddlerInfo, clampedYears > 0f ? clampedYears : requestedYears);
			}
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

		private static void RefreshExistingPawnLifeStages()
		{
			if (Current.ProgramState != ProgramState.Playing)
			{
				return;
			}

			List<Pawn> pawns = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn?.ageTracker == null || pawn.RaceProps?.Humanlike != true)
				{
					continue;
				}

				long ageTicks = pawn.ageTracker.AgeBiologicalTicks;
				pawn.ageTracker.AgeBiologicalTicks = ageTicks;
			}
		}
	}
}
