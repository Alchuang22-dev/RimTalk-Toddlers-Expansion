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

		private AnimationDef _playAnimation;

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

			yield return Toils_Interpersonal.GotoInteractablePosition(InitiatorInd);

			Toil play = ToilMaker.MakeToil("ToddlerMutualPlayPartner");
			play.initAction = () =>
			{
				_playAnimation = ToddlerPlayAnimationUtility.GetRandomMutualPlayAnimation();
				ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
			};
			play.tickIntervalAction = delta =>
			{
				ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);

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
				pawn.GainComfortFromCellIfPossible(delta);

				if (SocialNeedTuning_Toddlers.IsPlayNeedSatisfied(pawn))
				{
					EndJobWith(JobCondition.Succeeded);
				}
			};
			play.handlingFacing = true;
			play.defaultCompleteMode = ToilCompleteMode.Delay;
			play.defaultDuration = job.def.joyDuration;

			AddFinishAction(_ =>
			{
				ToddlerPlayAnimationUtility.ClearAnimation(pawn, _playAnimation);
			});

			yield return play;
		}
	}
}
