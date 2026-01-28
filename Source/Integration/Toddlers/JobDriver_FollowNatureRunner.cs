using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
    /// <summary>
    /// Job driver for following a child who is nature running.
    /// Based on JobDriver_Workwatching logic from vanilla RimWorld.
    /// </summary>
    public class JobDriver_FollowNatureRunner : JobDriver
    {
        private const TargetIndex LeaderInd = TargetIndex.A;
        
        /// <summary>
        /// Follow distance in cells - stay within this distance of the leader
        /// </summary>
        private const int FollowDistance = 5;
        
        /// <summary>
        /// Cached NatureRunning JobDef
        /// </summary>
        private static JobDef _natureRunningJobDef;
        
        private static JobDef NatureRunningJobDef
        {
            get
            {
                if (_natureRunningJobDef == null)
                {
                    _natureRunningJobDef = DefDatabase<JobDef>.GetNamedSilentFail("NatureRunning");
                }
                return _natureRunningJobDef;
            }
        }
        
        /// <summary>
        /// If we've been unable to follow for this many consecutive ticks, end the job
        /// </summary>
        private const int MaxConsecutiveTicksWithoutFollowing = 120;
        
        /// <summary>
        /// Hunger threshold - exit if below this percentage (10%)
        /// </summary>
        private const float HungerExitThreshold = 0.1f;
        
        /// <summary>
        /// Rest threshold - exit if below this percentage (10%)
        /// </summary>
        private const float RestExitThreshold = 0.1f;
        
        private int consecutiveTicksUnableToFollow;
        
        /// <summary>
        /// The child we're following who is nature running
        /// </summary>
        private Pawn LeaderToFollow => (Pawn)TargetThingA;
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // No reservations needed - we're just following
            return true;
        }
        
        /// <summary>
        /// Check if the leader is still nature running
        /// </summary>
        private bool LeaderStillNatureRunning()
        {
            if (LeaderToFollow == null || LeaderToFollow.Dead || !LeaderToFollow.Spawned)
            {
                return false;
            }
            
            // Check if leader's current job is NatureRunning
            Job leaderJob = LeaderToFollow.CurJob;
            if (leaderJob == null)
            {
                return false;
            }
            
            return NatureRunningJobDef != null && leaderJob.def == NatureRunningJobDef;
        }
        
        /// <summary>
        /// Check if this pawn should exit following due to needs
        /// </summary>
        private bool ShouldExitDueToNeeds()
        {
            // Hunger check
            if (pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < HungerExitThreshold)
            {
                return true;
            }
            
            // Rest check
            if (pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage < RestExitThreshold)
            {
                return true;
            }
            
            return false;
        }
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail conditions
            this.FailOnDespawnedOrNull(LeaderInd);
            
            // Main following toil - based on JobDriver_Workwatching
            Toil followToil = ToilMaker.MakeToil("FollowNatureRunner");
            followToil.tickIntervalAction = delegate(int delta)
            {
                Pawn leader = LeaderToFollow;
                
                // Check if leader is still nature running
                if (!LeaderStillNatureRunning())
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                
                // Check if we should exit due to needs
                if (ShouldExitDueToNeeds())
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                
                // Check distance to leader
                float distanceToLeader = (leader.Position - pawn.Position).LengthHorizontal;
                bool withinFollowDistance = distanceToLeader <= FollowDistance;
                bool canReachLeader = pawn.Position.WithinRegions(leader.Position, pawn.Map, 2, TraverseParms.For(pawn));
                
                if (withinFollowDistance && canReachLeader)
                {
                    // We're close enough - reset counter and gain some joy
                    consecutiveTicksUnableToFollow = 0;
                    
                    // Give small joy gain for following (social joy from being with friend)
                    pawn.needs?.joy?.GainJoy(0.00002f * delta, JoyKindDefOf.Social);
                    
                    // Face the leader occasionally
                    if (Find.TickManager.TicksGame % 60 == 0)
                    {
                        pawn.rotationTracker?.FaceTarget(leader);
                    }
                }
                else
                {
                    // Need to move closer
                    if (!pawn.CanReach(leader, PathEndMode.Touch, Danger.Deadly) || leader.IsForbidden(pawn))
                    {
                        // Can't reach leader at all
                        consecutiveTicksUnableToFollow += delta;
                        if (consecutiveTicksUnableToFollow >= MaxConsecutiveTicksWithoutFollowing)
                        {
                            EndJobWith(JobCondition.Incompletable);
                        }
                    }
                    else if (!pawn.pather.Moving || pawn.pather.Destination != leader)
                    {
                        // Start moving toward leader
                        pawn.pather.StartPath(leader, PathEndMode.Touch);
                        consecutiveTicksUnableToFollow = 0;
                    }
                }
            };
            followToil.defaultCompleteMode = ToilCompleteMode.Never;
            followToil.socialMode = RandomSocialMode.SuperActive;
            yield return followToil;
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref consecutiveTicksUnableToFollow, "consecutiveTicksUnableToFollow", 0);
        }
        
        public override string GetReport()
        {
            if (LeaderToFollow != null)
            {
                return "RimTalk_FollowingNatureRunner".Translate(LeaderToFollow.LabelShort);
            }
            return base.GetReport();
        }
    }
}