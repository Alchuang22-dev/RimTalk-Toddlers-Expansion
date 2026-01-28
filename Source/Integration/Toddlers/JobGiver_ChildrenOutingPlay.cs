using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
    /// <summary>
    /// JobGiver for children's outing play activities.
    /// Randomly selects a play activity for children during the outing.
    /// </summary>
    public class JobGiver_ChildrenOutingPlay : ThinkNode_JobGiver
    {
        
        /// <summary>
        /// Duration range for play activities (in ticks)
        /// </summary>
        private static readonly IntRange PlayDurationRange = new IntRange(500, 1500);
        
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned)
            {
                return null;
            }
            
            // Get available play jobs for this specific pawn
            List<JobDef> playJobs = GetAvailablePlayJobs(pawn);
            if (playJobs.Count == 0)
            {
                return null;
            }
            
            // Try to find a partner for mutual play
            Pawn partner = FindPlayPartner(pawn);
            
            // Select a random play activity
            JobDef selectedJob = SelectPlayJob(pawn, partner, playJobs);
            if (selectedJob == null)
            {
                return null;
            }
            
            // Create the job
            Job job = CreatePlayJob(pawn, selectedJob, partner);
            return job;
        }
        
        /// <summary>
        /// Get list of available play JobDefs for a specific pawn
        /// </summary>
        private List<JobDef> GetAvailablePlayJobs(Pawn pawn)
        {
            List<JobDef> availableJobs = new List<JobDef>();
            
            bool isToddler = ToddlersCompatUtility.IsToddler(pawn);
            bool isChild = pawn.DevelopmentalStage.Child();
            
            // RimTalk play jobs - suitable for both toddlers and children
            AddJobDefIfExists(availableJobs, "RimTalk_ToddlerSelfPlayJob");
            
            // Mutual play is suitable for both
            AddJobDefIfExists(availableJobs, "RimTalk_ToddlerMutualPlayJob");
            
            // Toddlers mod play jobs - ONLY for toddlers
            if (isToddler)
            {
                AddJobDefIfExists(availableJobs, "ToddlerFloordrawing");
                AddJobDefIfExists(availableJobs, "ToddlerSkydreaming");
                AddJobDefIfExists(availableJobs, "ToddlerBugwatching");
            }
            
            // Vanilla child play jobs - ONLY for children
            if (isChild && !isToddler)
            {
                AddJobDefIfExists(availableJobs, "ChildPlayGround");
            }
            
            return availableJobs;
        }
        
        /// <summary>
        /// Add a JobDef to the list if it exists
        /// </summary>
        private void AddJobDefIfExists(List<JobDef> list, string defName)
        {
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail(defName);
            if (jobDef != null)
            {
                list.Add(jobDef);
            }
        }
        
        /// <summary>
        /// Find a play partner from the same lord
        /// </summary>
        private Pawn FindPlayPartner(Pawn pawn)
        {
            if (pawn.GetLord() == null)
            {
                return null;
            }
            
            List<Pawn> lordPawns = pawn.GetLord().ownedPawns;
            List<Pawn> candidates = new List<Pawn>();
            
            foreach (Pawn otherPawn in lordPawns)
            {
                if (otherPawn == pawn)
                {
                    continue;
                }
                
                if (!otherPawn.Spawned || otherPawn.Downed || otherPawn.Dead)
                {
                    continue;
                }
                
                // Check if nearby
                if (!pawn.Position.InHorDistOf(otherPawn.Position, 8f))
                {
                    continue;
                }
                
                candidates.Add(otherPawn);
            }
            
            if (candidates.TryRandomElement(out Pawn partner))
            {
                return partner;
            }
            
            return null;
        }
        
        /// <summary>
        /// Select an appropriate play job
        /// </summary>
        private JobDef SelectPlayJob(Pawn pawn, Pawn partner, List<JobDef> playJobs)
        {
            List<JobDef> validJobs = new List<JobDef>();
            
            foreach (JobDef jobDef in playJobs)
            {
                // Check if job requires a partner
                if (JobRequiresPartner(jobDef))
                {
                    if (partner != null)
                    {
                        validJobs.Add(jobDef);
                    }
                }
                else
                {
                    validJobs.Add(jobDef);
                }
            }
            
            if (validJobs.TryRandomElement(out JobDef selected))
            {
                return selected;
            }
            
            return null;
        }
        
        /// <summary>
        /// Check if a job requires a partner
        /// </summary>
        private bool JobRequiresPartner(JobDef jobDef)
        {
            if (jobDef.defName.Contains("Mutual"))
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Create the play job
        /// </summary>
        private Job CreatePlayJob(Pawn pawn, JobDef jobDef, Pawn partner)
        {
            Job job;
            
            if (partner != null && JobRequiresPartner(jobDef))
            {
                job = JobMaker.MakeJob(jobDef, partner);
            }
            else
            {
                // For self-play or solo activities, find a nearby cell
                IntVec3 playCell = FindPlayCell(pawn);
                if (!playCell.IsValid)
                {
                    playCell = pawn.Position;
                }
                job = JobMaker.MakeJob(jobDef, playCell);
            }
            
            // Set random duration if not specified
            if (job != null)
            {
                job.expiryInterval = PlayDurationRange.RandomInRange;
            }
            
            return job;
        }
        
        /// <summary>
        /// Find a cell to play at
        /// </summary>
        private IntVec3 FindPlayCell(Pawn pawn)
        {
            // Try to find a nice spot nearby
            if (CellFinder.TryFindRandomReachableNearbyCell(
                pawn.Position, 
                pawn.Map, 
                5f, 
                TraverseParms.For(pawn), 
                c => c.Standable(pawn.Map) && !c.IsForbidden(pawn), 
                null, 
                out IntVec3 result))
            {
                return result;
            }
            
            return IntVec3.Invalid;
        }
    }
}