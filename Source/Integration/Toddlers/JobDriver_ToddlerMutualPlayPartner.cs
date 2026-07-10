using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.YayoAnimation;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class JobDriver_ToddlerMutualPlayPartner : JobDriver
	{
		private const TargetIndex InitiatorInd = TargetIndex.A;
		private const int MaxPartnerDistance = 6;
		private bool _failureLogged;
		private AnimationDef _playAnimation;

		private Pawn Initiator => TargetA.Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			MutualPlayDiagnostics.Log(
				pawn,
				"PartnerReservation",
				$"no reservation required; errorOnFailed={errorOnFailed} job={MutualPlayDiagnostics.DescribeJob(job)}",
				Initiator);
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(InitiatorInd);
			this.FailOn(() => FailIf(
				"partner became invalid",
				pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn)));
			this.FailOn(() => FailIf(
				"initiator became null/downed/drafted/mental-state blocked",
				Initiator == null || Initiator.Downed || Initiator.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(Initiator)));
			this.FailOn(() => FailIf("partner and initiator are on different maps", Initiator?.Map != pawn.Map));

			// Step 1: Wait for initiator to arrive (stop moving and stay in place)
			Toil waitForInitiator = ToilMaker.MakeToil("WaitForInitiator");
			waitForInitiator.initAction = () =>
			{
				pawn.pather.StopDead();
				MutualPlayDiagnostics.Log(
					pawn,
					"PartnerWaiting",
					$"stopped pathing; distance={DistanceToInitiator():0.00}",
					Initiator);
			};
			waitForInitiator.tickAction = () =>
			{
				if (!IsInitiatorCommitted(requireReachability: true, out string failureReason))
				{
					LogFailure($"waiting: {failureReason}");
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				pawn.rotationTracker.FaceCell(Initiator.Position);

				// Check if initiator has arrived close enough, then proceed to next toil
				if (pawn.Position.InHorDistOf(Initiator.Position, 2))
				{
					MutualPlayDiagnostics.Log(
						pawn,
						"PartnerWaitCompleted",
						$"initiator arrived; distance={DistanceToInitiator():0.00}",
						Initiator);
					ReadyForNextToil();
				}
			};
			waitForInitiator.handlingFacing = true;
			waitForInitiator.defaultCompleteMode = ToilCompleteMode.Never;
			yield return waitForInitiator;

			// Step 2: Play together
			Toil play = ToilMaker.MakeToil("ToddlerMutualPlayPartner");
			play.initAction = () =>
			{
				MutualPlayDiagnostics.Log(
					pawn,
					"PartnerPlayStarted",
					$"entering play toil; distance={DistanceToInitiator():0.00}",
					Initiator);
				if (YayoAnimationCompatUtility.TryGetNativePlayAnimationOverride(pawn, out AnimationDef nativeAnimation))
				{
					_playAnimation = nativeAnimation;
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}
				else if (!YayoAnimationCompatUtility.ShouldUseYayoPlayAnimation(pawn))
				{
					_playAnimation = ToddlerPlayAnimationUtility.GetSharedMutualPlayAnimation(pawn, Initiator);
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}
			};
			play.tickAction = () =>
			{
				if (_playAnimation != null && pawn.Drawer?.renderer?.CurAnimation != _playAnimation)
				{
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}

				if (!IsInitiatorCommitted(requireReachability: false, out string failureReason))
				{
					LogFailure($"playing: {failureReason}");
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				if (!pawn.Position.InHorDistOf(Initiator.Position, MaxPartnerDistance))
				{
					LogFailure(
						$"playing: distance {DistanceToInitiator():0.00} exceeds maximum {MaxPartnerDistance}");
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				pawn.rotationTracker.FaceCell(Initiator.Position);
				pawn.GainComfortFromCellIfPossible(1);

				if (SocialNeedTuning_Toddlers.IsPlayNeedSatisfied(pawn))
				{
					MutualPlayDiagnostics.Log(pawn, "PartnerSatisfied", "play need satisfied", Initiator);
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
					"PartnerFinished",
					$"condition={condition} initiatorCommitted={IsInitiatorCommitted(false, out string reason)} " +
					$"commitmentReason={reason}",
					Initiator);
			});

			yield return play;
		}

		private bool IsInitiatorCommitted(bool requireReachability, out string reason)
		{
			if (Initiator == null)
			{
				reason = "initiator is null";
				return false;
			}

			Job initiatorJob = Initiator.CurJob;
			if (initiatorJob?.def != ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob)
			{
				reason = $"initiator current job is {MutualPlayDiagnostics.DescribeJob(initiatorJob)}";
				return false;
			}

			if (initiatorJob.targetA.Thing != pawn)
			{
				reason = $"initiator targetA is {initiatorJob.targetA}";
				return false;
			}

			if (requireReachability && !Initiator.CanReach(pawn, PathEndMode.Touch, Danger.Some))
			{
				reason = "initiator cannot reach partner";
				return false;
			}

			reason = "committed";
			return true;
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
				"PartnerFailure",
				$"reason={reason} partnerJob={MutualPlayDiagnostics.DescribeJob(pawn?.CurJob)} " +
				$"initiatorJob={MutualPlayDiagnostics.DescribeJob(Initiator?.CurJob)} distance={DistanceToInitiator():0.00}",
				Initiator);
		}

		private float DistanceToInitiator()
		{
			return pawn?.Spawned == true && Initiator?.Spawned == true && pawn.Map == Initiator.Map
				? pawn.Position.DistanceTo(Initiator.Position)
				: -1f;
		}
	}
}
