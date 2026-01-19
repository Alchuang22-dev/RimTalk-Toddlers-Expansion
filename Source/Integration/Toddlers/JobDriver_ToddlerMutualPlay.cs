using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class JobDriver_ToddlerMutualPlay : JobDriver
	{
		private const TargetIndex PartnerInd = TargetIndex.A;
		private const TargetIndex PlaySpotInd = TargetIndex.B;

		private float _initialPlayLevel = -1f;

		private Pawn Partner => TargetA.Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (!pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed))
			{
				return false;
			}

			if (TargetB.IsValid)
			{
				return pawn.Reserve(TargetB, job, 1, -1, null, errorOnFailed);
			}

			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(PartnerInd);
			this.FailOn(() => pawn.Downed || pawn.Drafted || pawn.InMentalState);
			this.FailOn(() => Partner == null || Partner.Downed || Partner.Drafted || Partner.InMentalState);
			this.FailOn(() => Partner.Map != pawn.Map);

			yield return Toils_Interpersonal.GotoInteractablePosition(PartnerInd);

			Toil play = ToilMaker.MakeToil("ToddlerMutualPlay");
			play.initAction = () =>
			{
				_initialPlayLevel = GetPlayLevel(pawn);
				ToddlerPlayReportUtility.EnsureReportRequested(job, pawn, Partner, ToddlerPlayReportKind.MutualPlay);
				ToddlerPlayReportUtility.TryApplyPendingReport(job);
			};
			play.tickIntervalAction = delta =>
			{
				if (ToddlerCareEventUtility.TryTriggerMutualPlayMishap(pawn, Partner, delta))
				{
					return;
				}

				ToddlerPlayReportUtility.TryApplyPendingReport(job);
				if (Partner != null)
				{
					pawn.rotationTracker.FaceCell(Partner.Position);
				}

				pawn.GainComfortFromCellIfPossible(delta);
				SocialNeedTuning_Toddlers.ApplyMutualPlayTickEffects(pawn, Partner, delta);

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
				ToddlerPlayReportUtility.CancelJob(job);
				if (condition != JobCondition.Succeeded)
				{
					return;
				}

				float current = GetPlayLevel(pawn);
				if (SocialNeedTuning_Toddlers.ShouldGainPlayedWithMeThought(_initialPlayLevel, current))
				{
					SocialNeedTuning_Toddlers.TryGainPlayedWithMeThought(pawn, Partner);
				}

				if (Partner != null)
				{
					ToddlerPlayDialogueEvents.OnToddlerMutualPlayCompleted(pawn, Partner, job, Map);
				}
			});

			yield return play;
		}

		private static float GetPlayLevel(Pawn pawn)
		{
			if (pawn?.needs?.play != null)
			{
				return pawn.needs.play.CurLevelPercentage;
			}

			return pawn?.needs?.joy?.CurLevelPercentage ?? -1f;
		}
	}
}
