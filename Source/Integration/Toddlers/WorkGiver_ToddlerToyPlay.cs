using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class WorkGiver_ToddlerToyPlay : JoyGiver
	{
		private const float PlayNeedThreshold = 0.9f;
		private const float SearchRadius = 30f;

		private static bool _walkHediffChecked;
		private static HediffDef _learningToWalkDef;

		public override Job TryGiveJob(Pawn pawn)
		{
			if (pawn?.Map == null || !IsEligiblePawn(pawn))
			{
				return null;
			}

			Need_Play play = pawn.needs?.play;
			if (play != null && play.CurLevelPercentage >= PlayNeedThreshold)
			{
				return null;
			}

			Need_Joy joy = pawn.needs?.joy;
			if (play == null && joy != null && joy.CurLevelPercentage >= PlayNeedThreshold)
			{
				return null;
			}

			Thing toy = FindBestToy(pawn);
			if (toy == null)
			{
				return null;
			}

			CompToddlerToy comp = toy.TryGetComp<CompToddlerToy>();
			Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_ToddlerPlayAtToy, toy);
			job.ignoreJoyTimeAssignment = false;
			if (comp != null && comp.UseDurationTicks > 0)
			{
				job.expiryInterval = comp.UseDurationTicks;
			}

			return job;
		}

		private static Thing FindBestToy(Pawn pawn)
		{
			Map map = pawn.Map;
			bool groundOnly = RequiresGroundToy(pawn);

			return GenClosest.ClosestThingReachable(
				pawn.Position,
				map,
				ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
				PathEndMode.InteractionCell,
				TraverseParms.For(pawn, Danger.Some),
				SearchRadius,
				thing =>
				{
					if (thing is not Building building)
					{
						return false;
					}

					CompToddlerToy comp = building.TryGetComp<CompToddlerToy>();
					if (comp == null || !comp.Allows(pawn))
					{
						return false;
					}

					if (groundOnly && !comp.GroundToy)
					{
						return false;
					}

					return pawn.CanReserveAndReach(building, PathEndMode.InteractionCell, Danger.Some);
				});
		}

		private static bool IsEligiblePawn(Pawn pawn)
		{
			if (pawn?.RaceProps?.Humanlike != true || pawn.Downed || pawn.Drafted || pawn.InMentalState)
			{
				return false;
			}

			return pawn.DevelopmentalStage.Newborn()
				|| pawn.DevelopmentalStage.Baby()
				|| ToddlersCompatUtility.IsToddler(pawn)
				|| pawn.DevelopmentalStage == DevelopmentalStage.Child;
		}

		private static bool RequiresGroundToy(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby())
			{
				return true;
			}

			if (!ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			EnsureWalkDef();
			if (_learningToWalkDef == null || pawn.health?.hediffSet == null)
			{
				return false;
			}

			Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(_learningToWalkDef);
			return hediff != null && hediff.Severity < 0.5f;
		}

		private static void EnsureWalkDef()
		{
			if (_walkHediffChecked)
			{
				return;
			}

			_walkHediffChecked = true;
			_learningToWalkDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningToWalk");
		}
	}
}
