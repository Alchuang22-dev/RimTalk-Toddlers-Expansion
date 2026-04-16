using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Harmony;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class ToddlerCarryingGameComponent : GameComponent
	{
		private const int CleanupInterval = 600;
		private const int CarriedJobInterval = 120;
		private const int OrphanExitInterval = 600;

		// Re-use the BabyCarryCheckIntervalTicks setting for visitor re-pickup scanning cadence.
		// This keeps visitor and colonist carry-desire scanning at the same frequency.
		private const int VisitorRepickupMaxPerTick = 3;

		private bool _needsCarryProtectionResync;
		private readonly List<Pawn> _activeCarriedToddlers = new List<Pawn>(32);
		private readonly List<Pawn> _activeCarriers = new List<Pawn>(32);

		public ToddlerCarryingGameComponent(Game game)
		{
		}

		public override void GameComponentTick()
		{
			base.GameComponentTick();
			if (Find.TickManager == null)
			{
				return;
			}

			int currentTick = Find.TickManager.TicksGame;
			int mainLoopInterval = ToddlersExpansionSettings.GetToddlerMainLoopCheckIntervalTicks();

			if (currentTick % mainLoopInterval == 0)
			{
				// Process deferred carry assignments outside GenSpawn stack.
				Patch_VisitorToddlerBabyFood.ProcessPendingCarryingAssignments();
				MaintainActiveCarryingRelations();
				ToddlerCarryDesireUtility.Tick();
			}

			if (currentTick % CleanupInterval == 0)
			{
				ToddlerCarryingTracker.CleanupInvalidEntries();
				Patch_VisitorToddlerBabyFood.CleanupInvalidNewEnvironmentMoodOnMaps();
			}

			if (currentTick % CarriedJobInterval == 0)
			{
				CarriedToddlerStateUtility.UpdateCarriedJobs();
			}

			if (currentTick % OrphanExitInterval == 0)
			{
				EnsureVisitorToddlersExitIfNoAdults();
			}

			int visitorPickupInterval = ToddlersExpansionSettings.GetBabyCarryPickupCheckIntervalTicks();
			if (currentTick % visitorPickupInterval == 0)
			{
				ProcessVisitorRepickup();
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

			if (toddler.IsBurning())
			{
				Patch_ToddlerCarriedDamageFactor.TryExtinguishCarriedPawn(toddler);
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

		/// <summary>
		/// Scans player-home maps for uncarried visitor toddlers during the stay phase
		/// and assigns same-Lord adults to pick them back up.
		/// Uses BabyCarryCheckIntervalTicks for scan frequency, matching colonist carry-desire cadence.
		/// </summary>
		private static void ProcessVisitorRepickup()
		{
			if (Find.Maps == null || Find.Maps.Count == 0)
			{
				return;
			}

			int processed = 0;
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
					if (processed >= VisitorRepickupMaxPerTick)
					{
						return;
					}

					Pawn toddler = pawns[j];
					if (!IsUncarriedVisitorToddler(toddler))
					{
						continue;
					}

					if (TryAssignSameLordCarrier(toddler))
					{
						processed++;
					}
				}
			}
		}

		private static bool IsUncarriedVisitorToddler(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned)
			{
				return false;
			}

			if (!ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return false;
			}

			if (pawn.IsPrisoner || pawn.IsPrisonerOfColony)
			{
				return false;
			}

			Faction faction = pawn.Faction;
			if (faction == null || faction == Faction.OfPlayer || faction.HostileTo(Faction.OfPlayer))
			{
				return false;
			}

			if (pawn.Map == null || !pawn.Map.IsPlayerHome)
			{
				return false;
			}

			if (ToddlerCarryingUtility.IsBeingCarried(pawn))
			{
				return false;
			}

			// Skip toddlers currently eating or moving to food to avoid interrupt-and-repickup loops:
			// hungry toddler struggles off -> starts eating -> gets picked up -> can't eat -> struggles -> loop.
			JobDef curJob = pawn.CurJobDef;
			if (curJob == JobDefOf.Ingest || curJob == JobDefOf.Goto)
			{
				return false;
			}

			// Must be in a Lord (part of a visiting/trading group)
			return pawn.GetLord() != null;
		}

		private static bool TryAssignSameLordCarrier(Pawn toddler)
		{
			Lord lord = toddler.GetLord();
			if (lord == null || lord.ownedPawns.NullOrEmpty())
			{
				return false;
			}

			// Find the closest valid carrier in the same Lord
			Pawn bestCarrier = null;
			float bestDist = float.MaxValue;

			List<Pawn> members = lord.ownedPawns;
			for (int i = 0; i < members.Count; i++)
			{
				Pawn candidate = members[i];
				if (candidate == null || candidate == toddler)
				{
					continue;
				}

				if (!ToddlerCarryingUtility.IsValidCarrier(candidate))
				{
					continue;
				}

				if (!candidate.Spawned || candidate.MapHeld != toddler.MapHeld)
				{
					continue;
				}

				if (ToddlerCarryingUtility.GetCarriedToddlerCount(candidate)
				    >= ToddlerCarryingUtility.GetMaxCarryCapacity(candidate))
				{
					continue;
				}

				if (!candidate.CanReach(toddler, PathEndMode.Touch, Danger.Some))
				{
					continue;
				}

				if (!candidate.CanReserve(toddler, 1, -1, null, false))
				{
					continue;
				}

				float dist = candidate.Position.DistanceToSquared(toddler.Position);
				if (dist < bestDist)
				{
					bestDist = dist;
					bestCarrier = candidate;
				}
			}

			if (bestCarrier == null)
			{
				return false;
			}

			if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
			{
				Log.Message($"[RimTalk_ToddlersExpansion][VisitorRepickup] Assigning {bestCarrier.LabelShort} to pick up visitor toddler {toddler.LabelShort}.");
			}

			Job job = JobMaker.MakeJob(
				ToddlersExpansionJobDefOf.RimTalk_PickUpToddler, toddler);
			job.locomotionUrgency = LocomotionUrgency.Jog;
			bestCarrier.jobs?.TryTakeOrderedJob(job);
			return true;
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
