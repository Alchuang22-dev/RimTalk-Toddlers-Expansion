using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class WorkGiver_ToddlerSelfPlay : WorkGiver
	{
		private const float PlayNeedThreshold = 0.9f;
		private const int SearchRadius = 6;

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

			if (pawn.Downed || pawn.Drafted || pawn.InMentalState)
			{
				return null;
			}

			if (PawnUtility.WillSoonHaveBasicNeed(pawn, 0f))
			{
				return null;
			}

			Need_Play play = pawn.needs?.play;
			if (play != null && play.CurLevelPercentage >= PlayNeedThreshold)
			{
				return null;
			}

			if (!TryFindPlaySpot(pawn, out IntVec3 spot))
			{
				return null;
			}

			Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfPlayJob, spot);
			job.ignoreJoyTimeAssignment = true;
			job.expiryInterval = 2000;
			return job;
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
}
