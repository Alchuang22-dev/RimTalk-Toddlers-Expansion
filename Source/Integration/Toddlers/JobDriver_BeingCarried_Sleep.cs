using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class JobDriver_BeingCarried_Sleep : JobDriver_BeingCarriedBase
	{
		private const float RestGainPerTick = 0.00012f;
		private const float ComfortGainPerTick = 0.0002f;

		protected override string ReportKey => "RimTalk_BeingCarriedSleepBy";

		protected override void TickAlways()
		{
			ApplyComfortUsed(1f);
			Need_Rest rest = pawn?.needs?.rest;
			if (rest != null)
			{
				rest.TickResting(1f);
			}
		}

		protected override void TickEffects(int ticks)
		{
			var rest = pawn?.needs?.rest;
			if (rest == null)
			{
				return;
			}

			float nextLevel = rest.CurLevel + RestGainPerTick * ticks;
			rest.CurLevel = Mathf.Min(1f, nextLevel);

			Need_Comfort comfort = pawn?.needs?.comfort;
			if (comfort == null)
			{
				return;
			}

			float comfortNext = comfort.CurLevel + ComfortGainPerTick * ticks;
			comfort.CurLevel = Mathf.Min(1f, comfortNext);
		}
	}
}
