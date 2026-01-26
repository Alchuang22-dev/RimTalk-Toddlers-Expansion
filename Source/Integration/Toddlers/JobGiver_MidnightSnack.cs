using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
    public class JobGiver_MidnightSnack : ThinkNode_JobGiver
    {
        private static readonly SimpleCurve FoodScoreCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(0.7f, 1f)
        };

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ShouldAttemptMidnightSnack(pawn))
                return null;

            var (food, score) = FindBestFoodTarget(pawn);
            if (food == null || score < 0.1f)
                return null;

            if (!pawn.CanReserveAndReach(food, PathEndMode.ClosestTouch, Danger.None))
                return null;

            var job = JobMaker.MakeJob(Core.ToddlersExpansionJobDefOf.RimTalk_MidnightSnack, food);
            job.count = Mathf.Min(food.def.ingestible.maxNumToIngestAtOnce, food.stackCount);

            ApplyCooldown(pawn);

            return job;
        }

        private bool ShouldAttemptMidnightSnack(Pawn pawn)
        {
            if (!IsValidPawn(pawn))
                return false;

            if (HasCooldown(pawn))
                return false;

            if (IsInBadCondition(pawn))
                return false;

            if (!HasCookingFoodAvailable(pawn))
                return false;

            return true;
        }

        private bool IsValidPawn(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned)
                return false;

            if (pawn.Faction != Faction.OfPlayer)
                return false;

            if (pawn.Downed || pawn.InBed())
                return false;

            if (ToddlersCompatUtility.IsToddler(pawn))
                return true;

            if (pawn.DevelopmentalStage == DevelopmentalStage.Child)
                return true;

            return false;
        }

        private bool HasCooldown(Pawn pawn)
        {
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(Core.ToddlersExpansionHediffDefOf.RimTalk_MidnightSnackCooldown);
            return hediff != null;
        }

        private void ApplyCooldown(Pawn pawn)
        {
            var hediff = HediffMaker.MakeHediff(Core.ToddlersExpansionHediffDefOf.RimTalk_MidnightSnackCooldown, pawn);
            hediff.Severity = 1f;
            pawn.health.AddHediff(hediff);
        }

        private bool IsInBadCondition(Pawn pawn)
        {
            if (pawn.needs.food.Starving)
                return true;

            if (HealthAIUtility.ShouldSeekMedicalRest(pawn))
                return true;

            if (pawn.health.hediffSet.hediffs.Any(h => h.def.isBad && h.Severity > 0.5f))
                return true;

            return false;
        }

        private bool HasCookingFoodAvailable(Pawn pawn)
        {
            return FindBestFoodTarget(pawn).food != null;
        }

        private (Thing food, float score) FindBestFoodTarget(Pawn pawn)
        {
            Thing bestFood = null;
            float bestScore = 0f;

            var allFood = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);

            foreach (var food in allFood)
            {
                if (!IsValidFood(pawn, food))
                    continue;

                float score = CalculateFoodScore(pawn, food);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestFood = food;
                }
            }

            return (bestFood, bestScore);
        }

        private bool IsValidFood(Pawn pawn, Thing food)
        {
            if (food == null || food.def == null || food.def.ingestible == null)
                return false;

            if (food.IsForbidden(pawn))
                return false;

            if (food.def.ingestible.preferability < FoodPreferability.MealAwful)
                return false;

            if (food.def.ingestible.foodType == FoodTypeFlags.Tree ||
                food.def.ingestible.foodType == FoodTypeFlags.Plant)
                return false;

            if (!pawn.CanReserve(food))
                return false;

            if (!pawn.CanReach(food, PathEndMode.ClosestTouch, Danger.None))
                return false;

            return true;
        }

        private float CalculateFoodScore(Pawn pawn, Thing food)
        {
            float score = FoodUtility.FoodOptimality(pawn, food, food.def, 0f, false);

            float distance = pawn.Position.DistanceTo(food.Position);
            float distanceFactor = Mathf.Clamp01(1f - distance / 50f);

            return score * distanceFactor;
        }
    }

    public class HediffCompProperties_MidnightSnackCooldown : HediffCompProperties
    {
        public HediffCompProperties_MidnightSnackCooldown()
        {
            compClass = typeof(HediffComp_MidnightSnackCooldown);
        }
    }

    public class HediffComp_MidnightSnackCooldown : HediffComp
    {
        private const float DecayRatePerDay = 1f / 3f;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (parent.pawn.IsHashIntervalTick(60000))
            {
                severityAdjustment = -DecayRatePerDay;
            }
        }
    }

    public static class MidnightSnackTracker
    {
        private static Dictionary<Pawn, int> lastSnackTicks = new Dictionary<Pawn, int>();

        public static bool CanAttemptSnack(Pawn pawn)
        {
            if (!lastSnackTicks.ContainsKey(pawn))
                return true;

            int ticksSinceLastSnack = Find.TickManager.TicksGame - lastSnackTicks[pawn];
            return ticksSinceLastSnack > GenDate.TicksPerDay * 2;
        }

        public static void RecordSnackAttempt(Pawn pawn)
        {
            lastSnackTicks[pawn] = Find.TickManager.TicksGame;
        }

        public static void CleanupDeadPawns()
        {
            var deadPawns = lastSnackTicks.Keys.Where(p => p.Dead).ToList();
            foreach (var pawn in deadPawns)
            {
                lastSnackTicks.Remove(pawn);
            }
        }
    }
}