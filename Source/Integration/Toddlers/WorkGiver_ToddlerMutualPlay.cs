using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class WorkGiver_ToddlerMutualPlay : WorkGiver
	{
		private const float PlayNeedThreshold = 0.9f;
		private const int PartnerSearchRadius = 10;
		private const int SpotSearchRadius = 6;

		public override Job NonScanJob(Pawn pawn)
		{
			return TryGiveJob(pawn);
		}

		public Job TryGiveJob(Pawn pawn)
		{
			if (pawn?.Map == null || !ToddlersCompatUtility.IsEligibleForSelfPlay(pawn))
			{
				return null;
			}

			if (pawn.Downed || pawn.Drafted || pawn.InMentalState || !pawn.Awake())
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

			Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob, partner);
			job.ignoreJoyTimeAssignment = true;
			job.expiryInterval = 2000;

			if (TryFindPlaySpot(pawn, out IntVec3 spot))
			{
				job.targetB = spot;
			}

			return job;
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

				if (other.Downed || other.Drafted || other.InMentalState || !other.Awake())
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
}
