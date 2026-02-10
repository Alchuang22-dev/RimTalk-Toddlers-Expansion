using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.AI
{
	/// <summary>
	/// Assign an adult to pick up an uncarried toddler in the same Lord.
	/// </summary>
	public class JobGiver_PickUpUncarriedToddler : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (!ToddlerCarryingUtility.IsValidCarrier(pawn))
			{
				return null;
			}

			int currentCount = ToddlerCarryingUtility.GetCarriedToddlerCount(pawn);
			int maxCapacity = ToddlerCarryingUtility.GetMaxCarryCapacity(pawn);
			if (currentCount >= maxCapacity)
			{
				return null;
			}

			Lord lord = pawn.GetLord();
			if (lord == null || lord.ownedPawns.NullOrEmpty())
			{
				return null;
			}

			// Duty XML injection is broad; enforce exit phase here to avoid pickup loops.
			if (!IsInMapExitPhase(pawn, lord))
			{
				return null;
			}

			Pawn toddlerToPickUp = FindUncarriedAndReservableToddler(pawn, lord);
			if (toddlerToPickUp == null)
			{
				return null;
			}

			Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_PickUpToddler, toddlerToPickUp);
			job.locomotionUrgency = LocomotionUrgency.Jog;
			return job;
		}

		private static Pawn FindUncarriedAndReservableToddler(Pawn carrier, Lord lord)
		{
			Pawn closestToddler = null;
			float closestDist = float.MaxValue;

			List<Pawn> pawns = lord.ownedPawns;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];

				if (!ToddlerCarryingUtility.CanBeCarried(p))
				{
					continue;
				}

				if (ToddlerCarryingUtility.IsBeingCarried(p))
				{
					continue;
				}

				if (!carrier.CanReach(p, PathEndMode.Touch, Danger.Some))
				{
					continue;
				}

				if (!carrier.CanReserve(p, 1, -1, null, false))
				{
					continue;
				}

				float dist = carrier.Position.DistanceToSquared(p.Position);
				if (dist < closestDist)
				{
					closestDist = dist;
					closestToddler = p;
				}
			}

			return closestToddler;
		}

		private static bool IsInMapExitPhase(Pawn pawn, Lord lord)
		{
			if (pawn == null || lord == null)
			{
				return false;
			}

			if (PawnUtility.IsExitingMap(pawn) || pawn.CurJob?.exitMapOnArrival == true)
			{
				return true;
			}

			if (lord.CurLordToil is LordToil_ExitMap)
			{
				return true;
			}

			// Trader caravan private toil type.
			string toilName = lord.CurLordToil?.GetType().Name;
			if (toilName == "LordToil_ExitMapAndEscortCarriers")
			{
				return true;
			}

			DutyDef duty = pawn.mindState?.duty?.def;
			return duty == DutyDefOf.ExitMapBest
				|| duty == DutyDefOf.ExitMapBestAndDefendSelf
				|| duty == DutyDefOf.ExitMapNearDutyTarget;
		}
	}
}
