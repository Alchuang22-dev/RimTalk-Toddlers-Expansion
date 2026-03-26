using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Harmony;
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
		private bool _needsCarryProtectionResync;
		private readonly List<Pawn> _activeCarriedToddlers = new List<Pawn>(32);
		private readonly List<Pawn> _activeCarriers = new List<Pawn>(32);

		public ToddlerCarryingGameComponent(Game game)
		{
		}

		public override void GameComponentTick()
		{
			base.GameComponentTick();

			// Process deferred carry assignments outside GenSpawn stack.
			Patch_VisitorToddlerBabyFood.ProcessPendingCarryingAssignments();
			MaintainActiveCarryingRelations();

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
			base.StartedNewGame();
			ToddlerCarryingTracker.ClearAll();
			ToddlerCarryDesireUtility.RebuildTrackingFromMaps();
			Patch_VisitorToddlerBabyFood.ClearCache();
			_needsCarryProtectionResync = false;
		}

		public override void LoadedGame()
		{
			base.LoadedGame();
			ToddlerCarryingTracker.ClearAll();
			ToddlerCarryDesireUtility.RebuildTrackingFromMaps();
			Patch_VisitorToddlerBabyFood.ClearCache();
			_needsCarryProtectionResync = true;
		}

		public override void ExposeData()
		{
			base.ExposeData();
		}

		public static void RegisterGameComponent()
		{
			if (Current.Game == null)
			{
				return;
			}

			if (Current.Game.GetComponent<ToddlerCarryingGameComponent>() == null)
			{
				Current.Game.components.Add(new ToddlerCarryingGameComponent(Current.Game));
			}
		}

		private void MaintainActiveCarryingRelations()
		{
			ToddlerCarryingTracker.CopyAllCarriedToddlersTo(_activeCarriedToddlers);
			for (int i = 0; i < _activeCarriedToddlers.Count; i++)
			{
				MaintainCarriedToddler(_activeCarriedToddlers[i]);
			}

			ToddlerCarryingTracker.CopyAllCarriersTo(_activeCarriers);
			for (int i = 0; i < _activeCarriers.Count; i++)
			{
				MaintainCarrier(_activeCarriers[i]);
			}

			if (_needsCarryProtectionResync)
			{
				RemoveStaleCarryProtection();
				_needsCarryProtectionResync = false;
			}
		}

		private static void MaintainCarriedToddler(Pawn toddler)
		{
			if (toddler == null || toddler.Dead || toddler.Destroyed || !toddler.Spawned)
			{
				ToddlerCarryingUtility.DismountToddler(toddler);
				return;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(toddler);
			if (carrier == null
				|| !carrier.Spawned
				|| carrier.Map != toddler.Map
				|| !ToddlerCarryingUtility.IsCarryRelationStillValid(carrier, toddler))
			{
				ToddlerCarryingUtility.DismountToddler(toddler);
				return;
			}

			if (toddler.Position != carrier.Position)
			{
				try
				{
					toddler.Position = carrier.Position;
				}
				catch
				{
				}
			}
		}

		private static void MaintainCarrier(Pawn carrier)
		{
			if (carrier == null || !carrier.Spawned)
			{
				return;
			}

			if (!ToddlerCarryingTracker.TryGetCarriedToddlersNoAlloc(carrier, out List<Pawn> toddlers))
			{
				return;
			}

			for (int i = toddlers.Count - 1; i >= 0; i--)
			{
				Pawn toddler = toddlers[i];
				if (toddler == null || toddler.Dead || toddler.Destroyed || !toddler.Spawned)
				{
					ToddlerCarryingUtility.DismountToddler(toddler);
					continue;
				}

				if (toddler.Map != carrier.Map || !ToddlerCarryingUtility.IsCarryRelationStillValid(carrier, toddler))
				{
					ToddlerCarryingUtility.DismountToddler(toddler);
				}
			}
		}

		private static void RemoveStaleCarryProtection()
		{
			if (Find.Maps == null)
			{
				return;
			}

			for (int i = 0; i < Find.Maps.Count; i++)
			{
				Map map = Find.Maps[i];
				var pawns = map?.mapPawns?.AllPawnsSpawned;
				if (pawns == null)
				{
					continue;
				}

				for (int j = 0; j < pawns.Count; j++)
				{
					Pawn pawn = pawns[j];
					if (!ToddlerCarryProtectionUtility.HasCarryProtection(pawn) || ToddlerCarryingUtility.IsBeingCarried(pawn))
					{
						continue;
					}

					ToddlerCarryProtectionUtility.SetCarryProtectionActive(pawn, false);
				}
			}
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
