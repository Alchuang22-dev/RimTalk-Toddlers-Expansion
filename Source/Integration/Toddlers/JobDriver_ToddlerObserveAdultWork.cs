using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class JobDriver_ToddlerObserveAdultWork : JobDriver
	{
		private const int FollowDistance = 4;
		private const int MaxConsecutiveTicksNoSkillJob = 5;
		private const float JoyGainPerTick = 0.0001f;
		private int _consecutiveTicksNoSkillJob;
		private int _lastDebugLogTick = -99999;

		private Pawn AdultToFollow => (Pawn)TargetA;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.A);
			yield return FollowAndObserve();
		}

		private Toil FollowAndObserve()
		{
			Toil toil = new Toil();
			toil.defaultCompleteMode = ToilCompleteMode.Never;
			toil.tickIntervalAction = delta =>
			{
				Pawn adult = AdultToFollow;
				if (adult == null || adult.Destroyed || adult.Dead || !adult.Spawned)
				{
					LogDebug("adult_missing");
					EndJobWith(JobCondition.Succeeded);
					return;
				}

				if (!ToddlerCanLearnFromAdult(pawn, adult))
				{
					_consecutiveTicksNoSkillJob++;
					if (_consecutiveTicksNoSkillJob >= MaxConsecutiveTicksNoSkillJob)
					{
						LogDebug("adult_not_eligible");
						EndJobWith(JobCondition.Succeeded);
					}
					return;
				}

				if (IsDoingInterestingJob(adult))
				{
					_consecutiveTicksNoSkillJob = 0;
					GainJoy(delta);
					MoveToObservationPosition(adult);
				}
				else if (adult.CurJob?.def == JobDefOf.Goto)
				{
					_consecutiveTicksNoSkillJob = 0;
					MoveToObservationPosition(adult);
				}
				else
				{
					_consecutiveTicksNoSkillJob++;
					if (_consecutiveTicksNoSkillJob >= MaxConsecutiveTicksNoSkillJob)
					{
						LogDebug("adult_not_interesting");
						EndJobWith(JobCondition.Succeeded);
					}
				}
			};

			toil.AddEndCondition(() =>
			{
				if (!ToddlersCompatUtility.IsToddler(pawn))
				{
					LogDebug("not_toddler");
					return JobCondition.Incompletable;
				}

				if (PawnUtility.WillSoonHaveBasicNeed(pawn, -0.05f))
				{
					LogDebug("basic_need");
					return JobCondition.Incompletable;
				}

				return JobCondition.Ongoing;
			});
			toil.AddEndCondition(() =>
			{
				if (pawn.needs?.joy?.CurLevel > 0.9f)
				{
					LogDebug("joy_full");
					return JobCondition.Incompletable;
				}

				return JobCondition.Ongoing;
			});
			toil.AddEndCondition(() =>
			{
				if (pawn.needs?.food?.CurLevel < 0.1f)
				{
					LogDebug("too_hungry");
					return JobCondition.Incompletable;
				}

				return JobCondition.Ongoing;
			});

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

			if (adult.IsForbidden(toddler))
			{
				return false;
			}

			if (!adult.Awake() || adult.IsPrisonerOfColony || adult.IsPrisoner)
			{
				return false;
			}

			if (!toddler.CanReach(adult, PathEndMode.Touch, Danger.Some))
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

		private bool IsDoingInterestingJob(Pawn adult)
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

			return true;
		}

		private void GainJoy(int delta)
		{
			if (pawn.needs?.joy != null)
			{
				pawn.needs.joy.CurLevel += JoyGainPerTick * delta;
			}
		}

		private void MoveToObservationPosition(Pawn adult)
		{
			if (pawn.pather.Moving)
			{
				return;
			}

			IntVec3 cell = GetObservationCell(adult);
			if (cell.IsValid && cell != pawn.Position && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some))
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

		private void LogDebug(string reason)
		{
			if (!Prefs.DevMode)
			{
				return;
			}

			int now = Find.TickManager?.TicksGame ?? 0;
			if (now == _lastDebugLogTick)
			{
				return;
			}

			_lastDebugLogTick = now;
			Log.Message($"[RimTalk_ToddlersExpansion] ObserveAdultWork end: pawn={pawn?.LabelShort ?? "null"} reason={reason} adult={AdultToFollow?.LabelShort ?? "null"}");
		}
	}
}
