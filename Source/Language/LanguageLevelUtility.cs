using System;
using System.Reflection;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Language
{
	public static class LanguageLevelUtility
	{
		private const string VersionKey = "lang1";
		private const float DaysPerYear = 60f;
		private const float TicksPerDay = 60000f;
		private const float MinLearningFactor = 0.01f;
		private const int LearningFactorCacheTicks = 2500;
		private static bool _toddlersApiResolved;
		private static FieldInfo _toddlersManipulationLearningFactorField;
		private static int _cachedManipulationLearningFactorUntilTick = -1;
		private static float _cachedManipulationLearningFactor = 1f;
		private static readonly string[] TierKeys =
		{
			"babble",
			"words",
			"phrases",
			"clear"
		};

		private static readonly string[] PromptDescriptors =
		{
			"lang1:babble",
			"lang1:words",
			"lang1:phrases",
			"lang1:clear"
		};

		public static int GetLanguageTier(float progress01)
		{
			float clamped = Mathf.Clamp01(progress01);
			if (clamped < 0.25f)
			{
				return 0;
			}

			if (clamped < 0.5f)
			{
				return 1;
			}

			if (clamped < 0.75f)
			{
				return 2;
			}

			return 3;
		}

		public static string GetTierKey(int tier)
		{
			if (tier < 0 || tier >= TierKeys.Length)
			{
				return VersionKey + ":babble";
			}

			return VersionKey + ":" + TierKeys[tier];
		}

		public static string GetPromptDescriptor(float progress01)
		{
			int tier = GetLanguageTier(progress01);
			return PromptDescriptors[tier];
		}

		public static bool TryGetLanguageProgress(Pawn pawn, out float progress01)
		{
			progress01 = 0f;
			if (pawn?.health?.hediffSet == null || ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning == null)
			{
				return false;
			}

			Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
			if (hediff == null)
			{
				return false;
			}

			progress01 = Mathf.Clamp01(hediff.Severity);
			return true;
		}

		public static bool TryGetOrCreateLanguageHediff(Pawn pawn, out Hediff language)
		{
			language = null;
			if (pawn?.health?.hediffSet == null || ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning == null)
			{
				return false;
			}

			language = pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
			if (language == null)
			{
				language = HediffMaker.MakeHediff(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning, pawn);
				pawn.health.AddHediff(language);
			}

			return language != null;
		}

		// Mirrors Toddlers.ToddlerLearningUtility.ResetHediffsForAge initial severity logic:
		// severity = PercentGrowth(pawn) / Toddlers_Settings.learningFactor_Manipulation
		public static bool TryGetToddlersLanguageInitialProgress(Pawn pawn, out float progress01)
		{
			progress01 = 0f;
			if (pawn?.ageTracker == null)
			{
				return false;
			}

			if (ToddlersCompatUtility.IsToddlersActive && !ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			if (!TryGetToddlersPercentGrowth(pawn, out float percentGrowth))
			{
				return false;
			}

			progress01 = Mathf.Clamp01(percentGrowth / GetToddlersManipulationLearningFactor());
			return true;
		}

		public static float GetLearningPerBioTick(Pawn pawn)
		{
			float minAge = ToddlersCompatUtility.GetToddlerMinAgeYears(pawn);
			float endAge = ToddlersCompatUtility.GetToddlerEndAgeYears(pawn);
			float stageTicks = Mathf.Max(1f, (endAge - minAge) * DaysPerYear * TicksPerDay);
			return 1f / stageTicks;
		}

		public static float GetToddlersManipulationLearningFactor()
		{
			int ticksGame = Find.TickManager?.TicksGame ?? -1;
			if (ticksGame >= 0 && ticksGame < _cachedManipulationLearningFactorUntilTick)
			{
				return _cachedManipulationLearningFactor;
			}

			ResolveToddlersApi();
			float result = 1f;
			if (_toddlersManipulationLearningFactorField != null)
			{
				try
				{
					object value = _toddlersManipulationLearningFactorField.GetValue(null);
					if (value is float factor && factor > MinLearningFactor)
					{
						result = factor;
					}
				}
				catch
				{
				}
			}

			_cachedManipulationLearningFactor = result;
			if (ticksGame >= 0)
			{
				_cachedManipulationLearningFactorUntilTick = ticksGame + LearningFactorCacheTicks;
			}

			return result;
		}

		private static bool TryGetToddlersPercentGrowth(Pawn pawn, out float percentGrowth)
		{
			percentGrowth = 0f;
			if (pawn?.ageTracker == null)
			{
				return false;
			}

			float minAge = ToddlersCompatUtility.GetToddlerMinAgeYears(pawn);
			float endAge = ToddlersCompatUtility.GetToddlerEndAgeYears(pawn);
			float stageTicks = Mathf.Max(1f, (endAge - minAge) * DaysPerYear * TicksPerDay);
			float ticksSinceBaby = pawn.ageTracker.AgeBiologicalTicks - (minAge * DaysPerYear * TicksPerDay);
			percentGrowth = Mathf.Clamp01(ticksSinceBaby / stageTicks);
			return true;
		}

		private static void ResolveToddlersApi()
		{
			if (_toddlersApiResolved)
			{
				return;
			}

			_toddlersApiResolved = true;

			Type toddlersSettingsType = GenTypes.GetTypeInAnyAssembly("Toddlers.Toddlers_Settings");
			if (toddlersSettingsType != null)
			{
				_toddlersManipulationLearningFactorField = toddlersSettingsType.GetField("learningFactor_Manipulation", BindingFlags.Public | BindingFlags.Static);
			}
		}
	}
}
