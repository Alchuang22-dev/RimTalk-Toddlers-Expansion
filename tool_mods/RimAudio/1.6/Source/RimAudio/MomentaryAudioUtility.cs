using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAudio
{
    public static class MomentaryAudioUtility
    {
        private const float MinAudibleStrength = 0.2f;

        public static void BroadcastMomentarySound(Map map, IntVec3 origin, ThoughtDef thought, float radiusMultiplier = 1f, Predicate<Pawn> validator = null, bool requireLineOfSight = true)
        {
            if (map == null || thought == null)
            {
                return;
            }

            float baseRadius = RimAudioMod.Settings?.audioRadius ?? 10f;
            float radius = Mathf.Max(1f, baseRadius * Mathf.Max(0.1f, radiusMultiplier));

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || !pawn.RaceProps.Humanlike || pawn.needs?.mood == null || !RimAudioUtility.PawnAllowedToHear(pawn))
                {
                    continue;
                }

                if (validator != null && !validator(pawn))
                {
                    continue;
                }

                float hearingFactor = RimAudioUtility.GetHearingFactor(pawn);
                if (hearingFactor <= 0f)
                {
                    continue;
                }

                float distance = pawn.Position.DistanceTo(origin);
                if (distance > radius)
                {
                    continue;
                }

                if (requireLineOfSight && !GenSight.LineOfSight(pawn.Position, origin, map, true))
                {
                    continue;
                }

                float strength = hearingFactor * Mathf.Clamp01(1f - (distance / Mathf.Max(1f, radius)));
                if (strength < MinAudibleStrength)
                {
                    continue;
                }

                RimAudioUtility.GainThought(pawn, thought, 1);
            }
        }
    }
}
