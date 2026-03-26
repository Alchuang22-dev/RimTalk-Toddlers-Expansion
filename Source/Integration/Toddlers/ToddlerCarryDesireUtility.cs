using System;
using System.Collections.Generic;
using System.Reflection;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ToddlerCarryDesireUtility
	{
		private const int DesireCheckInterval = 600;
		private const int PickupScanInterval = 120;
		private const float DesireChancePerCheck = 0.01f;
		private const float MinFoodPercent = 0.5f;
		private const float SearchRadius = 2.5f;
		private static readonly HashSet<Pawn> _trackedDesireCandidates = new HashSet<Pawn>();
		private static readonly HashSet<Pawn> _activeWantToBeHeldPawns = new HashSet<Pawn>();
		private static readonly List<Pawn> _trackedCandidatesBuffer = new List<Pawn>(32);
		private static readonly List<Pawn> _activePawnsBuffer = new List<Pawn>(16);

		public static void Tick()
		{
			if (Find.TickManager == null)
			{
				return;
			}

			ProcessTrackedCandidates();
			ProcessActiveWantToBeHeldPawns();
		}

		public static void ResetTracking()
		{
			_trackedDesireCandidates.Clear();
			_activeWantToBeHeldPawns.Clear();
			_trackedCandidatesBuffer.Clear();
			_activePawnsBuffer.Clear();
		}

		public static void RebuildTrackingFromMaps()
		{
			ResetTracking();
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
					TrackPawnIfRelevant(pawns[j]);
				}
			}
		}

		public static bool TryEndWantToBeHeld(Pawn toddler, bool logSuccess)
		{
			_activeWantToBeHeldPawns.Remove(toddler);
			if (!IsWantingToBeHeld(toddler))
			{
				return false;
			}

			try
			{
				TryRecoverMentalState(toddler?.mindState?.mentalStateHandler);
				if (logSuccess && Prefs.DevMode)
				{
					Log.Message($"[RimTalk_ToddlersExpansion] WantToBeHeld ended for {toddler?.LabelShort}");
				}
				return true;
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to end WantToBeHeld: {ex.Message}");
				}
				return false;
			}
		}

		public static void NotifyPawnSpawned(Pawn pawn)
		{
			TrackPawnIfRelevant(pawn);
		}

		public static void NotifyPawnDespawned(Pawn pawn)
		{
			UntrackPawn(pawn);
		}

		public static void NotifyPawnFactionChanged(Pawn pawn)
		{
			TrackPawnIfRelevant(pawn);
		}

		public static void NotifyPawnKilled(Pawn pawn)
		{
			UntrackPawn(pawn);
		}

		private static void ProcessTrackedCandidates()
		{
			if (_trackedDesireCandidates.Count == 0)
			{
				return;
			}

			_trackedCandidatesBuffer.Clear();
			foreach (Pawn pawn in _trackedDesireCandidates)
			{
				_trackedCandidatesBuffer.Add(pawn);
			}

			for (int i = 0; i < _trackedCandidatesBuffer.Count; i++)
			{
				Pawn pawn = _trackedCandidatesBuffer[i];
				if (!IsTrackedDesireCandidate(pawn))
				{
					UntrackPawn(pawn);
					continue;
				}

				if (IsWantingToBeHeld(pawn))
				{
					_activeWantToBeHeldPawns.Add(pawn);
					continue;
				}

				if (!pawn.IsHashIntervalTick(DesireCheckInterval))
				{
					continue;
				}

				if (!CanStartWantToBeHeld(pawn) || !Rand.Chance(DesireChancePerCheck))
				{
					continue;
				}

				if (TryStartWantToBeHeld(pawn))
				{
					TryAssignCaregiverForPawn(pawn);
				}
			}
		}

		private static void ProcessActiveWantToBeHeldPawns()
		{
			if (_activeWantToBeHeldPawns.Count == 0)
			{
				return;
			}

			_activePawnsBuffer.Clear();
			foreach (Pawn pawn in _activeWantToBeHeldPawns)
			{
				_activePawnsBuffer.Add(pawn);
			}

			for (int i = 0; i < _activePawnsBuffer.Count; i++)
			{
				Pawn pawn = _activePawnsBuffer[i];
				if (!IsTrackedDesireCandidate(pawn) || !IsWantingToBeHeld(pawn))
				{
					_activeWantToBeHeldPawns.Remove(pawn);
					if (!IsTrackedDesireCandidate(pawn))
					{
						UntrackPawn(pawn);
					}
					continue;
				}

				if (pawn.IsHashIntervalTick(PickupScanInterval))
				{
					TryAssignCaregiverForPawn(pawn);
				}
			}
		}

		private static bool TryStartWantToBeHeld(Pawn pawn)
		{
			MentalStateDef desireDef = ToddlersExpansionMentalStateDefOf.RimTalk_WantToBeHeld;
			if (desireDef == null || pawn?.mindState?.mentalStateHandler == null)
			{
				return false;
			}

			bool started = pawn.mindState.mentalStateHandler.TryStartMentalState(desireDef, null, false, false, false, null, false, false);
			if (started)
			{
				_activeWantToBeHeldPawns.Add(pawn);
			}

			return started;
		}

		private static bool IsWantingToBeHeld(Pawn pawn)
		{
			if (pawn == null || pawn.mindState?.mentalStateHandler == null)
			{
				return false;
			}

			MentalStateDef desireDef = ToddlersExpansionMentalStateDefOf.RimTalk_WantToBeHeld;
			if (desireDef == null)
			{
				return false;
			}

			MentalStateDef currentDef = GetCurrentMentalStateDef(pawn.mindState.mentalStateHandler);
			return currentDef == desireDef;
		}

		private static MentalStateDef GetCurrentMentalStateDef(MentalStateHandler handler)
		{
			if (handler == null)
			{
				return null;
			}

			Type handlerType = handler.GetType();
			PropertyInfo defProp = handlerType.GetProperty("CurStateDef");
			if (defProp != null)
			{
				return defProp.GetValue(handler) as MentalStateDef;
			}

			PropertyInfo stateProp = handlerType.GetProperty("CurState");
			if (stateProp != null)
			{
				object state = stateProp.GetValue(handler);
				if (state == null)
				{
					return null;
				}

				PropertyInfo innerDefProp = state.GetType().GetProperty("def");
				if (innerDefProp != null)
				{
					return innerDefProp.GetValue(state) as MentalStateDef;
				}

				FieldInfo innerDefField = state.GetType().GetField("def");
				return innerDefField?.GetValue(state) as MentalStateDef;
			}

			return null;
		}

		private static bool TryRecoverMentalState(MentalStateHandler handler)
		{
			if (handler == null)
			{
				return false;
			}

			Type handlerType = handler.GetType();
			MethodInfo method = handlerType.GetMethod("ClearMentalState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method != null)
			{
				ParameterInfo[] parameters = method.GetParameters();
				object[] args = parameters.Length == 1 ? new object[] { false } : Array.Empty<object>();
				method.Invoke(handler, args);
				return true;
			}

			method = handlerType.GetMethod("EndCurrentState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method != null)
			{
				method.Invoke(handler, Array.Empty<object>());
				return true;
			}

			method = handlerType.GetMethod("RecoverFromState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method != null)
			{
				method.Invoke(handler, Array.Empty<object>());
				return true;
			}

			PropertyInfo curStateProp = handlerType.GetProperty("CurState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			object state = curStateProp?.GetValue(handler);
			if (state == null)
			{
				return false;
			}

			MethodInfo recover = state.GetType().GetMethod("RecoverFromState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (recover == null)
			{
				return false;
			}

			recover.Invoke(state, Array.Empty<object>());
			return true;
		}

		private static bool CanStartWantToBeHeld(Pawn pawn)
		{
			if (!IsTrackedDesireCandidate(pawn))
			{
				return false;
			}

			if (pawn.Downed || pawn.InMentalState || !pawn.Awake())
			{
				return false;
			}

			if (ToddlerCarryingUtility.IsBeingCarried(pawn))
			{
				return false;
			}

			var food = pawn.needs?.food;
			if (food == null || food.CurLevelPercentage < MinFoodPercent)
			{
				return false;
			}

			return pawn.mindState?.mentalStateHandler != null;
		}

		private static bool IsTrackedDesireCandidate(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned)
			{
				return false;
			}

			if (pawn.Faction != Faction.OfPlayer)
			{
				return false;
			}

			return IsBabyOrToddler(pawn);
		}

		private static void TrackPawnIfRelevant(Pawn pawn)
		{
			if (!IsTrackedDesireCandidate(pawn))
			{
				UntrackPawn(pawn);
				return;
			}

			_trackedDesireCandidates.Add(pawn);
			if (IsWantingToBeHeld(pawn))
			{
				_activeWantToBeHeldPawns.Add(pawn);
			}
		}

		private static void UntrackPawn(Pawn pawn)
		{
			if (pawn == null)
			{
				return;
			}

			_trackedDesireCandidates.Remove(pawn);
			_activeWantToBeHeldPawns.Remove(pawn);
		}

		private static bool IsBabyOrToddler(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			return ToddlersCompatUtility.IsToddler(pawn) || pawn.DevelopmentalStage.Baby();
		}

		private static void TryAssignCaregiverForPawn(Pawn toddler)
		{
			if (ToddlerCarryingUtility.IsBeingCarried(toddler))
			{
				return;
			}

			Pawn caregiver = FindNearbyCaregiver(toddler);
			if (caregiver == null)
			{
				return;
			}

			JobDef jobDef = ToddlersExpansionJobDefOf.RimTalk_PickUpToddler;
			if (jobDef == null)
			{
				return;
			}

			Job job = JobMaker.MakeJob(jobDef, toddler);
			job.count = 1;
			caregiver.jobs.TryTakeOrderedJob(job, JobTag.Misc);
		}

		private static Pawn FindNearbyCaregiver(Pawn toddler)
		{
			if (toddler == null || toddler.Map == null)
			{
				return null;
			}

			Pawn best = null;
			float bestDist = float.MaxValue;

			var pawns = toddler.Map.mapPawns?.FreeColonistsSpawned;
			if (pawns == null)
			{
				return null;
			}

			IntVec3 center = toddler.Position;

			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (!IsEligibleCaregiver(pawn, toddler))
				{
					continue;
				}

				if (!pawn.Position.InHorDistOf(center, SearchRadius))
				{
					continue;
				}

				float dist = pawn.Position.DistanceToSquared(center);
				if (dist < bestDist)
				{
					bestDist = dist;
					best = pawn;
				}
			}

			return best;
		}

		private static bool IsEligibleCaregiver(Pawn pawn, Pawn toddler)
		{
			if (pawn == null || toddler == null)
			{
				return false;
			}

			if (!ToddlerCarryingUtility.IsValidCarrier(pawn))
			{
				return false;
			}

			if (pawn.Drafted || pawn.InMentalState || !pawn.Awake())
			{
				return false;
			}

			if (pawn.workSettings == null || !pawn.workSettings.WorkIsActive(WorkTypeDefOf.Childcare))
			{
				return false;
			}

			if (ToddlerCarryingUtility.GetCarriedToddlerCount(pawn) >= ToddlerCarryingUtility.GetMaxCarryCapacity(pawn))
			{
				return false;
			}

			if (!pawn.CanReserveAndReach(toddler, PathEndMode.Touch, Danger.Some))
			{
				return false;
			}

			return true;
		}
	}
}
