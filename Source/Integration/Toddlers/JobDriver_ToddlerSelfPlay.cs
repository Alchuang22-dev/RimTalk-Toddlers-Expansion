using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class JobDriver_ToddlerSelfPlay : JobDriver
	{
		private const TargetIndex PlaySpotInd = TargetIndex.A;
		private AnimationDef _playAnimation;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(job.GetTarget(PlaySpotInd), job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(() => pawn.Downed || pawn.Drafted || pawn.InMentalState);

			yield return Toils_Goto.GotoCell(PlaySpotInd, PathEndMode.OnCell);

			Toil play = ToilMaker.MakeToil("ToddlerSelfPlay");
			play.initAction = () =>
			{
				_playAnimation = ToddlerPlayAnimationUtility.GetRandomSelfPlayAnimation();
				ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				ToddlerPlayReportUtility.EnsureReportRequested(job, pawn, null, ToddlerPlayReportKind.SelfPlay);
				ToddlerPlayReportUtility.TryApplyPendingReport(job);
			};
			play.tickIntervalAction = delta =>
			{
				ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				if (ToddlerCareEventUtility.TryTriggerSelfPlayMishap(pawn, delta))
				{
					return;
				}

				ToddlerPlayReportUtility.TryApplyPendingReport(job);
				pawn.GainComfortFromCellIfPossible(delta);
				SocialNeedTuning_Toddlers.ApplySelfPlayTickEffects(pawn, delta);
				if (SocialNeedTuning_Toddlers.IsPlayNeedSatisfied(pawn))
				{
					EndJobWith(JobCondition.Succeeded);
				}
			};
			play.handlingFacing = true;
			play.defaultCompleteMode = ToilCompleteMode.Delay;
			play.defaultDuration = job.def.joyDuration;

			AddFinishAction(condition =>
			{
				ToddlerPlayAnimationUtility.ClearAnimation(pawn, _playAnimation);
				ToddlerPlayReportUtility.CancelJob(job);
				if (condition == JobCondition.Succeeded)
				{
					ToddlerPlayDialogueEvents.OnToddlerSelfPlayCompleted(pawn, job, Map);
				}
			});

			yield return play;
		}
	}
}
