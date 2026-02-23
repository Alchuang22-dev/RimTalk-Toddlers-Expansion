using System;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.UI
{
	public class FloatMenuOptionProvider_PickUpToddler : FloatMenuOptionProvider
	{
		private const string LogPrefix = "[RimTalk_ToddlersExpansion][PickUpToddlerMenu]";

		protected override bool Drafted => true;
		protected override bool Undrafted => true;
		protected override bool Multiselect => false;
		protected override bool RequiresManipulation => true;

		protected override bool AppliesInt(FloatMenuContext context)
		{
			if (!base.AppliesInt(context))
			{
				return false;
			}

			Pawn pawn = context?.FirstSelectedPawn;
			if (pawn == null || pawn.IsMutant)
			{
				return false;
			}

			if (!ToddlerCarryingUtility.IsValidCarrier(pawn))
			{
				return false;
			}

			if (ToddlerCarryingUtility.GetCarriedToddlerCount(pawn) >= ToddlerCarryingUtility.GetMaxCarryCapacity(pawn))
			{
				return false;
			}

			return true;
		}

		protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
		{
			try
			{
				if (!IsToddlerOrBaby(clickedPawn))
				{
					return null;
				}

				Pawn carrier = context?.FirstSelectedPawn;
				if (carrier == null || carrier == clickedPawn)
				{
					return null;
				}

				if (carrier.Map == null || clickedPawn.Map != carrier.Map)
				{
					return null;
				}

				if (IsToddlerOrBaby(carrier))
				{
					return null;
				}

				if (!ToddlerCarryingUtility.IsValidCarrier(carrier))
				{
					return null;
				}

				if (ToddlerCarryingUtility.GetCarriedToddlerCount(carrier) >= ToddlerCarryingUtility.GetMaxCarryCapacity(carrier))
				{
					return null;
				}

				if (ToddlerCarryingUtility.IsBeingCarried(clickedPawn))
				{
					return null;
				}

				if (carrier.Faction == null || (clickedPawn.Faction != null && clickedPawn.Faction.HostileTo(carrier.Faction)))
				{
					return null;
				}

				if (!carrier.CanReserveAndReach(clickedPawn, PathEndMode.ClosestTouch, Danger.Deadly))
				{
					return new FloatMenuOption(
						"RimTalk_PickUpToddler".Translate(clickedPawn.LabelShort) + ": " + "NoPath".Translate().CapitalizeFirst(),
						null);
				}

				if (!carrier.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
				{
					return new FloatMenuOption(
						"RimTalk_PickUpToddler".Translate(clickedPawn.LabelShort) + ": " + "Incapable".Translate().CapitalizeFirst(),
						null);
				}

				Pawn toddlerCopy = clickedPawn;
				return FloatMenuUtility.DecoratePrioritizedTask(
					new FloatMenuOption("RimTalk_PickUpToddler".Translate(clickedPawn.LabelShort), delegate
					{
						toddlerCopy.SetForbidden(value: false, warnOnFail: false);
						Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_PickUpToddler, toddlerCopy);
						job.count = 1;
						carrier.jobs.TryTakeOrderedJob(job, JobTag.Misc);
					}),
					carrier,
					clickedPawn);
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"{LogPrefix} GetSingleOptionFor failed: {ex.Message}");
				}

				return null;
			}
		}

		private static bool IsToddlerOrBaby(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby())
			{
				return true;
			}

			if (pawn.ageTracker?.CurLifeStage == null)
			{
				return false;
			}

			string lifeStageName = pawn.ageTracker.CurLifeStage.defName;
			return lifeStageName == "HumanlikeToddler"
				|| (!lifeStageName.NullOrEmpty() && lifeStageName.IndexOf("Toddler", StringComparison.OrdinalIgnoreCase) >= 0);
		}
	}
}
