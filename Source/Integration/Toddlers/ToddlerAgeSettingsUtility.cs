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
		private static FieldInfo _cachedLifeStageIndexField;
		private static bool _ageTrackerReflectionResolved;

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

			minAge = stages[toddlerIndex].minAge;
			return true;
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
			ApplyHarToddlerInfoOverrides(requestedYears);
			_lastAppliedDays = targetDays;
			Log.Message($"[RimTalk_ToddlersExpansion][ToddlerAge] Applied newborn-to-toddler threshold: {targetDays} day(s) ({requestedYears:0.###} years); updated race life stages: {updatedRaceStages}; refreshExistingPawns={refreshExistingPawns}.");

			if (refreshExistingPawns)
			{
				RefreshExistingPawnLifeStages();
			}
		}

		private static int ApplyRaceLifeStageOverrides(float requestedYears)
		{
			int updatedCount = 0;
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

				float babyMinAge = toddlerIndex > 0 ? stages[toddlerIndex - 1].minAge : 0f;
				float targetMinAge = babyMinAge + requestedYears;
				float lowerBound = babyMinAge + MinStageGapYears;
				float upperBound = toddlerIndex + 1 < stages.Count ? stages[toddlerIndex + 1].minAge - MinStageGapYears : targetMinAge;
				if (upperBound < lowerBound)
				{
					upperBound = lowerBound;
				}

				stages[toddlerIndex].minAge = Mathf.Clamp(targetMinAge, lowerBound, upperBound);
				updatedCount++;
			}

			return updatedCount;
		}

		public static bool TryGetConfiguredToddlerMinAgeForAlienInfo(object alienRaceToddlerInfo, out float minAge)
		{
			minAge = 0f;
			if (alienRaceToddlerInfo == null)
			{
				return false;
			}

			Type type = alienRaceToddlerInfo.GetType();
			FieldInfo babyField = AccessTools.Field(type, "lsa_Baby");
			LifeStageAge babyStage = babyField?.GetValue(alienRaceToddlerInfo) as LifeStageAge;
			if (babyStage == null)
			{
				return false;
			}

			float targetMinAge = babyStage.minAge + GetConfiguredToddlerMinAgeYears();
			float toddlerEndAge = GetToddlerEndAgeFromAlienInfo(type, alienRaceToddlerInfo);
			minAge = ClampToddlerMinAge(targetMinAge, babyStage.minAge, toddlerEndAge);
			return true;
		}

		private static float GetToddlerEndAgeFromAlienInfo(Type type, object alienRaceToddlerInfo)
		{
			FieldInfo toddlerEndAgeField = AccessTools.Field(type, "toddlerEndAge");
			if (toddlerEndAgeField?.GetValue(alienRaceToddlerInfo) is float toddlerEndAge && toddlerEndAge > 0f)
			{
				return toddlerEndAge;
			}

			FieldInfo childField = AccessTools.Field(type, "lsa_Child");
			if (childField?.GetValue(alienRaceToddlerInfo) is LifeStageAge childStage)
			{
				return childStage.minAge;
			}

			return -1f;
		}

		private static float ClampToddlerMinAge(float targetMinAge, float babyMinAge, float toddlerEndAge)
		{
			float lowerBound = babyMinAge + MinStageGapYears;
			if (toddlerEndAge <= 0f)
			{
				return Mathf.Max(targetMinAge, lowerBound);
			}

			float upperBound = toddlerEndAge - MinStageGapYears;
			if (upperBound < lowerBound)
			{
				upperBound = lowerBound;
			}

			return Mathf.Clamp(targetMinAge, lowerBound, upperBound);
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
			int preparedToddlerEvents = 0;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn?.ageTracker == null || pawn.RaceProps?.Humanlike != true)
				{
					continue;
				}

				if (PrepareBabyToToddlerTransitionEvent(pawn))
				{
					preparedToddlerEvents++;
				}

				long ageTicks = pawn.ageTracker.AgeBiologicalTicks;
				pawn.ageTracker.AgeBiologicalTicks = ageTicks;
			}

			if (preparedToddlerEvents > 0)
			{
				Log.Message($"[RimTalk_ToddlersExpansion][ToddlerAge] Prepared Toddlers baby-to-toddler transition event for {preparedToddlerEvents} loaded pawn(s).");
			}
		}

		public static void RefreshExistingPawnLifeStagesForConfiguredAge()
		{
			ApplyConfiguredToddlerAge(refreshExistingPawns: true);
		}

		private static bool PrepareBabyToToddlerTransitionEvent(Pawn pawn)
		{
			List<LifeStageAge> stages = pawn?.def?.race?.lifeStageAges;
			if (stages == null || stages.Count == 0 || pawn.ageTracker == null)
			{
				return false;
			}

			int toddlerIndex = FindToddlerStageIndex(stages);
			if (toddlerIndex <= 0)
			{
				return false;
			}

			int babyIndex = toddlerIndex - 1;
			LifeStageDef babyStage = stages[babyIndex]?.def;
			if (babyStage == null || !babyStage.developmentalStage.Baby())
			{
				return false;
			}

			int targetIndex = GetLifeStageIndexForAge(stages, pawn.ageTracker.AgeBiologicalYearsFloat);
			if (targetIndex != toddlerIndex)
			{
				return false;
			}

			if (HasToddlerBackstory(pawn))
			{
				return false;
			}

			FieldInfo cachedLifeStageIndexField = GetCachedLifeStageIndexField();
			if (cachedLifeStageIndexField == null)
			{
				return false;
			}

			if (cachedLifeStageIndexField.GetValue(pawn.ageTracker) is int cachedIndex && cachedIndex == babyIndex)
			{
				return false;
			}

			cachedLifeStageIndexField.SetValue(pawn.ageTracker, babyIndex);
			return true;
		}

		private static int GetLifeStageIndexForAge(List<LifeStageAge> stages, float ageYears)
		{
			for (int i = stages.Count - 1; i >= 0; i--)
			{
				LifeStageAge stage = stages[i];
				if (stage != null && stage.minAge <= ageYears + 1E-06f)
				{
					return i;
				}
			}

			return 0;
		}

		private static bool HasToddlerBackstory(Pawn pawn)
		{
			List<string> categories = pawn?.story?.Childhood?.spawnCategories;
			return categories != null && categories.Contains("Toddler");
		}

		private static FieldInfo GetCachedLifeStageIndexField()
		{
			if (!_ageTrackerReflectionResolved)
			{
				_ageTrackerReflectionResolved = true;
				_cachedLifeStageIndexField = AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex");
			}

			return _cachedLifeStageIndexField;
		}

	}
}
