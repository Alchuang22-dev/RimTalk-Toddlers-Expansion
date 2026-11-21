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
		private const float DesireChancePerCheck = 0.01f;
		private const float MinFoodPercent = 0.5f;
		private const float SearchRadius = 2.5f;
		private static readonly HashSet<Pawn> _trackedDesireCandidates = new HashSet<Pawn>();
		private static readonly List<Pawn> _trackedCandidatesBuffer = new List<Pawn>(32);

		public static bool HasTrackedPawns => _trackedDesireCandidates.Count > 0;

		public static void Tick()
		{
			Tick(1);
		}

		internal static void Tick(int elapsedTicks)
		{
			if (Find.TickManager == null || _trackedDesireCandidates.Count == 0)
			{
				return;
			}

			ProcessTrackedCandidates(elapsedTicks);
		}

		public static void ResetTracking()
		{
			_trackedDesireCandidates.Clear();
			_trackedCandidatesBuffer.Clear();
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

		private static void ProcessTrackedCandidates(int elapsedTicks)
		{
			int pickupInterval = ToddlersExpansionSettings.GetBabyCarryPickupCheckIntervalTicks();
			int desireInterval = ToddlersExpansionSettings.GetBabyCarryDesireCheckIntervalTicks();
			_trackedCandidatesBuffer.Clear();
			foreach (Pawn pawn in _trackedDesireCandidates)
			{
				// The desire interval is a multiple of the pickup interval.
				if (IsDue(pawn.HashOffsetTicks(), pickupInterval, elapsedTicks))
				{
					_trackedCandidatesBuffer.Add(pawn);
				}
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
					TryAssignCaregiverForPawn(pawn);
					continue;
				}

				if (!IsDue(pawn.HashOffsetTicks(), desireInterval, elapsedTicks))
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
			_trackedCandidatesBuffer.Clear();
		}

		private static bool IsDue(int hashTick, int interval, int elapsedTicks)
		{
			int phase = hashTick % interval;
			int ticksSinceDue = phase < 0 ? phase + interval : phase;
			long ticksSinceWrap = (long)hashTick - int.MinValue;
			if (ticksSinceWrap < elapsedTicks - 1)
			{
				// The elapsed window includes ticks before the signed hash value wrapped.
				return ticksSinceDue <= ticksSinceWrap || ticksSinceWrap + 1 + int.MaxValue % interval < elapsedTicks;
			}

			return ticksSinceDue < elapsedTicks;
		}

		private static bool TryStartWantToBeHeld(Pawn pawn)
		{
			MentalStateDef desireDef = ToddlersExpansionMentalStateDefOf.RimTalk_WantToBeHeld;
			if (desireDef == null || pawn?.mindState?.mentalStateHandler == null)
			{
				return false;
			}

			return pawn.mindState.mentalStateHandler.TryStartMentalState(desireDef, null, false, false, false, null, false, false);
		}

		private static bool IsWantingToBeHeld(Pawn pawn)
		{
			MentalStateDef desireDef = ToddlersExpansionMentalStateDefOf.RimTalk_WantToBeHeld;
			return desireDef != null && pawn?.mindState?.mentalStateHandler != null && pawn.MentalStateDef == desireDef;
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
		}

		private static void UntrackPawn(Pawn pawn)
		{
			if (pawn == null)
			{
				return;
			}

			_trackedDesireCandidates.Remove(pawn);
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

			var pawns = ToddlersExpansionSettings.EnablePrisonerBabyCarryInteractions
				? toddler.Map.mapPawns?.FreeColonistsAndPrisonersSpawned
				: toddler.Map.mapPawns?.FreeColonistsSpawned;
			if (pawns == null)
			{
				return null;
			}

			IntVec3 center = toddler.Position;

			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn == null || !pawn.Position.InHorDistOf(center, SearchRadius))
				{
					continue;
				}

				float dist = pawn.Position.DistanceToSquared(center);
				if (dist >= bestDist || !IsEligibleCaregiver(pawn, toddler))
				{
					continue;
				}

				bestDist = dist;
				best = pawn;
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
