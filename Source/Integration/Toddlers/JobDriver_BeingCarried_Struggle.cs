namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class JobDriver_BeingCarried_Struggle : JobDriver_BeingCarriedBase
	{
		protected override string ReportKey => "RimTalk_BeingCarriedStruggleBy";

		protected override void OnStart()
		{
			if (!ToddlerCarryingUtility.IsBeingCarried(pawn))
			{
				return;
			}

			var carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			CarriedToddlerStateUtility.TryQueueStruggleTalk(carrier, pawn);
			ToddlerCarryingUtility.DismountToddler(pawn);
		}
	}
}
