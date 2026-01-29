using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class JobDriver_ToddlerSelfBath : JobDriver
	{
		private const TargetIndex BathTargetInd = TargetIndex.A;
		private const int BathDurationTicks = 1600;
		private const int WashDurationTicks = 1400;
		private const float HygieneGainPerTickBath = 0.0006f;
		private const float HygieneGainPerTickWash = 0.0005f;

		private static HediffDef _washingHediff;
		private static bool _hediffChecked;
		private static EffecterDef _washingEffect;
		private static bool _effectChecked;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			LocalTargetInfo target = job.GetTarget(BathTargetInd);
			if (!target.IsValid)
			{
				return false;
			}

			if (target.HasThing && target.Thing.Spawned)
			{
				return pawn.Reserve(target.Thing, job, 1, -1, null, errorOnFailed);
			}

			if (target.Cell.IsValid)
			{
				return pawn.Reserve(target.Cell, job, 1, -1, null, errorOnFailed);
			}

			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(() => pawn == null || pawn.Map == null);
			this.FailOn(() => pawn.Downed || pawn.Drafted || pawn.InMentalState);
			this.FailOn(() => !ToddlersCompatUtility.IsToddler(pawn));
			this.FailOn(() => !ToddlersCompatUtility.CanSelfCare(pawn));
			this.FailOn(() => ToddlerSelfBathUtility.GetHygieneNeed(pawn) == null);

			LocalTargetInfo target = job.GetTarget(BathTargetInd);
			bool isBath = ToddlerSelfBathUtility.IsBathFixture(target.Thing);
			bool targetIsSpawnedThing = target.HasThing && target.Thing.Spawned;

			if (targetIsSpawnedThing)
			{
				yield return Toils_Goto.GotoThing(BathTargetInd, PathEndMode.Touch);
			}
			else if (target.Cell.IsValid)
			{
				yield return Toils_Goto.GotoCell(BathTargetInd, PathEndMode.ClosestTouch);
			}

			Toil bath = ToilMaker.MakeToil("ToddlerSelfBath");
			EnsureWashingEffect();
			if (_washingEffect != null)
			{
				bath.WithEffect(_washingEffect, BathTargetInd);
			}
			bath.defaultCompleteMode = ToilCompleteMode.Delay;
			bath.defaultDuration = isBath ? BathDurationTicks : WashDurationTicks;
			bath.handlingFacing = true;
			bath.initAction = () =>
			{
				if (isBath)
				{
					pawn.jobs.posture = PawnPosture.LayingOnGroundFaceUp;
					if (targetIsSpawnedThing && ToddlerSelfBathUtility.TryGetBathLayCell(target.Thing, out IntVec3 bathCell))
					{
						pawn.pather?.StopDead();
						pawn.Position = bathCell;
						pawn.Notify_Teleported();
					}
				}

				EnsureWashingHediff();
				if (_washingHediff != null && pawn.health?.hediffSet != null && !pawn.health.hediffSet.HasHediff(_washingHediff))
				{
					pawn.health.AddHediff(_washingHediff);
				}
			};
			bath.tickIntervalAction = delta =>
			{
				Need hygiene = ToddlerSelfBathUtility.GetHygieneNeed(pawn);
				if (hygiene != null)
				{
					float gain = (isBath ? HygieneGainPerTickBath : HygieneGainPerTickWash) * delta;
					hygiene.CurLevel = Mathf.Min(hygiene.CurLevel + gain, 1f);
				}

				SocialNeedTuning_Toddlers.ApplySelfPlayTickEffects(pawn, delta);
				if (hygiene != null && hygiene.CurLevel >= 0.999f)
				{
					EndJobWith(JobCondition.Succeeded);
				}
			};
			bath.AddFinishAction(() =>
			{
				if (_washingHediff != null && pawn.health?.hediffSet != null)
				{
					Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(_washingHediff);
					if (hediff != null)
					{
						pawn.health.RemoveHediff(hediff);
					}
				}

				Need hygiene = ToddlerSelfBathUtility.GetHygieneNeed(pawn);
				if (hygiene != null && hygiene.CurLevel >= 0.999f
					&& pawn.needs?.mood?.thoughts?.memories != null
					&& ToddlersExpansionThoughtDefOf.RimTalk_ToddlerPlayedInWater != null)
				{
					pawn.needs.mood.thoughts.memories.TryGainMemory(ToddlersExpansionThoughtDefOf.RimTalk_ToddlerPlayedInWater);
				}
			});

			yield return bath;
		}

		private static void EnsureWashingHediff()
		{
			if (_hediffChecked)
			{
				return;
			}

			_hediffChecked = true;
			_washingHediff = DefDatabase<HediffDef>.GetNamedSilentFail("Washing");
		}

		private static void EnsureWashingEffect()
		{
			if (_effectChecked)
			{
				return;
			}

			_effectChecked = true;
			_washingEffect = DefDatabase<EffecterDef>.GetNamedSilentFail("WashingEffect");
		}
	}
}
