using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.BioTech;
using RimTalk_ToddlersExpansion.Language;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class LanguageLearningBootstrapComponent : GameComponent
	{
		private static bool _toddlersLearningResolved;
		private static MethodInfo _resetHediffsForAge;
		private static HediffDef _learningManipulationDef;
		private static HediffDef _learningToWalkDef;

		public LanguageLearningBootstrapComponent(Game game)
		{
		}

		public static void NotifyPawnSpawned(Pawn pawn)
		{
			bool canAddLanguage = ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning != null;
			bool canAddBabbling = ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling != null;
			int addedLanguage = 0;
			int addedBabbling = 0;
			EnsurePawnLanguageState(pawn, canAddLanguage, canAddBabbling, ref addedLanguage, ref addedBabbling);
		}

		private static void EnsurePawnLanguageState(Pawn pawn, bool canAddLanguage, bool canAddBabbling, ref int addedLanguage, ref int addedBabbling)
		{
			if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.health?.hediffSet == null)
			{
				return;
			}

			bool hasLanguageHediff = HasLanguageHediff(pawn, canAddLanguage);
			bool hasNativeLearningHediff = HasToddlersNativeLearningHediff(pawn);
			bool isToddlerLifeStage = ToddlersCompatUtility.IsToddler(pawn);
			bool isToddlerState = isToddlerLifeStage || hasLanguageHediff || hasNativeLearningHediff;
			bool isBabyOnly = BiotechCompatUtility.IsBaby(pawn) && !isToddlerState;

			if (isBabyOnly)
			{
				RemoveLanguageHediffIfPresent(pawn, canAddLanguage);
				Hediff existingBabbling = canAddBabbling
					? pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling)
					: null;
				if (canAddBabbling && existingBabbling == null)
				{
					Hediff babbling = HediffMaker.MakeHediff(ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling, pawn);
					pawn.health.AddHediff(babbling);
					addedBabbling += 1;
				}
				return;
			}

			if (isToddlerLifeStage && !hasNativeLearningHediff)
			{
				TryResetToddlersNativeLearningHediffs(pawn);
				hasNativeLearningHediff = HasToddlersNativeLearningHediff(pawn);
			}

			RemoveBabblingHediffIfPresent(pawn, canAddBabbling);
			if (canAddLanguage && isToddlerLifeStage && hasNativeLearningHediff)
			{
				Hediff existingLanguage = pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
				bool hadLanguage = existingLanguage != null;
				if (!hadLanguage
					&& LanguageLevelUtility.TryGetToddlersLanguageInitialProgress(pawn, out float initialProgress)
					&& initialProgress < 1f
					&& LanguageLevelUtility.TryGetOrCreateLanguageHediff(pawn, out Hediff language))
				{
					language.Severity = initialProgress;
					if (language != null)
					{
						addedLanguage += 1;
					}
				}
			}
		}

		private static bool HasLanguageHediff(Pawn pawn, bool canAddLanguage)
		{
			return canAddLanguage
				&& pawn?.health?.hediffSet?.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning) != null;
		}

		private static bool HasToddlersNativeLearningHediff(Pawn pawn)
		{
			EnsureToddlersLearningResolved();
			if (pawn?.health?.hediffSet == null)
			{
				return false;
			}

			return (_learningManipulationDef != null && pawn.health.hediffSet.GetFirstHediffOfDef(_learningManipulationDef) != null)
				|| (_learningToWalkDef != null && pawn.health.hediffSet.GetFirstHediffOfDef(_learningToWalkDef) != null);
		}

		private static void TryResetToddlersNativeLearningHediffs(Pawn pawn)
		{
			EnsureToddlersLearningResolved();
			if (_resetHediffsForAge == null || pawn == null)
			{
				return;
			}

			try
			{
				ParameterInfo[] parameters = _resetHediffsForAge.GetParameters();
				object[] args = parameters.Length >= 2 ? new object[] { pawn, true } : new object[] { pawn };
				_resetHediffsForAge.Invoke(null, args);
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to invoke Toddlers.ResetHediffsForAge for {pawn.LabelShort}: {ex.Message}");
				}
			}
		}

		private static void EnsureToddlersLearningResolved()
		{
			if (_toddlersLearningResolved)
			{
				return;
			}

			_toddlersLearningResolved = true;
			Type learningUtilityType = AccessTools.TypeByName("Toddlers.ToddlerLearningUtility");
			if (learningUtilityType != null)
			{
				_resetHediffsForAge = AccessTools.Method(learningUtilityType, "ResetHediffsForAge", new[] { typeof(Pawn), typeof(bool) })
					?? AccessTools.Method(learningUtilityType, "ResetHediffsForAge", new[] { typeof(Pawn) });
			}

			_learningManipulationDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningManipulation");
			_learningToWalkDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningToWalk");
		}

		private static void RemoveLanguageHediffIfPresent(Pawn pawn, bool canAddLanguage)
		{
			if (!canAddLanguage || pawn == null)
			{
				return;
			}

			Hediff existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
			if (existing != null)
			{
				pawn.health.RemoveHediff(existing);
			}
		}

		private static void RemoveBabblingHediffIfPresent(Pawn pawn, bool canAddBabbling)
		{
			if (!canAddBabbling || pawn == null)
			{
				return;
			}

			Hediff existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling);
			if (existing != null)
			{
				pawn.health.RemoveHediff(existing);
			}
		}
	}

	public static class LanguageLearningUtility
	{
		public static void RegisterGameComponent()
		{
			if (Current.Game == null)
			{
				return;
			}

			if (Current.Game.GetComponent<LanguageLearningBootstrapComponent>() == null)
			{
				Current.Game.components.Add(new LanguageLearningBootstrapComponent(Current.Game));
			}
		}
	}
}
