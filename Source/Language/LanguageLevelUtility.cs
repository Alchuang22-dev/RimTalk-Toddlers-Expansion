using RimTalk_ToddlersExpansion.Core;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Language
{
	public static class LanguageLevelUtility
	{
		private const string VersionKey = "lang1";
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
	}
}
