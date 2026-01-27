using UnityEngine;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class JobDriver_BeingCarried_Sleep : JobDriver_BeingCarriedBase
	{
		private const float RestGainPerTick = 0.00012f;

		protected override string ReportKey => "RimTalk_BeingCarriedSleepBy";

		protected override void TickEffects(int ticks)
		{
			var rest = pawn?.needs?.rest;
			if (rest == null)
			{
				return;
			}

			float nextLevel = rest.CurLevel + RestGainPerTick * ticks;
			rest.CurLevel = Mathf.Min(1f, nextLevel);
		}
	}
}
