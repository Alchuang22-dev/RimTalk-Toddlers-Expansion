using System.Collections.Generic;
using System.Linq;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
    /// <summary>
    /// Data class for tracking children outing participation
    /// </summary>
    public class LordToilData_ChildrenOuting : LordToilData
    {
        public Dictionary<Pawn, int> presentForTicks = new Dictionary<Pawn, int>();
        
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref presentForTicks, "presentForTicks", LookMode.Reference, LookMode.Value);
            if (presentForTicks == null)
            {
                presentForTicks = new Dictionary<Pawn, int>();
            }
        }
    }
    
    /// <summary>
    /// Lord toil for children's outing.
    /// Based on LordToil_Party / LordToil_Gathering.
    /// </summary>
    public class LordToil_ChildrenOuting : LordToil
    {
        protected IntVec3 spot;
        protected GatheringDef gatheringDef;
        
        /// <summary>
        /// Joy gain per tick for participants in the gathering area
        /// </summary>
        private const float JoyPerTick = 3.5E-05f;
        
        /// <summary>
        /// Hunger threshold for exiting
        /// </summary>
        private const float HungerExitThreshold = 0.1f;
        
        /// <summary>
        /// Rest threshold for exiting
        /// </summary>
        private const float RestExitThreshold = 0.1f;
        
        public LordToilData_ChildrenOuting Data => (LordToilData_ChildrenOuting)data;
        
        public LordToil_ChildrenOuting(IntVec3 spot, GatheringDef gatheringDef)
        {
            this.spot = spot;
            this.gatheringDef = gatheringDef;
            data = new LordToilData_ChildrenOuting();
        }
        
        public override ThinkTreeDutyHook VoluntaryJoinDutyHookFor(Pawn p)
        {
            if (gatheringDef?.duty != null)
            {
                return gatheringDef.duty.hook;
            }
            return ThinkTreeDutyHook.MediumPriority;
        }
        
        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (gatheringDef?.duty != null)
                {
                    pawn.mindState.duty = new PawnDuty(gatheringDef.duty, spot);
                }
                else
                {
                	// Fallback: simple wander duty (use our DefOf since Party isn't in vanilla DutyDefOf)
                	pawn.mindState.duty = new PawnDuty(ToddlersExpansionDutyDefOf.Party, spot);
                }
            }
        }
        
        public override void LordToilTick()
        {
            List<Pawn> ownedPawns = lord.ownedPawns;
            
            // Check each pawn
            for (int i = ownedPawns.Count - 1; i >= 0; i--)
            {
                Pawn pawn = ownedPawns[i];
                
                // Check if in gathering area
                if (GatheringsUtility.InGatheringArea(pawn.Position, spot, base.Map))
                {
                    // Track presence time
                    if (!Data.presentForTicks.ContainsKey(pawn))
                    {
                        Data.presentForTicks.Add(pawn, 0);
                    }
                    Data.presentForTicks[pawn]++;
                    
                    // Give joy (social)
                    pawn.needs?.joy?.GainJoy(JoyPerTick, JoyKindDefOf.Social);
                }
                
                // Check exit conditions every 60 ticks
                if (Find.TickManager.TicksGame % 60 == 0)
                {
                    if (ShouldPawnExit(pawn))
                    {
                        lord.Notify_PawnLost(pawn, PawnLostCondition.LeftVoluntarily);
                    }
                }
            }
        }
        
        /// <summary>
        /// Check if a pawn should exit the outing due to needs
        /// </summary>
        private bool ShouldPawnExit(Pawn pawn)
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
            
            // Downed check
            if (pawn.Downed)
            {
                return true;
            }
            
            // Mental state check
            if (ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
            {
                return true;
            }
            
            return false;
        }
    }
}
