using System;
using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
    /// <summary>
    /// Lord job for children's outing gathering.
    /// Based on LordJob_Joinable_Party.
    /// </summary>
    public class LordJob_ChildrenOuting : LordJob_VoluntarilyJoinable
    {
        protected IntVec3 spot;
        protected Pawn organizer;
        protected GatheringDef gatheringDef;
        protected int durationTicks;
        protected Trigger_TicksPassed timeoutTrigger;
        
        /// <summary>
        /// Hunger threshold - participants leave if below 10%
        /// </summary>
        private const float HungerExitThreshold = 0.1f;
        
        /// <summary>
        /// Rest threshold - participants leave if below 10%
        /// </summary>
        private const float RestExitThreshold = 0.1f;
        
        public override bool AllowStartNewGatherings => false;
        
        public Pawn Organizer => organizer;
        public int DurationTicks => durationTicks;
        public virtual int TicksLeft => timeoutTrigger?.TicksLeft ?? 0;
        public virtual IntVec3 Spot => spot;
        
        protected virtual ThoughtDef AttendeeThought => ToddlersExpansionThoughtDefOf.RimTalk_AttendedChildrenOuting;
        protected virtual ThoughtDef OrganizerThought => ToddlersExpansionThoughtDefOf.RimTalk_OrganizedChildrenOuting;
        
        public LordJob_ChildrenOuting()
        {
        }
        
        public LordJob_ChildrenOuting(IntVec3 spot, Pawn organizer, GatheringDef gatheringDef)
        {
            this.spot = spot;
            this.organizer = organizer;
            this.gatheringDef = gatheringDef;
            // Duration: 5000-15000 ticks (approximately 2-6 minutes in game)
            durationTicks = Rand.RangeInclusive(5000, 15000);
        }
        
        public override string GetReport(Pawn pawn)
        {
            return "RimTalk_ChildrenOutingReport".Translate();
        }
        
        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            
            // Main outing toil
            LordToil outingToil = CreateGatheringToil(spot, organizer, gatheringDef);
            stateGraph.AddToil(outingToil);
            
            // End toil
            LordToil_End endToil = new LordToil_End();
            stateGraph.AddToil(endToil);
            
            // Transition: Called off (danger, organizer issues, etc.)
            Transition calledOffTransition = new Transition(outingToil, endToil);
            calledOffTransition.AddTrigger(new Trigger_TickCondition(ShouldBeCalledOff));
            calledOffTransition.AddTrigger(new Trigger_PawnKilled());
            calledOffTransition.AddTrigger(new Trigger_PawnLost(PawnLostCondition.LeftVoluntarily, organizer));
            calledOffTransition.AddPreAction(new TransitionAction_Custom((Action)delegate
            {
                ApplyOutcome((LordToil_ChildrenOuting)outingToil);
            }));
            calledOffTransition.AddPreAction(new TransitionAction_Message(
                gatheringDef?.calledOffMessage ?? "The children's outing has been called off.", 
                MessageTypeDefOf.NegativeEvent, 
                new TargetInfo(spot, base.Map)));
            stateGraph.AddTransition(calledOffTransition);
            
            // Transition: Timeout (normal end)
            timeoutTrigger = new Trigger_TicksPassed(durationTicks);
            Transition timeoutTransition = new Transition(outingToil, endToil);
            timeoutTransition.AddTrigger(timeoutTrigger);
            timeoutTransition.AddPreAction(new TransitionAction_Custom((Action)delegate
            {
                ApplyOutcome((LordToil_ChildrenOuting)outingToil);
            }));
            timeoutTransition.AddPreAction(new TransitionAction_Message(
                gatheringDef?.finishedMessage ?? "The children's outing has finished.", 
                MessageTypeDefOf.SituationResolved, 
                new TargetInfo(spot, base.Map)));
            stateGraph.AddTransition(timeoutTransition);
            
            return stateGraph;
        }
        
        protected virtual LordToil CreateGatheringToil(IntVec3 spot, Pawn organizer, GatheringDef gatheringDef)
        {
            return new LordToil_ChildrenOuting(spot, gatheringDef);
        }
        
        protected virtual bool ShouldBeCalledOff()
        {
            // Check if organizer can continue
            if (organizer != null && !CanPawnContinueOuting(organizer))
            {
                return true;
            }
            
            // Check for danger
            if (base.Map != null && base.Map.dangerWatcher.DangerRating == StoryDanger.High)
            {
                return true;
            }
            
            return false;
        }
        
        public override float VoluntaryJoinPriorityFor(Pawn p)
        {
            // Only children and toddlers can join
            if (!IsChildOrToddler(p))
            {
                return 0f;
            }
            
            // Check if pawn should keep participating
            if (!ShouldPawnKeepOuting(p))
            {
                return 0f;
            }
            
            // Check if spot is forbidden
            if (spot.IsForbidden(p))
            {
                return 0f;
            }
            
            // Don't join if almost over
            if (!lord.ownedPawns.Contains(p) && IsGatheringAboutToEnd())
            {
                return 0f;
            }
            
            return VoluntarilyJoinableLordJobJoinPriorities.SocialGathering;
        }
        
        /// <summary>
        /// Check if a pawn is a child or toddler
        /// </summary>
        private bool IsChildOrToddler(Pawn p)
        {
            if (p.DevelopmentalStage.Child())
            {
                return true;
            }
            
            if (ToddlersCompatUtility.IsToddler(p))
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if pawn should keep participating in the outing
        /// </summary>
        private bool ShouldPawnKeepOuting(Pawn p)
        {
            if (p.Downed || p.Dead || !p.Spawned)
            {
                return false;
            }
            
            if (ToddlerMentalStateUtility.HasBlockingMentalState(p))
            {
                return false;
            }
            
            // Hunger check
            if (p.needs?.food != null && p.needs.food.CurLevelPercentage < HungerExitThreshold)
            {
                return false;
            }
            
            // Rest check
            if (p.needs?.rest != null && p.needs.rest.CurLevelPercentage < RestExitThreshold)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if pawn can continue the outing (for organizer check)
        /// </summary>
        private bool CanPawnContinueOuting(Pawn p)
        {
            if (p.Downed || p.Dead || !p.Spawned)
            {
                return false;
            }
            
            if (ToddlerMentalStateUtility.HasBlockingMentalState(p))
            {
                return false;
            }
            
            // Organizer being drafted cancels the outing
            if (p.Drafted)
            {
                return false;
            }
            
            return true;
        }
        
        protected bool IsGatheringAboutToEnd()
        {
            return TicksLeft < 1200;
        }
        
        /// <summary>
        /// Apply outcome when outing ends - give thoughts to participants
        /// </summary>
        private void ApplyOutcome(LordToil_ChildrenOuting toil)
        {
            if (toil?.Data == null)
            {
                return;
            }
            
            List<Pawn> ownedPawns = lord.ownedPawns;
            LordToilData_ChildrenOuting data = toil.Data;
            
            for (int i = 0; i < ownedPawns.Count; i++)
            {
                Pawn pawn = ownedPawns[i];
                bool isOrganizer = pawn == organizer;
                
                if (data.presentForTicks.TryGetValue(pawn, out int ticksPresent) && ticksPresent > 0)
                {
                    if (pawn.needs?.mood != null)
                    {
                        ThoughtDef thoughtDef = isOrganizer ? OrganizerThought : AttendeeThought;
                        if (thoughtDef != null)
                        {
                            // Calculate mood power based on attendance time
                            float baseFactor = 0.5f / thoughtDef.stages[0].baseMoodEffect;
                            float moodPowerFactor = Mathf.Min((float)ticksPresent / (float)durationTicks + baseFactor, 1f);
                            
                            Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(thoughtDef);
                            thought.moodPowerFactor = moodPowerFactor;
                            pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
                        }
                    }
                }
            }
        }
        
        public override void ExposeData()
        {
            Scribe_Values.Look(ref spot, "spot");
            Scribe_Values.Look(ref durationTicks, "durationTicks", 0);
            Scribe_References.Look(ref organizer, "organizer");
            Scribe_Defs.Look(ref gatheringDef, "gatheringDef");
        }
    }
}
