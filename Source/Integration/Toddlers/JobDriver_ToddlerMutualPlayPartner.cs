using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class JobDriver_ToddlerMutualPlayPartner : JobDriver
	{
		private const TargetIndex InitiatorInd = TargetIndex.A;
		private const int MaxPartnerDistance = 6;

		private Pawn Initiator => TargetA.Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(InitiatorInd);
			this.FailOn(() => pawn.Downed || pawn.Drafted || pawn.InMentalState);
			this.FailOn(() => Initiator == null || Initiator.Downed || Initiator.Drafted || Initiator.InMentalState);
			this.FailOn(() => Initiator.Map != pawn.Map);

			// Step 1: Wait for initiator to arrive (stop moving and stay in place)
			Toil waitForInitiator = ToilMaker.MakeToil("WaitForInitiator");
			waitForInitiator.initAction = () =>
			{
				pawn.pather.StopDead();
			};
			waitForInitiator.tickAction = () =>
			{
				if (Initiator == null
					|| Initiator.CurJob?.def != ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob
					|| Initiator.CurJob.targetA.Thing != pawn)
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				pawn.rotationTracker.FaceCell(Initiator.Position);
			};
			waitForInitiator.handlingFacing = true;
			waitForInitiator.defaultCompleteMode = ToilCompleteMode.Never;
			waitForInitiator.AddEndCondition(() =>
			{
				if (Initiator != null && pawn.Position.InHorDistOf(Initiator.Position, 2))
				{
					return JobCondition.Succeeded;
				}
				return JobCondition.Ongoing;
			});
			yield return waitForInitiator;

			// Step 2: Play together
			Toil play = ToilMaker.MakeToil("ToddlerMutualPlayPartner");
			play.tickAction = () =>
			{
				if (Initiator == null
					|| Initiator.CurJob?.def != ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob
					|| Initiator.CurJob.targetA.Thing != pawn)
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				if (!pawn.Position.InHorDistOf(Initiator.Position, MaxPartnerDistance))
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				pawn.rotationTracker.FaceCell(Initiator.Position);
				pawn.GainComfortFromCellIfPossible(1);

				if (SocialNeedTuning_Toddlers.IsPlayNeedSatisfied(pawn))
				{
					EndJobWith(JobCondition.Succeeded);
				}
			};
			play.handlingFacing = true;
			play.defaultCompleteMode = ToilCompleteMode.Delay;
			play.defaultDuration = job.def.joyDuration;

			yield return play;
		}
	}
}
