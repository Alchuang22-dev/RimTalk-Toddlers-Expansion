using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Toddlers;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class ToddlerPlayGiver_SelfPlay : ToddlerPlayGiver
	{
		private const float PlayNeedThreshold = 0.92f;
		private const int SearchRadius = 6;

		public override bool CanDo(Pawn pawn)
		{
			if (!IsEligiblePawn(pawn))
			{
				return false;
			}

			Need_Play play = pawn.needs?.play;
			if (play != null && play.CurLevelPercentage >= PlayNeedThreshold)
			{
				return false;
			}

			return TryFindPlaySpot(pawn, out _);
		}

		public override Job TryGiveJob(Pawn pawn)
		{
			if (!IsEligiblePawn(pawn))
			{
				return null;
			}

			if (!TryFindPlaySpot(pawn, out IntVec3 spot))
			{
				return null;
			}

			Job job = JobMaker.MakeJob(def.jobDef, spot);
			job.ignoreJoyTimeAssignment = true;
			job.expiryInterval = 2000;
			return job;
		}

		private static bool IsEligiblePawn(Pawn pawn)
		{
			if (pawn?.Map == null || pawn.needs?.play == null || !ToddlersCompatUtility.IsEligibleForSelfPlay(pawn))
			{
				return false;
			}

			if (pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
			{
				return false;
			}

			return !PawnUtility.WillSoonHaveBasicNeed(pawn, 0f);
		}

		private static bool TryFindPlaySpot(Pawn pawn, out IntVec3 spot)
		{
			Map map = pawn.Map;
			IntVec3 root = pawn.Position;
			return CellFinder.TryFindRandomCellNear(root, map, SearchRadius, cell =>
			{
				if (!cell.Standable(map) || cell.IsForbidden(pawn))
				{
					return false;
				}

				return pawn.CanReserveSittableOrSpot(cell);
			}, out spot);
		}
	}

	public sealed class ToddlerPlayGiver_MutualPlay : ToddlerPlayGiver
	{
		private const float PlayNeedThreshold = 0.85f;
		private const int PartnerSearchRadius = 10;
		private const int SpotSearchRadius = 6;

		public override bool CanDo(Pawn pawn)
		{
			if (!IsEligiblePawn(pawn))
			{
				return false;
			}

			Need_Play play = pawn.needs?.play;
			if (play != null && play.CurLevelPercentage >= PlayNeedThreshold)
			{
				return false;
			}

			return FindPartner(pawn) != null;
		}

		public override Job TryGiveJob(Pawn pawn)
		{
			if (!IsEligiblePawn(pawn))
			{
				return null;
			}

			Need_Play play = pawn.needs?.play;
			if (play != null && play.CurLevelPercentage >= PlayNeedThreshold)
			{
				return null;
			}

			Pawn partner = FindPartner(pawn);
			if (partner == null)
			{
				return null;
			}

			Job job = JobMaker.MakeJob(def.jobDef, partner);
			job.ignoreJoyTimeAssignment = true;
			job.expiryInterval = 2000;

			if (TryFindPlaySpot(pawn, out IntVec3 spot))
			{
				job.targetB = spot;
			}

			return job;
		}

		private static bool IsEligiblePawn(Pawn pawn)
		{
			if (pawn?.Map == null || pawn.needs?.play == null || !ToddlersCompatUtility.IsEligibleForSelfPlay(pawn))
			{
				return false;
			}

			if (pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn) || !pawn.Awake())
			{
				return false;
			}

			return true;
		}

		private static Pawn FindPartner(Pawn pawn)
		{
			Map map = pawn.Map;
			var pawns = pawn.Faction != null
				? map.mapPawns.SpawnedPawnsInFaction(pawn.Faction)
				: map.mapPawns.AllPawnsSpawned;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn other = pawns[i];
				if (other == pawn)
				{
					continue;
				}

				if (!ToddlersCompatUtility.IsToddler(other))
				{
					continue;
				}

				if (other.Downed || other.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(other) || !other.Awake())
				{
					continue;
				}

				if (!pawn.Position.InHorDistOf(other.Position, PartnerSearchRadius))
				{
					continue;
				}

				Need_Play otherPlay = other.needs?.play;
				if (otherPlay != null && otherPlay.CurLevelPercentage >= PlayNeedThreshold)
				{
					continue;
				}

				if (!pawn.CanReserve(other))
				{
					continue;
				}

				return other;
			}

			return null;
		}

		private static bool TryFindPlaySpot(Pawn pawn, out IntVec3 spot)
		{
			Map map = pawn.Map;
			IntVec3 root = pawn.Position;
			return CellFinder.TryFindRandomCellNear(root, map, SpotSearchRadius, cell =>
			{
				if (!cell.Standable(map) || cell.IsForbidden(pawn))
				{
					return false;
				}

				return pawn.CanReserveSittableOrSpot(cell);
			}, out spot);
		}
	}

	public sealed class ToddlerPlayGiver_PlayAtToy : ToddlerPlayGiver
	{
		private const float PlayNeedThreshold = 0.88f;
		private const float SearchRadius = 30f;

		private static bool _walkHediffChecked;
		private static HediffDef _learningToWalkDef;

		public override bool CanDo(Pawn pawn)
		{
			if (!IsEligiblePawn(pawn))
			{
				return false;
			}

			Need_Play play = pawn.needs?.play;
			if (play != null && play.CurLevelPercentage >= PlayNeedThreshold)
			{
				return false;
			}

			return FindBestToy(pawn) != null;
		}

		public override Job TryGiveJob(Pawn pawn)
		{
			if (!IsEligiblePawn(pawn))
			{
				return null;
			}

			Thing toy = FindBestToy(pawn);
			if (toy == null)
			{
				return null;
			}

			CompToddlerToy comp = toy.TryGetComp<CompToddlerToy>();
			Job job = JobMaker.MakeJob(def.jobDef, toy);
			job.ignoreJoyTimeAssignment = false;
			if (comp != null && comp.UseDurationTicks > 0)
			{
				job.expiryInterval = comp.UseDurationTicks;
			}

			return job;
		}

		private static bool IsEligiblePawn(Pawn pawn)
		{
			if (pawn?.Map == null || pawn.needs?.play == null || !ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			if (pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
			{
				return false;
			}

			return !PawnUtility.WillSoonHaveBasicNeed(pawn, 0f);
		}

		private static Thing FindBestToy(Pawn pawn)
		{
			Map map = pawn.Map;
			bool groundOnly = RequiresGroundToy(pawn);

			return GenClosest.ClosestThingReachable(
				pawn.Position,
				map,
				ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
				PathEndMode.InteractionCell,
				TraverseParms.For(pawn, Danger.Some),
				SearchRadius,
				thing =>
				{
					if (thing is not Building building)
					{
						return false;
					}

					CompToddlerToy comp = building.TryGetComp<CompToddlerToy>();
					if (comp == null || !comp.Allows(pawn))
					{
						return false;
					}

					if (groundOnly && !comp.GroundToy)
					{
						return false;
					}

					return pawn.CanReserveAndReach(building, PathEndMode.InteractionCell, Danger.Some);
				});
		}

		private static bool RequiresGroundToy(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			if (!ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			EnsureWalkDef();
			if (_learningToWalkDef == null || pawn.health?.hediffSet == null)
			{
				return false;
			}

			Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(_learningToWalkDef);
			return hediff != null && hediff.Severity < 0.5f;
		}

		private static void EnsureWalkDef()
		{
			if (_walkHediffChecked)
			{
				return;
			}

			_walkHediffChecked = true;
			_learningToWalkDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningToWalk");
		}
	}

	public sealed class ToddlerPlayGiver_ObserveAdultWork : ToddlerPlayGiver
	{
		private const float MaxDistanceToAdult = 100f;
		private const float MaxToddlerFollowDistanceSq = 25f;
		private const float MaxMovingAdultDistanceSq = 9f;
		private const int ObserveCooldownTicks = 60;
		private const int ObserveCooldownKey = 1934182754;

		public override bool CanDo(Pawn pawn)
		{
			if (!IsEligiblePawn(pawn))
			{
				return false;
			}

			if (IsOnCooldown(pawn))
			{
				return false;
			}

			return FindBestAdult(pawn) != null;
		}

		public override Job TryGiveJob(Pawn pawn)
		{
			if (!IsEligiblePawn(pawn))
			{
				return null;
			}

			Pawn adult = FindBestAdult(pawn);
			if (adult == null)
			{
				return null;
			}

			StartCooldown(pawn);
			return JobMaker.MakeJob(def.jobDef, adult);
		}

		private static bool IsEligiblePawn(Pawn pawn)
		{
			if (pawn?.Map == null || !ToddlersCompatUtility.IsToddler(pawn) || pawn.needs == null)
			{
				return false;
			}

			if (pawn.needs.joy != null && pawn.needs.joy.CurLevel > 0.8f)
			{
				return false;
			}

			return !PawnUtility.WillSoonHaveBasicNeed(pawn, -0.05f);
		}

		private static bool IsOnCooldown(Pawn pawn)
		{
			if (pawn?.mindState?.thinkData == null)
			{
				return false;
			}

			int now = Find.TickManager?.TicksGame ?? 0;
			if (!pawn.mindState.thinkData.TryGetValue(ObserveCooldownKey, out int nextTick))
			{
				return false;
			}

			return now < nextTick;
		}

		private static void StartCooldown(Pawn pawn)
		{
			if (pawn?.mindState?.thinkData == null)
			{
				return;
			}

			int now = Find.TickManager?.TicksGame ?? 0;
			pawn.mindState.thinkData[ObserveCooldownKey] = now + ObserveCooldownTicks;
		}

		private static Pawn FindBestAdult(Pawn pawn)
		{
			Pawn bestAdult = null;
			float bestScore = float.MinValue;

			foreach (Pawn adult in AdultsToObserve(pawn))
			{
				float distanceSq = pawn.Position.DistanceToSquared(adult.Position);
				if (distanceSq > MaxToddlerFollowDistanceSq)
				{
					continue;
				}

				float score = 1f / Mathf.Max(1f, distanceSq);
				if (adult.pather?.Moving == true)
				{
					score *= 0.5f;
				}

				if (score > bestScore)
				{
					bestScore = score;
					bestAdult = adult;
				}
			}

			return bestAdult;
		}

		private static IEnumerable<Pawn> AdultsToObserve(Pawn toddler)
		{
			if (toddler == null || !toddler.Spawned || toddler.Map == null)
			{
				yield break;
			}

			foreach (Pawn pawn in toddler.Map.mapPawns.FreeColonistsSpawned)
			{
				if (pawn == null)
				{
					continue;
				}

				float distanceSq = pawn.Position.DistanceToSquared(toddler.Position);
				if (distanceSq > MaxDistanceToAdult * MaxDistanceToAdult)
				{
					continue;
				}

				if (!ToddlerCanLearnFromAdult(toddler, pawn))
				{
					continue;
				}

				if (pawn.pather?.Moving == true && distanceSq > MaxMovingAdultDistanceSq)
				{
					continue;
				}

				yield return pawn;
			}
		}

		private static bool ToddlerCanLearnFromAdult(Pawn toddler, Pawn adult)
		{
			if (adult == null || toddler == null || adult.Dead || !adult.Spawned || adult.Destroyed)
			{
				return false;
			}

			if (adult.DevelopmentalStage.Juvenile() || adult == toddler || adult.IsForbidden(toddler))
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

			return IsInterestingAdultJob(adult);
		}

		private static bool IsInterestingAdultJob(Pawn adult)
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
	}
}
