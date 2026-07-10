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
		private bool _failureLogged;
		private AnimationDef _playAnimation;

		private Pawn Partner => TargetA.Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			bool reserved = pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
			MutualPlayDiagnostics.Log(
				pawn,
				"InitiatorReservation",
				$"reserved={reserved} errorOnFailed={errorOnFailed} job={MutualPlayDiagnostics.DescribeJob(job)}",
				Partner);
			return reserved;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(PartnerInd);
			this.FailOn(() => FailIf(
				"initiator became invalid",
				pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn)));
			this.FailOn(() => FailIf(
				"partner became null/downed/drafted/mental-state blocked",
				Partner == null || Partner.Downed || Partner.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(Partner)));
			this.FailOn(() => FailIf("initiator and partner are on different maps", Partner?.Map != pawn.Map));
			this.FailOn(() => FailIf(
				"partner job is no longer committed to this initiator",
				_partnerJobStarted && !IsPartnerStillCommitted()));

			// Step 1: Start partner job first (partner will stop moving and wait)
			Toil startPartnerJob = ToilMaker.MakeToil("StartPartnerJob");
			startPartnerJob.initAction = () =>
			{
				MutualPlayDiagnostics.Log(
					pawn,
					"StartPartnerJob",
					$"entering start toil; partnerJobStarted={_partnerJobStarted}",
					Partner);
				if (!_partnerJobStarted)
				{
					_partnerJobStarted = true;
					if (!TryStartPartnerJob())
					{
						LogFailure("TryStartPartnerJob returned false");
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
				MutualPlayDiagnostics.Log(
					pawn,
					"InitiatorPlayStarted",
					$"arrived at partner; distance={DistanceToPartner():0.00}",
					Partner);
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
					MutualPlayDiagnostics.Log(pawn, "InitiatorSatisfied", "play need satisfied", Partner);
					EndJobWith(JobCondition.Succeeded);
				}
			};
			play.handlingFacing = true;
			play.defaultCompleteMode = ToilCompleteMode.Delay;
			play.defaultDuration = job.def.joyDuration;
			play.AddFinishAction(() => ToddlerPlayAnimationUtility.ClearAnimation(pawn, _playAnimation));

			AddFinishAction(condition =>
			{
				MutualPlayDiagnostics.Log(
					pawn,
					"InitiatorFinished",
					$"condition={condition} partnerJobStarted={_partnerJobStarted} partnerCommitted={IsPartnerStillCommitted()}",
					Partner);
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
				MutualPlayDiagnostics.Log(pawn, "PartnerJobRejected", "partner or partner.jobs is null", Partner);
				return false;
			}

			if (!pawn.CanReach(Partner, PathEndMode.Touch, Danger.Some))
			{
				MutualPlayDiagnostics.Log(pawn, "PartnerJobRejected", "initiator cannot reach partner", Partner);
				return false;
			}

			JobDef partnerJobDef = ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayPartnerJob;
			if (partnerJobDef == null)
			{
				MutualPlayDiagnostics.Log(pawn, "PartnerJobRejected", "partner JobDef is null", Partner);
				return false;
			}

			Job currentPartnerJob = Partner.CurJob;
			if (currentPartnerJob?.def == partnerJobDef)
			{
				bool alreadyCommitted = currentPartnerJob.targetA.Thing == pawn;
				MutualPlayDiagnostics.Log(
					pawn,
					"PartnerAlreadyInJob",
					$"alreadyCommitted={alreadyCommitted} currentPartnerJob={MutualPlayDiagnostics.DescribeJob(currentPartnerJob)}",
					Partner);
				return alreadyCommitted;
			}

			Job partnerJob = JobMaker.MakeJob(partnerJobDef, pawn);
			partnerJob.ignoreJoyTimeAssignment = true;
			partnerJob.expiryInterval = job.expiryInterval > 0 ? job.expiryInterval : partnerJob.def.joyDuration;
			if (job.targetB.IsValid)
			{
				partnerJob.targetB = job.targetB;
			}
			bool accepted = Partner.jobs.TryTakeOrderedJob(partnerJob);
			MutualPlayDiagnostics.Log(
				pawn,
				"PartnerJobOrdered",
				$"accepted={accepted} requested={MutualPlayDiagnostics.DescribeJob(partnerJob)} " +
				$"actualCurrent={MutualPlayDiagnostics.DescribeJob(Partner.CurJob)}",
				Partner);
			return accepted;
		}

		private bool FailIf(string reason, bool failed)
		{
			if (failed)
			{
				LogFailure(reason);
			}

			return failed;
		}

		private void LogFailure(string reason)
		{
			if (_failureLogged)
			{
				return;
			}

			_failureLogged = true;
			MutualPlayDiagnostics.Log(
				pawn,
				"InitiatorFailure",
				$"reason={reason} initiatorJob={MutualPlayDiagnostics.DescribeJob(pawn?.CurJob)} " +
				$"partnerJob={MutualPlayDiagnostics.DescribeJob(Partner?.CurJob)} distance={DistanceToPartner():0.00}",
				Partner);
		}

		private float DistanceToPartner()
		{
			return pawn?.Spawned == true && Partner?.Spawned == true && pawn.Map == Partner.Map
				? pawn.Position.DistanceTo(Partner.Position)
				: -1f;
		}

		private static float GetPlayLevel(Pawn pawn)
		{
			if (pawn?.needs?.play != null)
			{
				return pawn.needs.play.CurLevelPercentage;
			}

			return pawn?.needs?.joy?.CurLevelPercentage ?? -1f;
		}

		private bool IsPartnerStillCommitted()
		{
			if (Partner == null)
			{
				return false;
			}

			Job curJob = Partner.CurJob;
			return curJob?.def == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayPartnerJob
				&& curJob.targetA.Thing == pawn;
		}
	}
}
