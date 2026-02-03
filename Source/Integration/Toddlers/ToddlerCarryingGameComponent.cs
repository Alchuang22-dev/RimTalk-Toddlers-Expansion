using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class ToddlerCarryingGameComponent : GameComponent
	{
		private const int CleanupInterval = 600;
		private const int CarriedJobInterval = 120;
		private const int OrphanExitInterval = 600;

		private int _tickCounter;

		public ToddlerCarryingGameComponent(Game game)
		{
		}

		public override void GameComponentTick()
		{
			_tickCounter++;
			if (_tickCounter >= CleanupInterval)
			{
				_tickCounter = 0;
				ToddlerCarryingTracker.CleanupInvalidEntries();
			}

			ToddlerCarryDesireUtility.Tick();

			if (_tickCounter % CarriedJobInterval == 0)
			{
				CarriedToddlerStateUtility.UpdateCarriedJobs();
			}

			if (_tickCounter % OrphanExitInterval == 0)
			{
				EnsureVisitorToddlersExitIfNoAdults();
			}
		}

		public override void StartedNewGame()
		{
			ToddlerCarryingTracker.ClearAll();
		}

		public override void LoadedGame()
		{
			ToddlerCarryingTracker.ClearAll();
		}

		public override void ExposeData()
		{
		}

		private static void EnsureVisitorToddlersExitIfNoAdults()
		{
			if (Find.Maps == null || Find.Maps.Count == 0)
			{
				return;
			}

			for (int i = 0; i < Find.Maps.Count; i++)
			{
				Map map = Find.Maps[i];
				if (map == null || !map.IsPlayerHome)
				{
					continue;
				}

				var pawns = map.mapPawns?.AllPawnsSpawned;
				if (pawns == null || pawns.Count == 0)
				{
					continue;
				}

				for (int j = 0; j < pawns.Count; j++)
				{
					Pawn toddler = pawns[j];
					if (toddler == null || toddler.Dead || toddler.Destroyed || !toddler.Spawned)
					{
						continue;
					}

					if (!ToddlersCompatUtility.IsToddlerOrBaby(toddler))
					{
						continue;
					}

					if (toddler.IsPrisoner || toddler.IsPrisonerOfColony)
					{
						continue;
					}

					Faction faction = toddler.Faction;
					if (faction == null || faction == Faction.OfPlayer || faction.HostileTo(Faction.OfPlayer))
					{
						continue;
					}

					if (ToddlerCarryingUtility.IsBeingCarried(toddler))
					{
						continue;
					}

					if (HasSameFactionAdultOnMap(map, faction))
					{
						continue;
					}

					TryStartExitMap(toddler);
				}
			}
		}

		private static bool HasSameFactionAdultOnMap(Map map, Faction faction)
		{
			var pawns = map.mapPawns?.AllPawnsSpawned;
			if (pawns == null)
			{
				return false;
			}

			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn == null || pawn.Faction != faction)
				{
					continue;
				}

				if (ToddlerCarryingUtility.IsValidCarrier(pawn))
				{
					return true;
				}
			}

			return false;
		}

		private static void TryStartExitMap(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned)
			{
				return;
			}

			if (PawnUtility.IsExitingMap(pawn) || pawn.jobs?.curJob?.exitMapOnArrival == true)
			{
				return;
			}

			if (!RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 spot, TraverseMode.ByPawn, canBash: false))
			{
				return;
			}

			Job job = JobMaker.MakeJob(JobDefOf.Goto, spot);
			job.exitMapOnArrival = true;
			job.locomotionUrgency = LocomotionUrgency.Jog;
			pawn.jobs?.StartJob(job, JobCondition.InterruptForced);
		}
	}
}
