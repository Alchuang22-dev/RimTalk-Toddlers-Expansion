using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimAudio
{
    public class PawnCapacityWorker_Hearing : PawnCapacityWorker
    {
        public override float CalculateCapacityLevel(HediffSet hediffSet, List<PawnCapacityUtility.CapacityImpactor> impactors = null)
        {
            if (hediffSet?.pawn?.RaceProps?.body?.AllParts == null)
            {
                return 0f;
            }

            var hearingParts = hediffSet.pawn.RaceProps.body.AllParts.Where(IsHearingPart).ToList();
            if (hearingParts.Count == 0)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < hearingParts.Count; i++)
            {
                total += PawnCapacityUtility.CalculatePartEfficiency(hediffSet, hearingParts[i]);
            }

            return total / hearingParts.Count;
        }

        private static bool IsHearingPart(BodyPartRecord bodyPart)
        {
            if (bodyPart?.def == null)
            {
                return false;
            }

            string defName = bodyPart.def.defName ?? string.Empty;
            string label = bodyPart.def.label ?? string.Empty;
            return defName.IndexOf("ear", StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("ear", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("antenna", StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("antenna", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
