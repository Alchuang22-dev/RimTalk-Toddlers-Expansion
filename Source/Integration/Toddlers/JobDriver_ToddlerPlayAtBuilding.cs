using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimTalk_ToddlersExpansion.Integration.YayoAnimation;
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
		private static ThingDef _toyBoxDef;
		private static ThingDef _babyDecorationDef;

		private Building Toy => TargetA.Thing as Building;

		private CompToddlerToy ToyComp => Toy?.TryGetComp<CompToddlerToy>();

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (Toy == null || !IsSupportedToy(Toy))
			{
				return false;
			}

			return pawn.Reserve(Toy, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(ToyInd);
			this.FailOn(() => pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn));
			this.FailOn(() => Toy == null || !IsSupportedToy(Toy));

			yield return Toils_Goto.GotoThing(ToyInd, GetToyPathEndMode(Toy));

			Toil play = ToilMaker.MakeToil("ToddlerPlayAtToy");
			play.initAction = () =>
			{
				if (YayoAnimationCompatUtility.TryGetNativePlayAnimationOverride(pawn, out AnimationDef nativeAnimation))
				{
					_playAnimation = nativeAnimation;
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}
				else if (ShouldUseCustomPlayAnimation(pawn))
				{
					_playAnimation = ToddlerPlayAnimationUtility.GetRandomSelfPlayAnimation(pawn);
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}
			};
			play.tickIntervalAction = delta =>
			{
				if (Toy == null || !IsSupportedToy(Toy))
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				if (_playAnimation != null)
				{
					if (pawn.Drawer?.renderer?.CurAnimation != _playAnimation)
					{
						ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
					}
				}

				ToddlerPlayEffectUtility.ApplyPlayEffects(pawn, pawn.Map);

				pawn.rotationTracker.FaceCell(Toy.Position);
				pawn.GainComfortFromCellIfPossible(delta);
				ApplyToyJoy(pawn, ToyComp, delta);
			};
			play.handlingFacing = true;
			play.defaultCompleteMode = ToilCompleteMode.Delay;
			play.defaultDuration = job.expiryInterval > 0 ? job.expiryInterval : job.def.joyDuration;

			AddFinishAction(condition =>
			{
				if (_playAnimation != null)
				{
					ToddlerPlayAnimationUtility.ClearAnimation(pawn, _playAnimation);
				}

				ToddlerPlayEffectUtility.ClearEffects();
				if (condition == JobCondition.Succeeded && ToddlersCompatUtility.IsToddler(pawn))
				{
					ToddlerPlayDialogueEvents.OnToddlerSelfPlayCompleted(pawn, job, Map);
				}
			});

			yield return play;
		}

		private static bool IsSupportedToy(Building toy)
		{
			if (toy == null)
			{
				return false;
			}

			if (toy.TryGetComp<CompToddlerToy>() != null)
			{
				return true;
			}

			EnsureExternalToyDefs();
			return toy.def == _toyBoxDef || toy.def == _babyDecorationDef;
		}

		private static PathEndMode GetToyPathEndMode(Building toy)
		{
			if (toy == null)
			{
				return PathEndMode.Touch;
			}

			if (toy.TryGetComp<CompToddlerToy>() != null)
			{
				return PathEndMode.InteractionCell;
			}

			EnsureExternalToyDefs();
			if (toy.def == _babyDecorationDef && ShouldPlayDecorOnCell(toy))
			{
				return PathEndMode.OnCell;
			}

			return PathEndMode.Touch;
		}

		private static bool ShouldPlayDecorOnCell(Thing decor)
		{
			return decor != null && (decor.thingIDNumber % 5 == 0);
		}

		private static void EnsureExternalToyDefs()
		{
			_toyBoxDef ??= DefDatabase<ThingDef>.GetNamedSilentFail("ToyBox");
			_babyDecorationDef ??= DefDatabase<ThingDef>.GetNamedSilentFail("BabyDecoration");
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

		private static bool ShouldUseCustomPlayAnimation(Pawn pawn)
		{
			if (!ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return false;
			}

			return !YayoAnimationCompatUtility.ShouldUseYayoPlayAnimation(pawn);
		}
	}
}
