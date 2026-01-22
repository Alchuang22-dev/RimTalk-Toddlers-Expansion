using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
    public class JobDriver_MidnightSnack : JobDriver
    {
        private const TargetIndex FoodInd = TargetIndex.A;
        private const int BiteDelay = 300;

        private int bitesTaken = 0;
        private List<Pawn> followers = new List<Pawn>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(FoodInd), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(FoodInd);
            this.FailOn(() => pawn.needs.food.CurLevelPercentage > 0.9f);

            yield return Toils_Goto.GotoThing(FoodInd, PathEndMode.ClosestTouch);

            var findFollowers = ToilMaker.MakeToil("FindFollowers");
            findFollowers.initAction = () =>
            {
                FindNearbyChildrenToFollow();
                SetupFollowerJobs();
            };
            findFollowers.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findFollowers;

            var eat = ToilMaker.MakeToil("EatFood");
            eat.initAction = () =>
            {
                var food = job.GetTarget(FoodInd).Thing;
                if (food != null && food.def.IsIngestible)
                {
                    int bites = Mathf.CeilToInt(food.def.ingestible.maxNumToIngestAtOnce / 2f);
                    eat.actor.jobs.curDriver.ticksLeftThisToil = bites * BiteDelay;
                }
            };
            eat.tickIntervalAction = delta =>
            {
                if (Find.TickManager.TicksGame % BiteDelay == 0)
                {
                    TakeBite();
                    bitesTaken++;
                }
            };
            eat.defaultCompleteMode = ToilCompleteMode.Delay;
            eat.handlingFacing = true;
            yield return eat;

            var finish = ToilMaker.MakeToil("FinishEating");
            finish.initAction = () =>
            {
                ApplyEffects();
                NotifyFollowersComplete();
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        private void FindNearbyChildrenToFollow()
        {
            followers.Clear();

            if (!Rand.Chance(0.2f))
                return;

            var nearbyChildren = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction)
                .Where(p => p != pawn &&
                           (ToddlersCompatUtility.IsToddler(p) || p.DevelopmentalStage == DevelopmentalStage.Child) &&
                           p.Position.DistanceTo(pawn.Position) <= 3f &&
                           !p.Downed && !p.Dead &&
                           p.Awake())
                .ToList();

            if (nearbyChildren.Count > 0)
            {
                var followingPawn = nearbyChildren.RandomElement();
                followers.Add(followingPawn);
            }
        }

        private void SetupFollowerJobs()
        {
            foreach (var follower in followers)
            {
                if (follower.CanReserveAndReach(pawn, PathEndMode.ClosestTouch, Danger.None))
                {
                    var followJob = JobMaker.MakeJob(JobDefOf.Follow, pawn);
                    followJob.expiryInterval = 2000;
                    follower.jobs.TryTakeOrderedJob(followJob);
                }
            }
        }

        private void TakeBite()
        {
            var food = job.GetTarget(FoodInd).Thing;
            if (food == null || food.Destroyed)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            var ingestible = food.def.ingestible;
            float nutrition = FoodUtility.GetNutrition(pawn, food, food.def) / ingestible.maxNumToIngestAtOnce;

            pawn.needs.food.CurLevel += nutrition;
            food.Ingested(pawn, nutrition);

            if (Rand.Chance(0.05f))
            {
                var existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(Core.ToddlersExpansionHediffDefOf.RimTalk_ToddlerToothDecay);
                if (existingHediff != null)
                {
                    if (existingHediff.Severity < 1.5f)
                    {
                        existingHediff.Severity += 0.1f;
                    }
                }
                else if (bitesTaken == 0)
                {
                    var toothDecay = HediffMaker.MakeHediff(Core.ToddlersExpansionHediffDefOf.RimTalk_ToddlerToothDecay, pawn);
                    pawn.health.AddHediff(toothDecay);
                }
            }
        }

        private void ApplyEffects()
        {
            var food = job.GetTarget(FoodInd).Thing;
            if (food == null || pawn.needs.joy == null)
                return;

            float joyGain = 0.1f;
            pawn.needs.joy.CurLevel += joyGain;

            var thoughtDef = GetThoughtDefForPawn();
            if (thoughtDef != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
            }
        }

        private void NotifyFollowersComplete()
        {
            foreach (var follower in followers)
            {
                if (follower?.needs?.mood?.thoughts?.memories == null)
                    continue;

                var thoughtDef = GetThoughtDefForPawn(follower);
                if (thoughtDef != null)
                {
                    follower.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
                }

                if (follower.CurJob?.def == JobDefOf.Follow &&
                    follower.CurJob.targetA.HasThing &&
                    follower.CurJob.targetA.Thing == pawn)
                {
                    follower.jobs.EndCurrentJob(JobCondition.Succeeded, true);
                }
            }
        }

        public void OnToothExtraction()
        {
            var thoughtDef = GetDentistThoughtDefForPawn();
            if (thoughtDef != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
            }
        }

        private ThoughtDef GetDentistThoughtDefForPawn()
        {
            if (ToddlersCompatUtility.IsToddler(pawn))
                return Core.ToddlersExpansionThoughtDefOf.RimTalk_VisitedDentist_Toddler;
            else if (pawn.DevelopmentalStage == DevelopmentalStage.Baby)
                return Core.ToddlersExpansionThoughtDefOf.RimTalk_VisitedDentist_Baby;
            else
                return Core.ToddlersExpansionThoughtDefOf.RimTalk_VisitedDentist_Child;
        }

        private ThoughtDef GetThoughtDefForPawn(Pawn p = null)
        {
            var targetPawn = p ?? pawn;

            if (ToddlersCompatUtility.IsToddler(targetPawn))
                return Core.ToddlersExpansionThoughtDefOf.RimTalk_MidnightSnackSuccess_Toddler;
            else if (targetPawn.DevelopmentalStage == DevelopmentalStage.Baby)
                return Core.ToddlersExpansionThoughtDefOf.RimTalk_MidnightSnackSuccess_Baby;
            else
                return Core.ToddlersExpansionThoughtDefOf.RimTalk_MidnightSnackSuccess_Child;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref bitesTaken, "bitesTaken");
            Scribe_Collections.Look(ref followers, "followers", LookMode.Reference);
        }
    }
}
