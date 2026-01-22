using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class JobDriver_ToddlerObserveAdultWork : JobDriver
	{
		private const int FollowDistance = 4;
		private const int MaxConsecutiveTicksNoSkillJob = 5;
		private const float JoyGainPerTick = 0.0001f;

		private Pawn AdultToFollow => (Pawn)TargetA;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			yield return FollowAndObserve();
		}

		private Toil FollowAndObserve()
		{
			Toil toil = new Toil();
			toil.defaultCompleteMode = ToilCompleteMode.Delay;
			toil.defaultDuration = MaxConsecutiveTicksNoSkillJob;
			toil.tickAction = () =>
			{
				Pawn adult = AdultToFollow;
				if (adult == null || adult.Destroyed || adult.Dead || !adult.Spawned)
				{
					ReadyForNextToil();
					return;
				}

				if (!ToddlerCanLearnFromAdult(pawn, adult))
				{
					ReadyForNextToil();
					return;
				}

				if (IsDoingSkillJob(adult))
				{
					GainJoy();
					MoveToObservationPosition(adult);
				}
				else if (adult.CurJob?.def == JobDefOf.Goto)
				{
					MoveToObservationPosition(adult);
				}
				else
				{
					ReadyForNextToil();
				}
			};

			toil.AddFailCondition(() => !ToddlersCompatUtility.IsToddler(pawn) || pawn.pather.Moving);
			toil.AddFailCondition(() => PawnUtility.WillSoonHaveBasicNeed(pawn, -0.05f));
			toil.AddFailCondition(() => pawn.needs?.joy?.CurLevel > 0.9f);
			toil.AddEndCondition(() => pawn.needs?.food?.CurLevel < 0.1f ? JobCondition.Incompletable : JobCondition.Ongoing);

			return toil;
		}

		private bool ToddlerCanLearnFromAdult(Pawn toddler, Pawn adult)
		{
			if (adult == null || toddler == null || adult.Dead || !adult.Spawned || adult.Destroyed)
			{
				return false;
			}

			if (adult.DevelopmentalStage.Juvenile() || adult == toddler)
			{
				return false;
			}

			if (!adult.Awake() || adult.IsPrisonerOfColony || adult.IsPrisoner)
			{
				return false;
			}

			float num = adult.Position.DistanceTo(toddler.Position);
			if (num > 30f)
			{
				return false;
			}

			return true;
		}

		private bool IsDoingSkillJob(Pawn adult)
		{
			Job curJob = adult.CurJob;
			if (curJob == null)
			{
				return false;
			}

			JobDef def = curJob.def;
			if (def == JobDefOf.Wait_Wander || def == JobDefOf.Wait_MaintainPosture)
			{
				return false;
			}

			RecipeDef recipe = curJob.RecipeDef;
			if (recipe?.workSkill != null)
			{
				return true;
			}

			List<SkillDef> relevantSkills = curJob.workGiverDef?.workType?.relevantSkills;
			return !relevantSkills.NullOrEmpty();
		}

		private void GainJoy()
		{
			if (pawn.needs?.joy != null)
			{
				pawn.needs.joy.CurLevel += JoyGainPerTick;
			}
		}

		private void MoveToObservationPosition(Pawn adult)
		{
			if (pawn.pather.Moving)
			{
				return;
			}

			IntVec3 cell = GetObservationCell(adult);
			if (cell.IsValid && cell != pawn.Position)
			{
				pawn.pather.StartPath(cell, PathEndMode.OnCell);
			}
		}

		private IntVec3 GetObservationCell(Pawn adult)
		{
			CellRect cellRect = CellRect.CenteredOn(adult.Position, FollowDistance);
			cellRect.ClipInsideMap(adult.Map);

			IntVec3 bestCell = IntVec3.Invalid;
			float bestDistance = float.MaxValue;

			foreach (IntVec3 cell in cellRect.Cells)
			{
				if (!cell.Standable(adult.Map) || cell.IsForbidden(pawn) || !pawn.CanReserve(cell))
				{
					continue;
				}

				float distance = cell.DistanceToSquared(adult.Position);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					bestCell = cell;
				}
			}

			return bestCell;
		}
	}
}
