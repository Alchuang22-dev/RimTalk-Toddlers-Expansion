using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers.HAR;
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

			if (!SocialNeedTuning_Toddlers.ShouldDoOptionalActivity(pawn, PlayNeedThreshold))
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
		private const int SharedSpotDistance = 3;

		public override bool CanDo(Pawn pawn)
		{
			string eligibilityFailure = GetEligibilityFailure(pawn);
			if (eligibilityFailure != null)
			{
				MutualPlayDiagnostics.LogSearchFailure(pawn, $"initiator ineligible: {eligibilityFailure}");
				return false;
			}

			if (!SocialNeedTuning_Toddlers.ShouldDoOptionalActivity(pawn, PlayNeedThreshold))
			{
				MutualPlayDiagnostics.LogSearchFailure(
					pawn,
					$"initiator optional-activity check failed at threshold={PlayNeedThreshold:0.00}");
				return false;
			}

			if (!ShouldRunPartnerSearch(pawn))
			{
				MutualPlayDiagnostics.LogSearchFailure(
					pawn,
					$"partner search deferred by hash interval={ToddlersExpansionSettings.GetMutualPlayPartnerCheckIntervalTicks()}");
				return false;
			}

			return TryFindPartnerAndSpot(pawn, out _, out _);
		}

		public override Job TryGiveJob(Pawn pawn)
		{
			if (!IsEligiblePawn(pawn))
			{
				return null;
			}

			if (!SocialNeedTuning_Toddlers.ShouldDoOptionalActivity(pawn, PlayNeedThreshold))
			{
				return null;
			}

			if (!ShouldRunPartnerSearch(pawn))
			{
				return null;
			}

			if (!TryFindPartnerAndSpot(pawn, out Pawn partner, out IntVec3 spot))
			{
				return null;
			}

			Job job = JobMaker.MakeJob(def.jobDef, partner);
			job.ignoreJoyTimeAssignment = true;
			job.expiryInterval = 2000;
			job.targetB = spot;
			MutualPlayDiagnostics.Log(
				pawn,
				"JobCreated",
				$"selected sharedSpot={spot} job={MutualPlayDiagnostics.DescribeJob(job)}",
				partner);

			return job;
		}

		private static bool IsEligiblePawn(Pawn pawn)
		{
			return GetEligibilityFailure(pawn) == null;
		}

		private static string GetEligibilityFailure(Pawn pawn)
		{
			if (pawn == null)
			{
				return "pawn is null";
			}

			if (pawn.Map == null)
			{
				return "map is null";
			}

			if (pawn.needs?.play == null)
			{
				return "play need is null";
			}

			if (!ToddlersCompatUtility.IsEligibleForSelfPlay(pawn))
			{
				return "Toddlers compatibility eligibility rejected pawn";
			}

			if (pawn.Downed)
			{
				return "downed";
			}

			if (pawn.Drafted)
			{
				return "drafted";
			}

			if (ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
			{
				return $"blocking mental state {pawn.MentalStateDef?.defName ?? "unknown"}";
			}

			if (!pawn.Awake())
			{
				return "not awake";
			}

			return ToddlersCompatUtility.IsBusyForMutualPlay(pawn)
				? "busy for mutual play"
				: null;
		}

		private static bool ShouldRunPartnerSearch(Pawn pawn)
		{
			int interval = ToddlersExpansionSettings.GetMutualPlayPartnerCheckIntervalTicks();
			return interval <= 1 || pawn.IsHashIntervalTick(interval);
		}

		private static bool TryFindPartnerAndSpot(Pawn pawn, out Pawn partner, out IntVec3 spot)
		{
			partner = null;
			spot = IntVec3.Invalid;
			Map map = pawn.Map;
			var pawns = pawn.Faction != null
				? map.mapPawns.SpawnedPawnsInFaction(pawn.Faction)
				: map.mapPawns.AllPawnsSpawned;
			int candidates = 0;
			int notToddler = 0;
			int invalidState = 0;
			int busy = 0;
			int tooFar = 0;
			int optionalActivityRejected = 0;
			int reservationRejected = 0;
			int unreachable = 0;
			int noSharedSpot = 0;
			string toddlerRejectionSample = null;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn other = pawns[i];
				if (other == pawn)
				{
					continue;
				}
				candidates++;

				if (!ToddlersCompatUtility.IsToddler(other))
				{
					notToddler++;
					continue;
				}

				if (other.Downed || other.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(other) || !other.Awake())
				{
					invalidState++;
					if (toddlerRejectionSample == null)
					{
						toddlerRejectionSample = $"invalidState: {MutualPlayDiagnostics.DescribePawn(other)}";
					}
					continue;
				}

				if (ToddlersCompatUtility.IsBusyForMutualPlay(other))
				{
					busy++;
					if (toddlerRejectionSample == null)
					{
						toddlerRejectionSample = $"busy: {MutualPlayDiagnostics.DescribePawn(other)}";
					}
					continue;
				}

				if (!pawn.Position.InHorDistOf(other.Position, PartnerSearchRadius))
				{
					tooFar++;
					if (toddlerRejectionSample == null)
					{
						toddlerRejectionSample = $"tooFar: {MutualPlayDiagnostics.DescribePawn(other)}";
					}
					continue;
				}

				if (!SocialNeedTuning_Toddlers.ShouldDoOptionalActivity(other, PlayNeedThreshold))
				{
					optionalActivityRejected++;
					if (toddlerRejectionSample == null)
					{
						toddlerRejectionSample = $"optionalActivity: {MutualPlayDiagnostics.DescribePawn(other)}";
					}
					continue;
				}

				if (!pawn.CanReserve(other))
				{
					reservationRejected++;
					if (toddlerRejectionSample == null)
					{
						toddlerRejectionSample = $"cannotReserve: {MutualPlayDiagnostics.DescribePawn(other)}";
					}
					continue;
				}

				if (!pawn.CanReach(other, PathEndMode.Touch, Danger.Some))
				{
					unreachable++;
					if (toddlerRejectionSample == null)
					{
						toddlerRejectionSample = $"unreachable: {MutualPlayDiagnostics.DescribePawn(other)}";
					}
					continue;
				}

				if (!TryFindPlaySpot(pawn, other, out IntVec3 sharedSpot))
				{
					noSharedSpot++;
					if (toddlerRejectionSample == null)
					{
						toddlerRejectionSample = $"noSharedSpot: {MutualPlayDiagnostics.DescribePawn(other)}";
					}
					continue;
				}

				partner = other;
				spot = sharedSpot;
				return true;
			}

			MutualPlayDiagnostics.LogSearchFailure(
				pawn,
				$"no partner/spot; pool={pawns.Count} candidates={candidates} factionPool={pawn.Faction?.def?.defName ?? "all"} " +
				$"notToddler={notToddler} invalidState={invalidState} busy={busy} tooFar={tooFar} " +
				$"optionalRejected={optionalActivityRejected} reserveRejected={reservationRejected} " +
				$"unreachable={unreachable} noSharedSpot={noSharedSpot} " +
				$"sample=[{toddlerRejectionSample ?? "none"}]");

			return false;
		}

		private static bool TryFindPlaySpot(Pawn pawn, Pawn partner, out IntVec3 spot)
		{
			Map map = pawn.Map;
			IntVec3 root = new IntVec3((pawn.Position.x + partner.Position.x) / 2, 0, (pawn.Position.z + partner.Position.z) / 2);
			return CellFinder.TryFindRandomCellNear(root, map, SpotSearchRadius, cell =>
			{
				if (!cell.Standable(map) || cell.IsForbidden(pawn) || cell.IsForbidden(partner))
				{
					return false;
				}

				if (!cell.InHorDistOf(pawn.Position, SharedSpotDistance) || !cell.InHorDistOf(partner.Position, SharedSpotDistance))
				{
					return false;
				}

				if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some) || !partner.CanReach(cell, PathEndMode.OnCell, Danger.Some))
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

			if (!SocialNeedTuning_Toddlers.ShouldDoOptionalActivity(pawn, PlayNeedThreshold))
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

			if (!SocialNeedTuning_Toddlers.ShouldDoOptionalActivity(pawn, 0.8f))
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
