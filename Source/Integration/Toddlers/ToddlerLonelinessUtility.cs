using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	internal static class ToddlerLonelinessUtility
	{
		private const float SeekCompanyThreshold = 0.4f;
		private const float EnoughCompanyThreshold = 0.1f;

		private static readonly HediffDef LonelyDef = DefDatabase<HediffDef>.GetNamedSilentFail("ToddlerLonely");

		public static bool SeeksCompany(Pawn pawn) => GetLevel(pawn) >= SeekCompanyThreshold;

		public static bool WantsMoreCompany(Pawn pawn) => GetLevel(pawn) > EnoughCompanyThreshold;

		private static float GetLevel(Pawn pawn) => GetLonelyHediff(pawn)?.Severity ?? 0f;

		public static void Reduce(Pawn pawn, float amount)
		{
			Hediff lonely = GetLonelyHediff(pawn);
			if (lonely != null && amount > 0f && lonely.Severity > LonelyDef.minSeverity)
			{
				lonely.Severity = Mathf.Max(LonelyDef.minSeverity, lonely.Severity - amount);
			}
		}

		private static Hediff GetLonelyHediff(Pawn pawn)
		{
			if (LonelyDef == null)
			{
				return null;
			}

			Hediff lonely = pawn?.health?.hediffSet?.GetFirstHediffOfDef(LonelyDef);
			return lonely != null && ToddlersCompatUtility.IsToddler(pawn) ? lonely : null;
		}
	}
}
