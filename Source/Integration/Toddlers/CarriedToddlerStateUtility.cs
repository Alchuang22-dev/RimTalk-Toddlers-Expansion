using System;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class CarriedToddlerStateUtility
	{
		private const float HungryThreshold = 0.10f;
		private const float RestThreshold = 0.10f;
		private const float ObserveChance = 0.65f;

		public static void UpdateCarriedJobs()
		{
			var carriedToddlers = ToddlerCarryingTracker.GetAllCarriedToddlers();
			if (carriedToddlers.Count == 0)
			{
				return;
			}

			for (int i = 0; i < carriedToddlers.Count; i++)
			{
				Pawn toddler = carriedToddlers[i];
				if (toddler == null || toddler.Dead || toddler.Destroyed)
				{
					continue;
				}

				Pawn carrier = ToddlerCarryingUtility.GetCarrier(toddler);
				if (carrier == null)
				{
					continue;
				}

				JobDef current = toddler.CurJobDef;

				if (ShouldStruggle(toddler))
				{
					if (current != ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Struggle)
					{
						StartCarriedJob(toddler, carrier, ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Struggle);
					}
					continue;
				}

				if (ShouldSleep(toddler))
				{
					if (current != ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Sleep)
					{
						StartCarriedJob(toddler, carrier, ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Sleep);
					}
					continue;
				}

				if (!IsCarriedStateJob(current))
				{
					StartCarriedJob(toddler, carrier, SelectIdleOrObserveJob());
				}
			}
		}

		public static void EnsureCarriedJob(Pawn toddler, Pawn carrier, bool force)
		{
			if (toddler?.jobs == null || carrier == null)
			{
				return;
			}

			JobDef desired = GetDesiredJobDef(toddler);
			if (desired == null)
			{
				return;
			}

			if (!force && toddler.CurJobDef == desired)
			{
				return;
			}

			StartCarriedJob(toddler, carrier, desired);
		}

		public static bool IsCarriedStateJob(JobDef jobDef)
		{
			if (jobDef == null)
			{
				return false;
			}

			return jobDef == ToddlersExpansionJobDefOf.RimTalk_BeingCarried
				|| jobDef == ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Idle
				|| jobDef == ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Observe
				|| jobDef == ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Sleep
				|| jobDef == ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Struggle;
		}

		public static void TryQueueStruggleTalk(Pawn carrier, Pawn toddler)
		{
			if (carrier == null || toddler == null || !RimTalkCompatUtility.IsRimTalkActive)
			{
				return;
			}

			string carrierName = carrier.Name?.ToStringShort ?? "Adult";
			string toddlerName = toddler.Name?.ToStringShort ?? "Toddler";
			string prompt =
				$"{toddlerName} starts squirming in {carrierName}'s arms because they are hungry. " +
				$"{carrierName} decides to put {toddlerName} down and check on their needs. " +
				"Generate a short, gentle interaction.";

			RimTalkCompatUtility.TryQueueTalk(carrier, toddler, prompt, "Event");
		}

		private static JobDef GetDesiredJobDef(Pawn toddler)
		{
			if (ShouldStruggle(toddler))
			{
				return ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Struggle;
			}

			if (ShouldSleep(toddler))
			{
				return ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Sleep;
			}

			return SelectIdleOrObserveJob();
		}

		private static JobDef SelectIdleOrObserveJob()
		{
			JobDef observe = ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Observe;
			JobDef idle = GetIdleJobDef();

			if (observe == null && idle == null)
			{
				return null;
			}

			if (observe == null)
			{
				return idle;
			}

			if (idle == null)
			{
				return observe;
			}

			return Rand.Chance(ObserveChance) ? observe : idle;
		}

		private static JobDef GetIdleJobDef()
		{
			return ToddlersExpansionJobDefOf.RimTalk_BeingCarried_Idle
				?? ToddlersExpansionJobDefOf.RimTalk_BeingCarried;
		}

		private static void StartCarriedJob(Pawn toddler, Pawn carrier, JobDef jobDef)
		{
			if (jobDef == null || toddler?.jobs == null)
			{
				return;
			}

			try
			{
				Job job = JobMaker.MakeJob(jobDef, carrier);
				toddler.jobs.StartJob(job, JobCondition.InterruptForced);
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to start carried job {jobDef.defName}: {ex.Message}");
				}
			}
		}

		private static bool ShouldStruggle(Pawn toddler)
		{
			if (ShouldSuppressStruggleDuringExit(toddler))
			{
				return false;
			}

			var food = toddler?.needs?.food;
			return food != null && food.CurLevelPercentage < HungryThreshold;
		}

		private static bool ShouldSuppressStruggleDuringExit(Pawn toddler)
		{
			if (toddler == null || !ToddlerCarryingUtility.IsBeingCarried(toddler))
			{
				return false;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(toddler);
			if (carrier == null || carrier.Dead || carrier.Destroyed)
			{
				return false;
			}

			if (carrier.Faction == Faction.OfPlayer)
			{
				return false;
			}

			Lord lord = carrier.GetLord();
			if (lord == null)
			{
				return false;
			}

			// Check if the Lord is in an exit-related LordToil
			if (lord.CurLordToil is LordToil_ExitMap)
			{
				return true;
			}

			// Trader caravans use a custom exit toil that still represents map-leaving state.
			string toilName = lord.CurLordToil?.GetType().Name;
			if (toilName == "LordToil_ExitMapAndEscortCarriers")
			{
				return true;
			}

			// Visitors use LordToil_Travel when moving to exit point before actually exiting
			if (lord.CurLordToil is LordToil_Travel)
			{
				// Check if the carrier's duty is TravelOrLeave (moving to exit point)
				var duty = carrier.mindState?.duty;
				if (duty?.def == DutyDefOf.TravelOrLeave)
				{
					return true;
				}
			}

			return false;
		}

		private static bool ShouldSleep(Pawn toddler)
		{
			var rest = toddler?.needs?.rest;
			return rest != null && rest.CurLevelPercentage < RestThreshold;
		}
	}
}
