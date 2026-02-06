using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class JobDriver_BeingCarried_Idle : JobDriver_BeingCarriedBase
	{
		private const float ComfortGainPerTick = 0.0002f;

		protected override string ReportKey => "RimTalk_BeingCarriedIdleBy";

		protected override void TickAlways()
		{
			ApplyComfortUsed(1f);
		}

		protected override void TickEffects(int ticks)
		{
			Need_Comfort comfort = pawn?.needs?.comfort;
			if (comfort == null)
			{
				return;
			}

			float nextLevel = comfort.CurLevel + ComfortGainPerTick * ticks;
			comfort.CurLevel = Mathf.Min(1f, nextLevel);
		}
	}
}
