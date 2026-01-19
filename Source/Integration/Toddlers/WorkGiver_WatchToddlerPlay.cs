using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class WorkGiver_WatchToddlerPlay : JoyGiver
	{
		private const int WatchSearchRadius = 10;
		private const int SpotSearchRadius = 4;

		public override Job TryGiveJob(Pawn pawn)
		{
			if (pawn?.Map == null || pawn.needs?.joy == null)
			{
				return null;
			}

			if (pawn.Downed || pawn.Drafted || pawn.InMentalState || !IsEligibleWatcher(pawn))
			{
				return null;
			}

			Pawn toddler = FindPlayingToddler(pawn);
			if (toddler == null)
			{
				return null;
			}

			if (!TryFindWatchSpot(pawn, toddler, out IntVec3 spot))
			{
				return null;
			}

			Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_WatchToddlerPlayJob, toddler, spot);
			job.ignoreJoyTimeAssignment = false;
			job.expiryInterval = 2000;
			return job;
		}

		private static Pawn FindPlayingToddler(Pawn watcher)
		{
			Map map = watcher.Map;
			var pawns = watcher.Faction != null
				? map.mapPawns.SpawnedPawnsInFaction(watcher.Faction)
				: map.mapPawns.AllPawnsSpawned;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn other = pawns[i];
				if (!ToddlersCompatUtility.IsToddlerOrBaby(other))
				{
					continue;
				}

				if (!other.Spawned || other.Downed || !ToddlersCompatUtility.IsEngagedInToddlerPlay(other))
				{
					continue;
				}

				if (!watcher.Position.InHorDistOf(other.Position, WatchSearchRadius))
				{
					continue;
				}

				return other;
			}

			return null;
		}

		private static bool IsEligibleWatcher(Pawn pawn)
		{
			if (pawn?.RaceProps?.Humanlike != true)
			{
				return false;
			}

			if (ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			return pawn.DevelopmentalStage == DevelopmentalStage.Adult;
		}

		private static bool TryFindWatchSpot(Pawn watcher, Pawn toddler, out IntVec3 spot)
		{
			Map map = watcher.Map;
			IntVec3 root = toddler.Position;
			return CellFinder.TryFindRandomCellNear(root, map, SpotSearchRadius, cell =>
			{
				if (!cell.Standable(map) || cell.IsForbidden(watcher))
				{
					return false;
				}

				return watcher.CanReserveSittableOrSpot(cell);
			}, out spot);
		}
	}
}
