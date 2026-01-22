using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
    public class HediffCompProperties_ToothDecayProgress : HediffCompProperties
    {
        public HediffCompProperties_ToothDecayProgress()
        {
            compClass = typeof(HediffComp_ToothDecayProgress);
        }
    }

    public class HediffComp_ToothDecayProgress : HediffComp
    {
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);

            if (parent.pawn == null)
                return;

            var existingHediff = parent.pawn.health.hediffSet.GetFirstHediffOfDef(parent.def);
            if (existingHediff != null && existingHediff != parent)
            {
                if (existingHediff.Severity < 1.5f)
                {
                    existingHediff.Severity += 0.3f;
                    parent.pawn.health.RemoveHediff(parent);
                }
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (parent.pawn.IsHashIntervalTick(60000))
            {
                if (parent.Severity < 1.5f)
                {
                    severityAdjustment = -0.01f;
                }
            }
        }

        public override void CompTended(float quality, float maxQuality, int batchPosition = 0)
        {
            base.CompTended(quality, maxQuality, batchPosition);

            if (parent.Severity > 0f)
            {
                float reduction = 0.2f + (quality * 0.3f);
                parent.Severity -= reduction;

                if (parent.Severity <= 0f)
                {
                    parent.pawn.health.RemoveHediff(parent);
                }
            }
        }
    }
}
