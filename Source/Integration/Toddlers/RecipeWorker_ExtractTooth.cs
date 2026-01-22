using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
    public class RecipeWorker_ExtractTooth : Recipe_RemoveHediff
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);

            if (pawn?.needs?.mood?.thoughts?.memories == null)
                return;

            var thoughtDef = GetDentistThoughtDefForPawn(pawn);
            if (thoughtDef != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
            }

            var missingToothHediff = HediffMaker.MakeHediff(Core.ToddlersExpansionHediffDefOf.RimTalk_MissingTooth, pawn);
            pawn.health.AddHediff(missingToothHediff);
        }

        private ThoughtDef GetDentistThoughtDefForPawn(Pawn pawn)
        {
            if (ToddlersCompatUtility.IsToddler(pawn))
                return Core.ToddlersExpansionThoughtDefOf.RimTalk_VisitedDentist_Toddler;
            else if (pawn.DevelopmentalStage == DevelopmentalStage.Baby)
                return Core.ToddlersExpansionThoughtDefOf.RimTalk_VisitedDentist_Baby;
            else
                return Core.ToddlersExpansionThoughtDefOf.RimTalk_VisitedDentist_Child;
        }
    }
}
