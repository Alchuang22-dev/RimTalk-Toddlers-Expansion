using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
    /// <summary>
    /// Patches for the NatureRunning learning activity.
    /// When a Child starts nature running, nearby children and toddlers will follow them.
    /// </summary>
    public static class Patch_LearningGiver_NatureRunning
    {
        /// <summary>
        /// Recruitment radius for finding nearby children to follow
        /// </summary>
        private const float RecruitmentRadius = 10f;
        
        /// <summary>
        /// Cached NatureRunning JobDef (looked up at runtime)
        /// </summary>
        private static JobDef _natureRunningJobDef;
        
        /// <summary>
        /// Field info for accessing Pawn_JobTracker.pawn
        /// </summary>
        private static FieldInfo _pawnJobTrackerPawnField;
        
        /// <summary>
        /// Get the NatureRunning JobDef
        /// </summary>
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
        
        public static void Init(HarmonyLib.Harmony harmony)
        {
            // Patch TryGiveJob to recruit followers after job is given
            var tryGiveJobTarget = AccessTools.Method(typeof(LearningGiver_NatureRunning), nameof(LearningGiver_NatureRunning.TryGiveJob));
            if (tryGiveJobTarget != null)
            {
                harmony.Patch(tryGiveJobTarget, 
                    postfix: new HarmonyMethod(typeof(Patch_LearningGiver_NatureRunning), nameof(TryGiveJob_Postfix)));
            }
            else
            {
                Log.Warning("[RimTalk_ToddlersExpansion] Could not find LearningGiver_NatureRunning.TryGiveJob.");
            }
            
            // Also patch Pawn_JobTracker.StartJob to catch when the job actually starts
            var startJobTarget = AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob));
            if (startJobTarget != null)
            {
                harmony.Patch(startJobTarget,
                    postfix: new HarmonyMethod(typeof(Patch_LearningGiver_NatureRunning), nameof(StartJob_Postfix)));
            }
        }
        
        /// <summary>
        /// Called after TryGiveJob - we'll use StartJob instead for more reliable detection
        /// </summary>
        private static void TryGiveJob_Postfix(Pawn pawn, ref Job __result)
        {
            // We handle the actual recruitment in StartJob_Postfix
            // This is just here for potential future use
        }
        
        /// <summary>
        /// Get the pawn from a Pawn_JobTracker using reflection
        /// </summary>
        private static Pawn GetPawnFromJobTracker(Pawn_JobTracker jobTracker)
        {
            if (_pawnJobTrackerPawnField == null)
            {
                _pawnJobTrackerPawnField = typeof(Pawn_JobTracker).GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            
            if (_pawnJobTrackerPawnField != null)
            {
                return _pawnJobTrackerPawnField.GetValue(jobTracker) as Pawn;
            }
            
            return null;
        }
        
        /// <summary>
        /// Called after a job is started - recruit nearby children if this is a NatureRunning job
        /// </summary>
        private static void StartJob_Postfix(Pawn_JobTracker __instance, Job newJob)
        {
            if (newJob == null || NatureRunningJobDef == null || newJob.def != NatureRunningJobDef)
            {
                return;
            }
            
            Pawn leader = GetPawnFromJobTracker(__instance);
            if (leader == null || leader.Map == null)
            {
                return;
            }
            
            // Only children (not babies/toddlers) can be the leader of nature running
            if (!leader.DevelopmentalStage.Child())
            {
                return;
            }
            
            // Only player faction
            if (leader.Faction != Faction.OfPlayer)
            {
                return;
            }
            
            // Recruit nearby children and toddlers
            RecruitNearbyFollowers(leader);
        }
        
        /// <summary>
        /// Find and recruit nearby children and toddlers to follow the nature runner
        /// </summary>
        private static void RecruitNearbyFollowers(Pawn leader)
        {
            if (leader?.Map == null)
            {
                return;
            }
            
            // Find all nearby children and toddlers
            List<Pawn> nearbyChildren = new List<Pawn>();
            
            foreach (Pawn pawn in leader.Map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn == leader)
                {
                    continue;
                }
                
                // Must be a child or toddler
                if (!IsChildOrToddler(pawn))
                {
                    continue;
                }
                
                // Must be within recruitment radius
                if (!pawn.Position.InHorDistOf(leader.Position, RecruitmentRadius))
                {
                    continue;
                }
                
                // Must be able to follow (not downed, not in mental state, etc.)
                if (!CanFollow(pawn))
                {
                    continue;
                }
                
                // Must not already be following someone
                if (IsAlreadyFollowing(pawn))
                {
                    continue;
                }
                
                nearbyChildren.Add(pawn);
            }
            
            // Give each nearby child the follow job
            foreach (Pawn follower in nearbyChildren)
            {
                GiveFollowJob(follower, leader);
            }
            
            if (nearbyChildren.Count > 0)
            {
                Log.Message($"[RimTalk_ToddlersExpansion] {leader.LabelShort} started nature running, {nearbyChildren.Count} children/toddlers are following.");
            }
        }
        
        /// <summary>
        /// Check if a pawn is a child or toddler
        /// </summary>
        private static bool IsChildOrToddler(Pawn pawn)
        {
            if (pawn.DevelopmentalStage.Child())
            {
                return true;
            }
            
            // Check for toddler (Baby stage + Toddlers mod)
            if (ToddlersCompatUtility.IsToddler(pawn))
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if a pawn can follow (basic availability checks)
        /// </summary>
        private static bool CanFollow(Pawn pawn)
        {
            if (pawn.Downed || pawn.Dead || !pawn.Spawned)
            {
                return false;
            }
            
            if (pawn.InMentalState)
            {
                return false;
            }
            
            if (pawn.IsBurning())
            {
                return false;
            }
            
            // Check if pawn is drafted (shouldn't happen for children, but just in case)
            if (pawn.Drafted)
            {
                return false;
            }
            
            // Check needs thresholds - don't recruit if already hungry or tired
            if (pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < 0.15f)
            {
                return false;
            }
            
            if (pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage < 0.15f)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if pawn is already following someone (has our follow job)
        /// </summary>
        private static bool IsAlreadyFollowing(Pawn pawn)
        {
            Job curJob = pawn.CurJob;
            if (curJob == null)
            {
                return false;
            }
            
            // Check if already following someone
            if (curJob.def == ToddlersExpansionJobDefOf.RimTalk_FollowNatureRunner)
            {
                return true;
            }
            
            // Check if already nature running themselves
            if (NatureRunningJobDef != null && curJob.def == NatureRunningJobDef)
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Give the follow job to a pawn
        /// </summary>
        private static void GiveFollowJob(Pawn follower, Pawn leader)
        {
            if (ToddlersExpansionJobDefOf.RimTalk_FollowNatureRunner == null)
            {
                Log.Error("[RimTalk_ToddlersExpansion] RimTalk_FollowNatureRunner JobDef not found!");
                return;
            }
            
            Job followJob = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_FollowNatureRunner, leader);
            followJob.locomotionUrgency = LocomotionUrgency.Jog;
            
            // Start the job, interrupting current job
            follower.jobs.StartJob(followJob, JobCondition.InterruptForced);
        }
    }
}
