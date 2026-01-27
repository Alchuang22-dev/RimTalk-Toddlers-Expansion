using RimWorld;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class JobDriver_BeingCarried_Observe : JobDriver_BeingCarriedBase
	{
		private const float JoyGainPerTick = 0.00008f;

		protected override string ReportKey => "RimTalk_BeingCarriedObserveBy";

		protected override void TickEffects(int ticks)
		{
			if (pawn?.needs?.joy == null)
			{
				return;
			}

			pawn.needs.joy.GainJoy(JoyGainPerTick * ticks, JoyKindDefOf.Meditative);
		}
	}
}
