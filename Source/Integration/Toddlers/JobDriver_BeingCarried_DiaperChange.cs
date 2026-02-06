using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class JobDriver_BeingCarried_DiaperChange : JobDriver_BeingCarriedBase
	{
		private const float HygieneGainPerTick = 0.0004f;

		protected override string ReportKey => "RimTalk_BeingCarriedDiaperChangeBy";

		protected override void TickAlways()
		{
			ApplyComfortUsed(1f);
			if (pawn != null && pawn.IsHashIntervalTick(15))
			{
				ApplyHygieneGain(15);
			}
		}

		protected override void TickEffects(int ticks)
		{
		}

		private void ApplyHygieneGain(int ticks)
		{
			if (pawn == null)
			{
				return;
			}

			Need hygiene = ToddlerSelfBathUtility.GetHygieneNeed(pawn);
			if (hygiene == null || hygiene.CurLevelPercentage >= 0.999f)
			{
				return;
			}

			ToddlerSelfBathUtility.ApplyHygieneClean(pawn, null, HygieneGainPerTick, ticks);
		}
	}
}
