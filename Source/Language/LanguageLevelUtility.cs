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
		private const float SyncEpsilon = 0.0001f;
		private const float DaysPerYear = 60f;
		private const float TicksPerDay = 60000f;
		private const float MinLearningFactor = 0.01f;
		private static bool _toddlersApiResolved;
		private static MethodInfo _toddlersPercentGrowthMethod;
		private static MethodInfo _toddlersLearningPerBioTickMethod;
		private static FieldInfo _toddlersManipulationLearningFactorField;
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
			if (hediff is HediffWithComps withComps)
			{
				HediffComp_LanguageLearningProgress comp = withComps.TryGetComp<HediffComp_LanguageLearningProgress>();
				if (comp != null)
				{
					progress01 = comp.Progress01;
					return true;
				}
			}

			return false;
		}

		public static bool TryGetOrCreateProgressComp(Pawn pawn, out HediffComp_LanguageLearningProgress comp)
		{
			comp = null;
			if (pawn?.health?.hediffSet == null || ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning == null)
			{
				return false;
			}

			Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
			if (hediff == null)
			{
				hediff = HediffMaker.MakeHediff(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning, pawn);
				pawn.health.AddHediff(hediff);
			}

			if (hediff is HediffWithComps withComps)
			{
				comp = withComps.TryGetComp<HediffComp_LanguageLearningProgress>();
			}

			return comp != null;
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
			if (pawn?.ageTracker == null)
			{
				return 1f / (2f * DaysPerYear * TicksPerDay);
			}

			ResolveToddlersApi();
			if (_toddlersLearningPerBioTickMethod != null)
			{
				try
				{
					object value = _toddlersLearningPerBioTickMethod.Invoke(null, new object[] { pawn, null });
					if (value is float perTick && perTick > 0f)
					{
						return perTick;
					}
				}
				catch
				{
				}
			}

			float minAge = ToddlersCompatUtility.GetToddlerMinAgeYears(pawn);
			float endAge = ToddlersCompatUtility.GetToddlerEndAgeYears(pawn);
			float stageTicks = Mathf.Max(1f, (endAge - minAge) * DaysPerYear * TicksPerDay);
			return 1f / stageTicks;
		}

		public static float GetToddlersManipulationLearningFactor()
		{
			ResolveToddlersApi();
			if (_toddlersManipulationLearningFactorField != null)
			{
				try
				{
					object value = _toddlersManipulationLearningFactorField.GetValue(null);
					if (value is float factor && factor > MinLearningFactor)
					{
						return factor;
					}
				}
				catch
				{
				}
			}

			float fallback = ToddlersExpansionSettings.learningFactor_Talking;
			return fallback > MinLearningFactor ? fallback : 1f;
		}

		public static bool TrySyncLearningProgress(Pawn pawn, bool createLanguageIfMissing = true)
		{
			if (pawn?.health?.hediffSet == null || ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning == null)
			{
				return false;
			}

			if (!TryGetToddlersLanguageInitialProgress(pawn, out float targetProgress))
			{
				return false;
			}

			Hediff language = pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
			HediffComp_LanguageLearningProgress languageComp = null;
			if (language is HediffWithComps withComps)
			{
				languageComp = withComps.TryGetComp<HediffComp_LanguageLearningProgress>();
			}
			else if (createLanguageIfMissing && TryGetOrCreateProgressComp(pawn, out HediffComp_LanguageLearningProgress created))
			{
				languageComp = created;
				language = pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
			}

			bool changed = false;
			if (languageComp != null && Mathf.Abs(languageComp.Progress01 - targetProgress) > SyncEpsilon)
			{
				languageComp.SetProgress01(targetProgress);
				changed = true;
			}
			else if (language != null && Mathf.Abs(language.Severity - targetProgress) > SyncEpsilon)
			{
				language.Severity = targetProgress;
				changed = true;
			}

			return changed;
		}

		private static bool TryGetToddlersPercentGrowth(Pawn pawn, out float percentGrowth)
		{
			percentGrowth = 0f;
			if (pawn?.ageTracker == null)
			{
				return false;
			}

			ResolveToddlersApi();
			if (_toddlersPercentGrowthMethod != null)
			{
				try
				{
					object value = _toddlersPercentGrowthMethod.Invoke(null, new object[] { pawn });
					if (value is float percent)
					{
						percentGrowth = Mathf.Clamp01(percent);
						return true;
					}
				}
				catch
				{
				}
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

			Type toddlerUtilityType = GenTypes.GetTypeInAnyAssembly("Toddlers.ToddlerUtility");
			if (toddlerUtilityType != null)
			{
				_toddlersPercentGrowthMethod = toddlerUtilityType.GetMethod("PercentGrowth", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Pawn) }, null);
			}

			Type toddlerLearningUtilityType = GenTypes.GetTypeInAnyAssembly("Toddlers.ToddlerLearningUtility");
			if (toddlerLearningUtilityType != null)
			{
				_toddlersLearningPerBioTickMethod = toddlerLearningUtilityType.GetMethod("GetLearningPerBioTick", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Pawn), typeof(Storyteller) }, null);
			}

			Type toddlersSettingsType = GenTypes.GetTypeInAnyAssembly("Toddlers.Toddlers_Settings");
			if (toddlersSettingsType != null)
			{
				_toddlersManipulationLearningFactorField = toddlersSettingsType.GetField("learningFactor_Manipulation", BindingFlags.Public | BindingFlags.Static);
			}
		}
	}
}
