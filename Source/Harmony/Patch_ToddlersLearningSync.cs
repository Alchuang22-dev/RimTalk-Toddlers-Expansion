using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimTalk_ToddlersExpansion.Language;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlersLearningSync
	{
		private const string LearningManipulationDefName = "LearningManipulation";

		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type learningUtilityType = AccessTools.TypeByName("Toddlers.ToddlerLearningUtility");
			if (learningUtilityType != null)
			{
				MethodInfo resetForAge = AccessTools.Method(learningUtilityType, "ResetHediffsForAge", new[] { typeof(Pawn), typeof(bool) })
					?? AccessTools.Method(learningUtilityType, "ResetHediffsForAge", new[] { typeof(Pawn) });
				if (resetForAge != null)
				{
					MethodInfo postfix = AccessTools.Method(typeof(Patch_ToddlersLearningSync), nameof(ResetHediffsForAge_Postfix));
					harmony.Patch(resetForAge, postfix: new HarmonyMethod(postfix));
				}
			}

			Type toddlerLearningType = AccessTools.TypeByName("Toddlers.Hediff_ToddlerLearning");
			if (toddlerLearningType != null)
			{
				MethodInfo innerTick = AccessTools.Method(toddlerLearningType, "InnerTick", new[] { typeof(float) });
				if (innerTick != null)
				{
					MethodInfo postfix = AccessTools.Method(typeof(Patch_ToddlersLearningSync), nameof(InnerTick_Postfix));
					harmony.Patch(innerTick, postfix: new HarmonyMethod(postfix));
				}
			}
		}

		private static void ResetHediffsForAge_Postfix(Pawn p)
		{
			if (p == null || p.health?.hediffSet == null || !ToddlersCompatUtility.IsToddler(p))
			{
				return;
			}

			LanguageLevelUtility.TrySyncLearningProgress(p, createLanguageIfMissing: true);
		}

		private static void InnerTick_Postfix(Hediff __instance)
		{
			Pawn pawn = __instance?.pawn;
			if (pawn == null || pawn.health?.hediffSet == null)
			{
				return;
			}

			HediffDef def = __instance.def;
			if (def == null || !string.Equals(def.defName, LearningManipulationDefName, StringComparison.Ordinal))
			{
				return;
			}

			if (!ToddlersCompatUtility.IsToddler(pawn))
			{
				return;
			}

			LanguageLevelUtility.TrySyncLearningProgress(pawn, createLanguageIfMissing: true);
		}
	}
}
