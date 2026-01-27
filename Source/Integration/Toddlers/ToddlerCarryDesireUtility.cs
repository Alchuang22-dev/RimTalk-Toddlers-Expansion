using System;
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

		public static void Tick()
		{
			int currentTick = Find.TickManager?.TicksGame ?? 0;
			if (currentTick <= 0)
			{
				return;
			}

			if (currentTick % DesireCheckInterval == 0)
			{
				TryTriggerDesires();
			}

			if (currentTick % PickupScanInterval == 0)
			{
				TryAssignNearbyCaregivers();
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

		private static void TryTriggerDesires()
		{
			foreach (Map map in Find.Maps)
			{
				if (map == null)
				{
					continue;
				}

				var pawns = map.mapPawns?.AllPawnsSpawned;
				if (pawns == null)
				{
					continue;
				}

				for (int i = 0; i < pawns.Count; i++)
				{
					Pawn pawn = pawns[i];
					if (!CanStartWantToBeHeld(pawn))
					{
						continue;
					}

					if (!Rand.Chance(DesireChancePerCheck))
					{
						continue;
					}

					if (TryStartWantToBeHeld(pawn))
					{
						TryAssignCaregiverForPawn(pawn);
					}
				}
			}
		}

		private static void TryAssignNearbyCaregivers()
		{
			foreach (Map map in Find.Maps)
			{
				if (map == null)
				{
					continue;
				}

				var pawns = map.mapPawns?.AllPawnsSpawned;
				if (pawns == null)
				{
					continue;
				}

				for (int i = 0; i < pawns.Count; i++)
				{
					Pawn pawn = pawns[i];
					if (!IsWantingToBeHeld(pawn))
					{
						continue;
					}

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

			return pawn.mindState.mentalStateHandler.TryStartMentalState(desireDef, null, false, false, false, null, false, false);
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
			if (pawn == null || pawn.Dead || pawn.Destroyed)
			{
				return false;
			}

			if (pawn.Faction != Faction.OfPlayer)
			{
				return false;
			}

			if (!IsBabyOrToddler(pawn))
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
