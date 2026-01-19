using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ToddlerTalkRecipientEffects
	{
		private const float LonelinessReduction = 0.02f;

		private static bool _defsInitialized;
		private static HediffDef _lonelyDef;

		public static void TryApply(Pawn recipient, Pawn speaker)
		{
			if (recipient?.needs?.mood == null || !ToddlersCompatUtility.IsToddlerOrBaby(recipient))
			{
				return;
			}

			TryReduceLoneliness(recipient);
			TryGainTalkedToMemory(recipient, speaker);
		}

		private static void TryReduceLoneliness(Pawn pawn)
		{
			EnsureDefsInitialized();
			if (_lonelyDef == null || pawn?.health?.hediffSet == null)
			{
				return;
			}

			Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(_lonelyDef);
			if (hediff == null || hediff.Severity <= 0f)
			{
				return;
			}

			hediff.Severity = Mathf.Max(0f, hediff.Severity - LonelinessReduction);
		}

		private static void TryGainTalkedToMemory(Pawn pawn, Pawn speaker)
		{
			ThoughtDef thought = ToddlersExpansionThoughtDefOf.RimTalk_TalkedToBaby;
			if (thought == null || pawn?.needs?.mood?.thoughts?.memories == null)
			{
				return;
			}

			pawn.needs.mood.thoughts.memories.TryGainMemory(thought, speaker);
		}

		private static void EnsureDefsInitialized()
		{
			if (_defsInitialized)
			{
				return;
			}

			_defsInitialized = true;
			_lonelyDef = DefDatabase<HediffDef>.GetNamedSilentFail("ToddlerLonely");
		}
	}
}
