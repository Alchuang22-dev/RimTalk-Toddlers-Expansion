using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Language
{
	public static class LanguageLevelUtility
	{
		private const string VersionKey = "lang1";
		private const float SyncEpsilon = 0.0001f;
		private static bool _toddlersLearningDefsResolved;
		private static HediffDef _learningToWalkDef;
		private static HediffDef _learningManipulationDef;
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

		public static bool TryGetToddlersLearningTargetProgress(Pawn pawn, out float targetProgress)
		{
			targetProgress = 0f;
			if (pawn?.health?.hediffSet == null)
			{
				return false;
			}

			ResolveToddlersLearningDefs();

			float target = 0f;
			bool hasSource = false;

			if (_learningToWalkDef != null)
			{
				Hediff walk = pawn.health.hediffSet.GetFirstHediffOfDef(_learningToWalkDef);
				if (walk != null)
				{
					target = Mathf.Max(target, walk.Severity);
					hasSource = true;
				}
			}

			if (_learningManipulationDef != null)
			{
				Hediff manipulation = pawn.health.hediffSet.GetFirstHediffOfDef(_learningManipulationDef);
				if (manipulation != null)
				{
					target = Mathf.Max(target, manipulation.Severity);
					hasSource = true;
				}
			}

			targetProgress = Mathf.Clamp01(target);
			return hasSource;
		}

		/// <summary>
		/// Sync LearningToWalk / LearningManipulation / RimTalk_ToddlerLanguageLearning to one progress.
		/// </summary>
		public static bool TrySyncLearningProgress(Pawn pawn, bool createLanguageIfMissing = true)
		{
			if (pawn?.health?.hediffSet == null || ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning == null)
			{
				return false;
			}

			if (!TryGetToddlersLearningTargetProgress(pawn, out float targetProgress))
			{
				return false;
			}

			ResolveToddlersLearningDefs();

			Hediff walk = _learningToWalkDef == null
				? null
				: pawn.health.hediffSet.GetFirstHediffOfDef(_learningToWalkDef);
			Hediff manipulation = _learningManipulationDef == null
				? null
				: pawn.health.hediffSet.GetFirstHediffOfDef(_learningManipulationDef);
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
			if (walk != null && Mathf.Abs(walk.Severity - targetProgress) > SyncEpsilon)
			{
				walk.Severity = targetProgress;
				changed = true;
			}

			if (manipulation != null && Mathf.Abs(manipulation.Severity - targetProgress) > SyncEpsilon)
			{
				manipulation.Severity = targetProgress;
				changed = true;
			}

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

		private static void ResolveToddlersLearningDefs()
		{
			if (_toddlersLearningDefsResolved)
			{
				return;
			}

			_toddlersLearningDefsResolved = true;
			_learningToWalkDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningToWalk");
			_learningManipulationDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningManipulation");
		}
	}
}
