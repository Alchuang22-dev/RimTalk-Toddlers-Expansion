using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimTalk_ToddlersExpansion.Language;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlersLearningSync
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type learningUtilityType = AccessTools.TypeByName("Toddlers.ToddlerLearningUtility");
			if (learningUtilityType != null)
			{
				MethodInfo resetForAge = AccessTools.Method(learningUtilityType, "ResetHediffsForAge", new[] { typeof(Pawn), typeof(bool) })
					?? AccessTools.Method(learningUtilityType, "ResetHediffsForAge", new[] { typeof(Pawn) });
				if (resetForAge != null)
				{
					bool hasClearExisting = resetForAge.GetParameters().Length >= 2;
					MethodInfo postfix = AccessTools.Method(
						typeof(Patch_ToddlersLearningSync),
						hasClearExisting ? nameof(ResetHediffsForAge_WithFlag_Postfix) : nameof(ResetHediffsForAge_Postfix));
					harmony.Patch(resetForAge, postfix: new HarmonyMethod(postfix));
				}
			}
		}

		private static void ResetHediffsForAge_WithFlag_Postfix(Pawn p, bool clearExisting)
		{
			SyncLanguageForReset(p, clearExisting);
		}

		private static void ResetHediffsForAge_Postfix(Pawn p)
		{
			SyncLanguageForReset(p, clearExisting: true);
		}

		private static void SyncLanguageForReset(Pawn p, bool clearExisting)
		{
			if (p == null || !ToddlersCompatUtility.IsToddler(p))
			{
				return;
			}

			if (p.ageTracker == null || p.health == null || p.health.hediffSet == null)
			{
				return;
			}

			if (ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning == null)
			{
				return;
			}

			if (clearExisting)
			{
				Hediff existing = p.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
				if (existing != null)
				{
					p.health.RemoveHediff(existing);
				}
			}

			if (!LanguageLevelUtility.TryGetToddlersLanguageInitialProgress(p, out float initialProgress))
			{
				return;
			}

			Hediff language = HediffMaker.MakeHediff(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning, p);
			language.Severity = initialProgress;
			p.health.AddHediff(language);
		}
	}
}
