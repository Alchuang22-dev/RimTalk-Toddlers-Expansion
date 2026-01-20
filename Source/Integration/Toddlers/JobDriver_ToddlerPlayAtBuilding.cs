using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class JobDriver_ToddlerPlayAtBuilding : JobDriver
	{
		private const TargetIndex ToyInd = TargetIndex.A;
		private const float DefaultJoyGainPerTick = 0.0002f;

		private AnimationDef _playAnimation;

		private Building Toy => TargetA.Thing as Building;

		private CompToddlerToy ToyComp => Toy?.TryGetComp<CompToddlerToy>();

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (Toy == null)
			{
				return false;
			}

			return pawn.Reserve(Toy, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(ToyInd);
			this.FailOn(() => pawn.Downed || pawn.Drafted || pawn.InMentalState);

			yield return Toils_Goto.GotoThing(ToyInd, PathEndMode.InteractionCell);

			Toil play = ToilMaker.MakeToil("ToddlerPlayAtToy");
			play.initAction = () =>
			{
				if (ToddlersCompatUtility.IsToddlerOrBaby(pawn))
				{
					_playAnimation = ToddlerPlayAnimationUtility.GetRandomSelfPlayAnimation();
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}
			};
			play.tickIntervalAction = delta =>
			{
				if (ToyComp == null)
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				if (ToddlersCompatUtility.IsToddlerOrBaby(pawn))
				{
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}

				pawn.rotationTracker.FaceCell(Toy.Position);
				pawn.GainComfortFromCellIfPossible(delta);
				ApplyToyJoy(pawn, ToyComp, delta);

				if (SocialNeedTuning_Toddlers.IsPlayNeedSatisfied(pawn))
				{
					EndJobWith(JobCondition.Succeeded);
				}
			};
			play.handlingFacing = true;
			play.defaultCompleteMode = ToilCompleteMode.Delay;
			play.defaultDuration = job.expiryInterval > 0 ? job.expiryInterval : job.def.joyDuration;

			AddFinishAction(condition =>
			{
				ToddlerPlayAnimationUtility.ClearAnimation(pawn, _playAnimation);
				if (condition == JobCondition.Succeeded && ToddlersCompatUtility.IsToddler(pawn))
				{
					ToddlerPlayDialogueEvents.OnToddlerSelfPlayCompleted(pawn, job, Map);
				}
			});

			yield return play;
		}

		private static void ApplyToyJoy(Pawn pawn, CompToddlerToy toy, int delta)
		{
			if (pawn == null)
			{
				return;
			}

			if (ToddlersCompatUtility.IsToddler(pawn))
			{
				SocialNeedTuning_Toddlers.ApplySelfPlayTickEffects(pawn, delta);
				return;
			}

			float gain = Mathf.Max(0f, (toy?.JoyGainPerTick ?? DefaultJoyGainPerTick) * delta);
			if (gain <= 0f)
			{
				return;
			}

			Need_Play play = pawn.needs?.play;
			if (play != null)
			{
				play.Play(gain);
				return;
			}

			Need_Joy joy = pawn.needs?.joy;
			if (joy != null)
			{
				joy.GainJoy(gain, toy?.JoyKind ?? JoyKindDefOf.Meditative);
			}
		}
	}
}
