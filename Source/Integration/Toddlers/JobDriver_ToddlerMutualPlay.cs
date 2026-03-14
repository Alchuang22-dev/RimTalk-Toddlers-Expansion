using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimTalk_ToddlersExpansion.Integration.YayoAnimation;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class JobDriver_ToddlerMutualPlay : JobDriver
	{
		private const TargetIndex PartnerInd = TargetIndex.A;

		private float _initialPlayLevel = -1f;
		private bool _partnerJobStarted;
		private AnimationDef _playAnimation;

		private Pawn Partner => TargetA.Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(PartnerInd);
			this.FailOn(() => pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn));
			this.FailOn(() => Partner == null || Partner.Downed || Partner.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(Partner));
			this.FailOn(() => Partner.Map != pawn.Map);

			// Step 1: Start partner job first (partner will stop moving and wait)
			Toil startPartnerJob = ToilMaker.MakeToil("StartPartnerJob");
			startPartnerJob.initAction = () =>
			{
				if (!_partnerJobStarted)
				{
					_partnerJobStarted = true;
					if (!TryStartPartnerJob())
					{
						EndJobWith(JobCondition.Incompletable);
					}
				}
			};
			startPartnerJob.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return startPartnerJob;

			// Step 2: Walk to partner's position
			yield return Toils_Goto.GotoThing(PartnerInd, PathEndMode.Touch);

			// Step 3: Play together
			Toil play = ToilMaker.MakeToil("ToddlerMutualPlay");
			play.initAction = () =>
			{
				if (YayoAnimationCompatUtility.TryGetNativePlayAnimationOverride(pawn, out AnimationDef nativeAnimation))
				{
					_playAnimation = nativeAnimation;
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}
				else if (!YayoAnimationCompatUtility.ShouldUseYayoPlayAnimation(pawn))
				{
					_playAnimation = ToddlerPlayAnimationUtility.GetSharedMutualPlayAnimation(pawn, Partner);
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}

				_initialPlayLevel = GetPlayLevel(pawn);
				ToddlerPlayReportUtility.EnsureReportRequested(job, pawn, Partner, ToddlerPlayReportKind.MutualPlay);
				ToddlerPlayReportUtility.TryApplyPendingReport(job);
			};
			play.tickAction = () =>
			{
				if (_playAnimation != null && pawn.Drawer?.renderer?.CurAnimation != _playAnimation)
				{
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}

				if (ToddlerCareEventUtility.TryTriggerMutualPlayMishap(pawn, Partner, 1))
				{
					return;
				}

				ToddlerPlayReportUtility.TryApplyPendingReport(job);
				if (Partner != null)
				{
					pawn.rotationTracker.FaceCell(Partner.Position);
				}

				pawn.GainComfortFromCellIfPossible(1);
				SocialNeedTuning_Toddlers.ApplyMutualPlayTickEffects(pawn, Partner, 1);

				if (SocialNeedTuning_Toddlers.IsPlayNeedSatisfied(pawn))
				{
					EndJobWith(JobCondition.Succeeded);
				}
			};
			play.handlingFacing = true;
			play.defaultCompleteMode = ToilCompleteMode.Delay;
			play.defaultDuration = job.def.joyDuration;
			play.AddFinishAction(() => ToddlerPlayAnimationUtility.ClearAnimation(pawn, _playAnimation));

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

		private bool TryStartPartnerJob()
		{
			if (Partner?.jobs == null)
			{
				return false;
			}

			JobDef partnerJobDef = ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayPartnerJob;
			if (partnerJobDef == null)
			{
				return false;
			}

			if (Partner.CurJob?.def == partnerJobDef)
			{
				return true;
			}

			Job partnerJob = JobMaker.MakeJob(partnerJobDef, pawn);
			partnerJob.ignoreJoyTimeAssignment = true;
			partnerJob.expiryInterval = job.expiryInterval > 0 ? job.expiryInterval : partnerJob.def.joyDuration;
			return Partner.jobs.TryTakeOrderedJob(partnerJob);
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
