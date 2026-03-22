using RimWorld;
using Verse;

namespace RimAudio
{
    [DefOf]
    public static class RimAudioDefOf
    {
        public static PawnCapacityDef RimAudio_Hearing;
        public static StatDef RimAudio_HearingSensitivity;

        public static ThoughtDef RimAudio_RainSound;
        public static ThoughtDef RimAudio_ThunderSound;
        public static ThoughtDef RimAudio_ToxicWind;
        public static ThoughtDef RimAudio_AshWind;
        public static ThoughtDef RimAudio_CampfireCrackle;
        public static ThoughtDef RimAudio_FireCrackle;
        public static ThoughtDef RimAudio_GeneratorHum;
        public static ThoughtDef RimAudio_RiverWater;
        public static ThoughtDef RimAudio_OceanWaves;
        public static ThoughtDef RimAudio_TelevisionNoise;
        public static ThoughtDef RimAudio_LeisureNoise;
        public static ThoughtDef RimAudio_CoolerHum;
        public static ThoughtDef RimAudio_AnimalNoise;
        public static ThoughtDef RimAudio_Birdsong;
        public static ThoughtDef RimAudio_WildlifeNoise;
        public static ThoughtDef RimAudio_PredatorNoise;
        public static ThoughtDef RimAudio_PetNoise;
        public static ThoughtDef RimAudio_MechNoise;
        public static ThoughtDef RimAudio_TreeRustle;
        public static ThoughtDef RimAudio_CrowdNoise;
        public static ThoughtDef RimAudio_BabyRoomNoise;
        public static ThoughtDef RimAudio_ParentVoice;
        public static ThoughtDef RimAudio_WoundedEnemy;
        public static ThoughtDef RimAudio_CombatNoise;

        static RimAudioDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RimAudioDefOf));
        }
    }
}
