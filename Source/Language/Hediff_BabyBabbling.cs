using RimTalk_ToddlersExpansion.Integration.BioTech;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Language
{
	public sealed class Hediff_BabyBabbling : Hediff
	{
		private const int UpdateIntervalTicks = 2500;
		private bool _removeBecauseNotBaby;

		public override bool ShouldRemove => pawn == null
			|| _removeBecauseNotBaby;

		public override string SeverityLabel => null;

		public override void TickInterval(int delta)
		{
			base.TickInterval(delta);
			if (pawn == null || !pawn.IsHashIntervalTick(UpdateIntervalTicks, delta))
			{
				return;
			}

			if (ToddlersCompatUtility.IsToddler(pawn) || !BiotechCompatUtility.IsBaby(pawn))
			{
				_removeBecauseNotBaby = true;
			}
		}
	}
}
