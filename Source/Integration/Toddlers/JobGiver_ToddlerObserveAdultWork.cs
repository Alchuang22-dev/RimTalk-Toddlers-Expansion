using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class JobGiver_ToddlerObserveAdultWork : ThinkNode_JobGiver
	{
		private const float MaxDistanceToAdult = 100f;
		private const float MaxToddlerFollowDistanceSq = 25f;
		private const float MaxMovingAdultDistanceSq = 9f;
		private const int ObserveCooldownTicks = 60;
		private const int ObserveCooldownKey = 1934182754;

		private static IEnumerable<Pawn> AdultsToObserve(Pawn toddler)
		{
			if (toddler == null || !toddler.Spawned || toddler.Map == null)
			{
				yield break;
			}

			foreach (Pawn pawn in toddler.Map.mapPawns.FreeColonistsSpawned)
			{
				if (pawn == null)
				{
					continue;
				}

				float distanceSq = pawn.Position.DistanceToSquared(toddler.Position);
				if (distanceSq > MaxDistanceToAdult * MaxDistanceToAdult)
				{
					continue;
				}

				if (!ToddlerCanLearnFromAdult(toddler, pawn))
				{
					continue;
				}

				if (pawn.pather?.Moving == true && distanceSq > MaxMovingAdultDistanceSq)
				{
					continue;
				}

				yield return pawn;
			}
		}

		private static bool ToddlerCanLearnFromAdult(Pawn toddler, Pawn adult)
		{
			if (adult == null || toddler == null || adult.Dead || !adult.Spawned || adult.Destroyed)
			{
				return false;
			}

			if (adult.DevelopmentalStage.Juvenile() || adult == toddler || adult.IsForbidden(toddler))
			{
				return false;
			}

			if (!adult.Awake() || adult.IsPrisonerOfColony || adult.IsPrisoner)
			{
				return false;
			}

			if (!toddler.CanReach(adult, PathEndMode.Touch, Danger.Some))
			{
				return false;
			}

			if (!IsInterestingAdultJob(adult))
			{
				return false;
			}

			return true;
		}

		private static bool IsInterestingAdultJob(Pawn adult)
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

			return true;
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

			if (IsOnCooldown(pawn))
			{
				return null;
			}

			Pawn bestAdult = null;
			float bestScore = float.MinValue;

			foreach (Pawn adult in AdultsToObserve(pawn))
			{
				float distanceSq = pawn.Position.DistanceToSquared(adult.Position);
				if (distanceSq > MaxToddlerFollowDistanceSq)
				{
					continue;
				}

				float score = 1f / Mathf.Max(1f, distanceSq);
				if (adult.pather?.Moving == true)
				{
					score *= 0.5f;
				}

				if (score > bestScore)
				{
					bestScore = score;
					bestAdult = adult;
				}
			}

			if (bestAdult != null)
			{
				StartCooldown(pawn);
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

		private static bool IsOnCooldown(Pawn pawn)
		{
			if (pawn?.mindState?.thinkData == null)
			{
				return false;
			}

			int now = Find.TickManager?.TicksGame ?? 0;
			if (!pawn.mindState.thinkData.TryGetValue(ObserveCooldownKey, out int nextTick))
			{
				return false;
			}

			return now < nextTick;
		}

		private static void StartCooldown(Pawn pawn)
		{
			if (pawn?.mindState?.thinkData == null)
			{
				return;
			}

			int now = Find.TickManager?.TicksGame ?? 0;
			pawn.mindState.thinkData[ObserveCooldownKey] = now + ObserveCooldownTicks;
		}
	}
}
