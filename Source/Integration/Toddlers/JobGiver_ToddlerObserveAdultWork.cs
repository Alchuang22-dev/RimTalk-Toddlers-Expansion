using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class JobGiver_ToddlerObserveAdultWork : ThinkNode_JobGiver
	{
		private const float MaxDistanceToAdult = 30f;
		private const float MaxToddlerFollowDistanceSq = 25f;

		private static IEnumerable<Pawn> AdultsToObserve(Pawn toddler)
		{
			if (!toddler.Spawned)
			{
				yield break;
			}

			ThingRequest request = ThingRequest.ForGroup(ThingRequestGroup.Pawn);
			foreach (Thing thing in GenRadial.RadialDistinctThingsAround(toddler.Position, toddler.Map, MaxDistanceToAdult, true))
			{
				if (thing is Pawn pawn && ToddlerCanLearnFromAdult(toddler, pawn))
				{
					yield return pawn;
				}
			}
		}

		private static bool ToddlerCanLearnFromAdult(Pawn toddler, Pawn adult)
		{
			if (adult == null || toddler == null || adult.Dead || !adult.Spawned || adult.Destroyed)
			{
				return false;
			}

			if (adult.DevelopmentalStage.Juvenile() || adult == toddler || adult.IsForbidden(adult))
			{
				return false;
			}

			if (!adult.Awake() || adult.IsPrisonerOfColony || adult.IsPrisoner)
			{
				return false;
			}

			if (!adult.CanReach(toddler.Position, PathEndMode.OnCell, Danger.Some))
			{
				return false;
			}

			if (!ChildCanLearnFromAdultJob(adult))
			{
				return false;
			}

			return true;
		}

		private static bool ChildCanLearnFromAdultJob(Pawn adult)
		{
			Job curJob = adult.CurJob;
			if (curJob == null)
			{
				return false;
			}

			JobDef def = curJob.def;
			if (def == RimWorld.JobDefOf.Wait_Wander || def == RimWorld.JobDefOf.Wait_MaintainPosture)
			{
				return false;
			}

			RecipeDef recipe = curJob.RecipeDef;
			if (recipe?.workSkill != null)
			{
				return true;
			}

			List<SkillDef> relevantSkills = curJob.workGiverDef?.workType?.relevantSkills;
			return !relevantSkills.NullOrEmpty();
		}

		protected override Job TryGiveJob(Pawn pawn)
		{
			return InternalTryGiveJob(pawn);
		}

		internal Job InternalTryGiveJob(Pawn pawn)
		{
			if (!ToddlersCompatUtility.IsToddler(pawn) || !CanDo(pawn))
			{
				return null;
			}

			Pawn bestAdult = null;
			float bestScore = float.MinValue;

			foreach (Pawn adult in AdultsToObserve(pawn))
			{
				if (pawn.Position.DistanceToSquared(adult.Position) > MaxToddlerFollowDistanceSq)
				{
					continue;
				}

				float distance = pawn.Position.DistanceTo(adult.Position);
				float score = 1f / (distance * distance);

				if (score > bestScore)
				{
					bestScore = score;
					bestAdult = adult;
				}
			}

			if (bestAdult != null)
			{
				return JobMaker.MakeJob(Core.ToddlersExpansionJobDefOf.RimTalk_ToddlerObserveAdultWork, bestAdult);
			}

			return null;
		}

		private bool CanDo(Pawn pawn)
		{
			if (!ToddlersCompatUtility.IsToddler(pawn) || pawn.needs == null)
			{
				return false;
			}

			if (pawn.needs.joy != null)
			{
				if (pawn.needs.joy.CurLevel > 0.8f)
				{
					return false;
				}
			}

			if (PawnUtility.WillSoonHaveBasicNeed(pawn, -0.05f))
			{
				return false;
			}

			return true;
		}
	}
}
