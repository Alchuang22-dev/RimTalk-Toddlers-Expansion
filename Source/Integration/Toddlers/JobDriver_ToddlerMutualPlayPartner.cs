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
		private const int InitiatorCommitGraceTicks = 30;
		private AnimationDef _playAnimation;
		private int _initiatorCommitGraceTicks;

		private Pawn Initiator => TargetA.Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(InitiatorInd);
			this.FailOn(() => !HasBasicValidInitiator());

			// Step 1: Wait for initiator to arrive (stop moving and stay in place)
			Toil waitForInitiator = ToilMaker.MakeToil("WaitForInitiator");
			waitForInitiator.initAction = () =>
			{
				pawn.pather.StopDead();
			};
			waitForInitiator.tickAction = () =>
			{
				if (!HasBasicValidInitiator())
				{
					ToddlerPlayGiver_MutualPlay.StartFailureCooldown(pawn, Initiator);
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				if (!IsInitiatorCommittedToThisPlay())
				{
					_initiatorCommitGraceTicks++;
					if (_initiatorCommitGraceTicks >= InitiatorCommitGraceTicks)
					{
						ToddlerPlayGiver_MutualPlay.StartFailureCooldown(pawn, Initiator);
						EndJobWith(JobCondition.Incompletable);
					}

					return;
				}

				_initiatorCommitGraceTicks = 0;
				Pawn initiator = Initiator;
				pawn.rotationTracker.FaceCell(initiator.Position);

				// Check if initiator has arrived close enough, then proceed to next toil
				if (pawn.Position.InHorDistOf(initiator.Position, 2))
				{
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
				if (!HasBasicValidInitiator() || !IsInitiatorCommittedToThisPlay())
				{
					ToddlerPlayGiver_MutualPlay.StartFailureCooldown(pawn, Initiator);
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				if (_playAnimation != null && pawn.Drawer?.renderer?.CurAnimation != _playAnimation)
				{
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}

				Pawn initiator = Initiator;
				if (!pawn.Position.InHorDistOf(initiator.Position, MaxPartnerDistance))
				{
					ToddlerPlayGiver_MutualPlay.StartFailureCooldown(pawn, initiator);
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				pawn.rotationTracker.FaceCell(initiator.Position);
				pawn.GainComfortFromCellIfPossible(1);

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
				if (condition != JobCondition.Succeeded)
				{
					ToddlerPlayGiver_MutualPlay.StartFailureCooldown(pawn, Initiator);
				}
			});

			yield return play;
		}

		private bool HasBasicValidInitiator()
		{
			Pawn initiator = Initiator;
			if (pawn?.Map == null || initiator?.Map == null || initiator.Map != pawn.Map)
			{
				return false;
			}

			if (pawn.Downed || pawn.Drafted || !pawn.Awake() || ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
			{
				return false;
			}

			if (initiator.Downed || initiator.Drafted || !initiator.Awake() || ToddlerMentalStateUtility.HasBlockingMentalState(initiator))
			{
				return false;
			}

			return true;
		}

		private bool IsInitiatorCommittedToThisPlay()
		{
			Pawn initiator = Initiator;
			Job initiatorJob = initiator.CurJob;
			return initiatorJob?.def == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob
				&& initiatorJob.targetA.Thing == pawn;
		}
	}
}
