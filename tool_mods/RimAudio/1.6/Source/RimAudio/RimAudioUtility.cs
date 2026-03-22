using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAudio
{
    public static class RimAudioUtility
    {
        public static float GetHearingFactor(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return 0f;
            }

            float capacityFactor = RimAudioDefOf.RimAudio_Hearing != null ? pawn.health.capacities.GetLevel(RimAudioDefOf.RimAudio_Hearing) : 1f;
            float statFactor = RimAudioDefOf.RimAudio_HearingSensitivity != null ? pawn.GetStatValue(RimAudioDefOf.RimAudio_HearingSensitivity) : 1f;
            return Mathf.Max(0f, capacityFactor * statFactor);
        }

        public static bool PawnAllowedToHear(Pawn pawn)
        {
            var settings = RimAudioMod.Settings;
            if (pawn == null || settings == null)
            {
                return pawn != null;
            }

            if (pawn.IsColonist)
            {
                return settings.colonistsCanHear;
            }

            if (pawn.IsPrisoner)
            {
                return settings.prisonersCanHear;
            }

            if (pawn.IsSlave)
            {
                return settings.slavesCanHear;
            }

            if (pawn.Faction != null)
            {
                return pawn.Faction.HostileTo(Faction.OfPlayer) ? settings.enemyFactionsCanHear : settings.friendlyFactionsCanHear;
            }

            return true;
        }

        public static bool IsOutdoors(Room room)
        {
            return room == null || room.PsychologicallyOutdoors;
        }

        public static bool SharesAudibleSpace(Pawn listener, Room sourceRoom)
        {
            if (listener == null || listener.Map == null)
            {
                return false;
            }

            Room listenerRoom = listener.GetRoom();
            bool listenerOutdoors = IsOutdoors(listenerRoom);
            bool sourceOutdoors = IsOutdoors(sourceRoom);

            if (listenerOutdoors != sourceOutdoors)
            {
                return false;
            }

            return listenerOutdoors || sourceRoom == listenerRoom;
        }

        public static int CountThought(Pawn pawn, ThoughtDef def)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null || def == null)
            {
                return 0;
            }

            return pawn.needs.mood.thoughts.memories.Memories.Count(memory => memory.def == def);
        }

        public static void GainThought(Pawn pawn, ThoughtDef def, int desiredStacks)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null || def == null || desiredStacks <= 0)
            {
                return;
            }

            bool allowStacking = RimAudioMod.Settings?.allowMoodStacking ?? true;
            int stackLimit = allowStacking ? (def.stackLimit > 0 ? def.stackLimit : 1) : 1;
            int target = Mathf.Min(desiredStacks, stackLimit);
            int existing = CountThought(pawn, def);
            int toAdd = target - existing;

            for (int i = 0; i < toAdd; i++)
            {
                if (ThoughtMaker.MakeThought(def) is Thought_Memory memory)
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(memory);
                }
            }

            if (existing > target)
            {
                var memories = pawn.needs.mood.thoughts.memories.Memories.Where(memory => memory.def == def).Take(existing - target).ToList();
                for (int i = 0; i < memories.Count; i++)
                {
                    pawn.needs.mood.thoughts.memories.RemoveMemory(memories[i]);
                }
            }
        }

        public static float ThoughtMagnitude(ThoughtDef def, int sourceCount)
        {
            if (def?.stages == null || def.stages.Count == 0)
            {
                return 0f;
            }

            bool allowStacking = RimAudioMod.Settings?.allowMoodStacking ?? true;
            int stackLimit = allowStacking ? (def.stackLimit > 0 ? def.stackLimit : 1) : 1;
            int effectiveCount = Mathf.Min(sourceCount, stackLimit);
            return Mathf.Abs(def.stages[0].baseMoodEffect) * effectiveCount;
        }

        public static ThoughtDef GetBabyAudioThought(Pawn sourceBaby, Pawn listener)
        {
            if (sourceBaby == null || listener == null || !sourceBaby.DevelopmentalStage.Baby())
            {
                return null;
            }

            string mentalState = sourceBaby.MentalStateDef?.defName;
            bool isParent = listener == sourceBaby.GetMother() || listener == sourceBaby.GetFather();

            if (mentalState == "BabyCry")
            {
                return DefDatabase<ThoughtDef>.GetNamedSilentFail(isParent ? "MyCryingBaby" : "CryingBaby");
            }

            if (mentalState == "BabyGiggle")
            {
                return DefDatabase<ThoughtDef>.GetNamedSilentFail(isParent ? "MyGigglingBaby" : "GigglingBaby");
            }

            return null;
        }

        public static ThoughtDef ResolveThoughtForListener(ThoughtDef def, Pawn listener)
        {
            if (def == null || listener == null)
            {
                return def;
            }

            string suffix = GetLifeStageSuffix(listener);
            if (suffix.NullOrEmpty())
            {
                return def;
            }

            string familyName = StripLifeStageSuffix(def.defName);
            ThoughtDef resolved = DefDatabase<ThoughtDef>.GetNamedSilentFail(familyName + suffix);
            return resolved ?? def;
        }

        private static string GetLifeStageSuffix(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            switch (pawn.DevelopmentalStage)
            {
                case DevelopmentalStage.Newborn:
                case DevelopmentalStage.Baby:
                    return "_Baby";
                case DevelopmentalStage.Child:
                    return "_Child";
                case DevelopmentalStage.Adult:
                    return "_Adult";
                default:
                    return null;
            }
        }

        private static string StripLifeStageSuffix(string defName)
        {
            if (defName.NullOrEmpty())
            {
                return defName;
            }

            if (defName.EndsWith("_Adult", StringComparison.Ordinal))
            {
                return defName.Substring(0, defName.Length - "_Adult".Length);
            }

            if (defName.EndsWith("_Child", StringComparison.Ordinal))
            {
                return defName.Substring(0, defName.Length - "_Child".Length);
            }

            if (defName.EndsWith("_Baby", StringComparison.Ordinal))
            {
                return defName.Substring(0, defName.Length - "_Baby".Length);
            }

            return defName;
        }
    }
}
