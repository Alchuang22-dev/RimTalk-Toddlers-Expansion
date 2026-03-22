using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAudio
{
    public class Pawn_AudioTracker : ThingComp
    {
        private const float MinAudibleStrength = 0.2f;
        private const int MaxManagedThoughts = 3;

        private int audioTickOffset;
        private ThoughtDef activeThought;
        private List<ThoughtDef> activeThoughts = new List<ThoughtDef>();

        private Pawn Pawn => parent as Pawn;

        public override void CompTick()
        {
            int interval = RimAudioMod.Settings?.audioTickInterval ?? 500;
            if (interval <= 0)
            {
                interval = 500;
            }

            if (audioTickOffset == 0)
            {
                audioTickOffset = Rand.Range(0, interval);
            }

            if ((Find.TickManager.TicksGame + audioTickOffset) % interval != 0)
            {
                return;
            }

            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.needs?.mood == null)
            {
                return;
            }

            if (!RimAudioUtility.PawnAllowedToHear(pawn))
            {
                ClearManagedThoughts(pawn);
                return;
            }

            UpdateAudio(pawn);
        }

        private void UpdateAudio(Pawn pawn)
        {
            float hearingFactor = RimAudioUtility.GetHearingFactor(pawn);
            if (hearingFactor <= 0f)
            {
                ClearManagedThoughts(pawn);
                return;
            }

            var strengths = new Dictionary<ThoughtDef, float>();
            var sourceCounts = new Dictionary<ThoughtDef, int>();

            bool homeOnly = RimAudioMod.Settings?.homeOnly ?? false;
            Area homeArea = homeOnly ? pawn.Map?.areaManager?.Home : null;
            int radius = RimAudioMod.Settings?.audioRadius ?? 10;
            int nearbyWaterCells = 0;
            int nearbyOceanCells = 0;
            int nearbyTreeCount = 0;
            int nearbyCrowdCount = 0;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, true))
            {
                if (!cell.InBounds(pawn.Map))
                {
                    continue;
                }

                if (homeOnly && homeArea != null && !homeArea[cell])
                {
                    continue;
                }

                TerrainDef terrain = cell.GetTerrain(pawn.Map);
                if (terrain != null && terrain.IsWater)
                {
                    nearbyWaterCells++;
                    if (terrain.IsOcean)
                    {
                        nearbyOceanCells++;
                    }
                }

                List<Thing> things = pawn.Map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn otherPawn)
                    {
                        if (otherPawn != pawn)
                        {
                            AddPawnAudioSources(pawn, otherPawn, strengths, sourceCounts, hearingFactor);
                            if (CountsTowardCrowd(pawn, otherPawn))
                            {
                                nearbyCrowdCount++;
                            }
                        }

                        continue;
                    }

                    AddThingAudioSource(pawn, things[i], strengths, sourceCounts, hearingFactor);

                    if (things[i] is Plant plant && CountsTowardTree(pawn, plant))
                    {
                        nearbyTreeCount++;
                    }
                }
            }

            AddGlobalAudioSources(pawn, strengths, sourceCounts, hearingFactor);
            AddTerrainAudioSources(pawn, nearbyWaterCells, nearbyOceanCells, strengths, sourceCounts, hearingFactor);
            AddTreeAudioSource(pawn, nearbyTreeCount, strengths, sourceCounts, hearingFactor);
            AddCrowdAudioSource(pawn, nearbyCrowdCount, strengths, sourceCounts, hearingFactor);

            if (strengths.Count == 0)
            {
                return;
            }

            bool uncapped = RimAudioMod.Settings?.uncappedAudio ?? false;
            if (!uncapped)
            {
                Dictionary<ThoughtDef, int> topThoughts = strengths
                    .OrderByDescending(pair => ThoughtScore(pair.Key, pair.Value, sourceCounts[pair.Key]))
                    .Take(MaxManagedThoughts)
                    .ToDictionary(pair => pair.Key, pair => sourceCounts.TryGetValue(pair.Key, out int count) ? count : 1);
                if (topThoughts.Count == 0)
                {
                    return;
                }

                SyncManagedThoughts(pawn, topThoughts);
            }
            else
            {
                SyncManagedThoughts(pawn, sourceCounts);
            }
        }

        private void AddThingAudioSource(Pawn listener, Thing thing, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, float hearingFactor)
        {
            if (thing == null || thing.Destroyed)
            {
                return;
            }

            ModExtension_Audio extension = thing.def.GetModExtension<ModExtension_Audio>();
            if (extension?.thought == null)
            {
                return;
            }

            CompRefuelable refuelable = thing.TryGetComp<CompRefuelable>();
            if (refuelable != null && !refuelable.HasFuel)
            {
                return;
            }

            CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn)
            {
                return;
            }

            TryAddLocalAudio(listener, thing.Position, thing.GetRoom(), extension, strengths, sourceCounts, hearingFactor);
        }

        private void AddPawnAudioSources(Pawn listener, Pawn source, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, float hearingFactor)
        {
            if (source == null || !source.Spawned || source.Dead)
            {
                return;
            }

            AddPawnHediffAudio(listener, source, strengths, sourceCounts, hearingFactor);
            AddPawnMentalAudio(listener, source, strengths, sourceCounts, hearingFactor);

            if (source.RaceProps.Animal && source.Awake())
            {
                if (IsBird(source))
                {
                    TryAddHeuristicThought(listener, source, RimAudioDefOf.RimAudio_Birdsong, 14f, 1.1f, strengths, sourceCounts, hearingFactor, false);
                }
                else if (source.RaceProps.predator)
                {
                    TryAddHeuristicThought(listener, source, RimAudioDefOf.RimAudio_PredatorNoise, 12f, 1.2f, strengths, sourceCounts, hearingFactor, true);
                }
                else if (IsPetFor(listener, source))
                {
                    TryAddHeuristicThought(listener, source, RimAudioDefOf.RimAudio_PetNoise, 10f, 1.1f, strengths, sourceCounts, hearingFactor, false);
                }
                else if (source.Faction == null)
                {
                    TryAddHeuristicThought(listener, source, RimAudioDefOf.RimAudio_WildlifeNoise, 12f, 1.0f, strengths, sourceCounts, hearingFactor, true);
                }
                else
                {
                    TryAddHeuristicThought(listener, source, RimAudioDefOf.RimAudio_AnimalNoise, 10f, 1.0f, strengths, sourceCounts, hearingFactor, true);
                }
            }

            if (source.RaceProps.IsMechanoid && source.Awake())
            {
                TryAddHeuristicThought(listener, source, RimAudioDefOf.RimAudio_MechNoise, 12f, 1.2f, strengths, sourceCounts, hearingFactor, true);
            }

            if (listener.DevelopmentalStage.Baby() && source.Awake() && (source == listener.GetMother() || source == listener.GetFather()))
            {
                TryAddHeuristicThought(listener, source, RimAudioDefOf.RimAudio_ParentVoice, 12f, 1.25f, strengths, sourceCounts, hearingFactor, false);
            }

            if (source.HostileTo(listener) && source.Awake() && source.health != null)
            {
                bool wounded = source.health.hediffSet.BleedRateTotal > 0f || source.health.summaryHealth.SummaryHealthPercent < 0.85f;
                if (wounded)
                {
                    TryAddHeuristicThought(listener, source, RimAudioDefOf.RimAudio_WoundedEnemy, 12f, 1.15f, strengths, sourceCounts, hearingFactor, true);
                }
            }
        }

        private void AddPawnHediffAudio(Pawn listener, Pawn source, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, float hearingFactor)
        {
            HediffSet hediffs = source.health?.hediffSet;
            if (hediffs == null)
            {
                return;
            }

            for (int i = 0; i < hediffs.hediffs.Count; i++)
            {
                ModExtension_Audio extension = hediffs.hediffs[i].def.GetModExtension<ModExtension_Audio>();
                if (extension?.thought == null)
                {
                    continue;
                }

                TryAddLocalAudio(listener, source.Position, source.GetRoom(), extension, strengths, sourceCounts, hearingFactor);
            }
        }

        private void AddPawnMentalAudio(Pawn listener, Pawn source, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, float hearingFactor)
        {
            ThoughtDef babyThought = RimAudioUtility.GetBabyAudioThought(source, listener);
            if (babyThought != null)
            {
                TryAddHeuristicThought(listener, source, babyThought, 14f, 1.4f, strengths, sourceCounts, hearingFactor, false);
            }
        }

        private void AddGlobalAudioSources(Pawn pawn, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, float hearingFactor)
        {
            if (pawn.Map == null)
            {
                return;
            }

            for (int i = 0; i < pawn.Map.gameConditionManager.ActiveConditions.Count; i++)
            {
                TryAddGlobalAudio(pawn, pawn.Map.gameConditionManager.ActiveConditions[i].def, strengths, sourceCounts, hearingFactor);
            }

            WeatherDef weather = pawn.Map.weatherManager.curWeather;
            if (weather != null)
            {
                TryAddGlobalAudio(pawn, weather, strengths, sourceCounts, hearingFactor);
            }
        }

        private void AddTerrainAudioSources(Pawn listener, int nearbyWaterCells, int nearbyOceanCells, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, float hearingFactor)
        {
            if (listener?.Map == null || !RimAudioUtility.IsOutdoors(listener.GetRoom()))
            {
                return;
            }

            if (nearbyOceanCells >= 4)
            {
                int stackCount = Mathf.Clamp(nearbyOceanCells / 6, 1, 3);
                float strength = 0.9f * hearingFactor * Mathf.Clamp(nearbyOceanCells / 8f, 0.8f, 2.2f);
                if (strength >= MinAudibleStrength)
                {
                    AddAudibleThought(listener, RimAudioDefOf.RimAudio_OceanWaves, strength, strengths, sourceCounts, stackCount);
                }
                return;
            }

            if (nearbyWaterCells >= 6)
            {
                int stackCount = Mathf.Clamp(nearbyWaterCells / 8, 1, 3);
                float strength = 0.75f * hearingFactor * Mathf.Clamp(nearbyWaterCells / 10f, 0.8f, 2f);
                if (strength >= MinAudibleStrength)
                {
                    AddAudibleThought(listener, RimAudioDefOf.RimAudio_RiverWater, strength, strengths, sourceCounts, stackCount);
                }
            }
        }

        private void AddTreeAudioSource(Pawn listener, int nearbyTreeCount, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, float hearingFactor)
        {
            if (listener?.Map == null || !RimAudioUtility.IsOutdoors(listener.GetRoom()) || nearbyTreeCount < 3)
            {
                return;
            }

            int stackCount = Mathf.Clamp(nearbyTreeCount / 5, 1, 3);
            float strength = 0.7f * hearingFactor * Mathf.Clamp(nearbyTreeCount / 6f, 0.8f, 2f);
            if (strength < MinAudibleStrength)
            {
                return;
            }

            AddAudibleThought(listener, RimAudioDefOf.RimAudio_TreeRustle, strength, strengths, sourceCounts, stackCount);
        }

        private void AddCrowdAudioSource(Pawn listener, int nearbyCrowdCount, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, float hearingFactor)
        {
            if (listener?.Map == null || nearbyCrowdCount < 4)
            {
                return;
            }

            int stackCount = Mathf.Clamp(nearbyCrowdCount / 4, 1, 4);
            float strength = 0.8f * hearingFactor * Mathf.Clamp(nearbyCrowdCount / 5f, 0.8f, 2.2f);
            if (strength < MinAudibleStrength)
            {
                return;
            }

            AddAudibleThought(listener, RimAudioDefOf.RimAudio_CrowdNoise, strength, strengths, sourceCounts, stackCount);
        }

        private void TryAddGlobalAudio(Pawn listener, Def def, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, float hearingFactor)
        {
            ModExtension_Audio extension = def?.GetModExtension<ModExtension_Audio>();
            if (extension?.thought == null)
            {
                return;
            }

            bool listenerOutdoors = RimAudioUtility.IsOutdoors(listener.GetRoom());
            if (extension.outdoorsOnly && !listenerOutdoors)
            {
                return;
            }

            if (extension.indoorsOnly && listenerOutdoors)
            {
                return;
            }

            float strength = extension.loudness * hearingFactor;
            if (!listenerOutdoors)
            {
                strength *= 0.65f;
            }

            if (strength < MinAudibleStrength)
            {
                return;
            }

            AddAudibleThought(listener, extension.thought, strength, strengths, sourceCounts);
        }

        private void TryAddHeuristicThought(Pawn listener, Pawn source, ThoughtDef thought, float radius, float loudness, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, float hearingFactor, bool requireLineOfSight)
        {
            if (thought == null || !RimAudioUtility.SharesAudibleSpace(listener, source.GetRoom()))
            {
                return;
            }

            float distance = listener.Position.DistanceTo(source.Position);
            if (distance > radius)
            {
                return;
            }

            if (requireLineOfSight && !GenSight.LineOfSight(listener.Position, source.Position, listener.Map, true))
            {
                return;
            }

            float strength = loudness * hearingFactor * DistanceFactor(distance, radius);
            if (strength < MinAudibleStrength)
            {
                return;
            }

            AddAudibleThought(listener, thought, strength, strengths, sourceCounts);
        }

        private void TryAddLocalAudio(Pawn listener, IntVec3 sourcePosition, Room sourceRoom, ModExtension_Audio extension, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, float hearingFactor)
        {
            if (extension?.thought == null || !RimAudioUtility.SharesAudibleSpace(listener, sourceRoom))
            {
                return;
            }

            bool sourceOutdoors = RimAudioUtility.IsOutdoors(sourceRoom);
            if (extension.indoorsOnly && sourceOutdoors)
            {
                return;
            }

            if (extension.outdoorsOnly && !sourceOutdoors)
            {
                return;
            }

            float radius = extension.radius > 0f ? extension.radius : (RimAudioMod.Settings?.audioRadius ?? 10);
            float distance = listener.Position.DistanceTo(sourcePosition);
            if (distance > radius)
            {
                return;
            }

            if (extension.requireLineOfSight && !GenSight.LineOfSight(listener.Position, sourcePosition, listener.Map, true))
            {
                return;
            }

            float strength = extension.loudness * hearingFactor * DistanceFactor(distance, radius);
            if (strength < MinAudibleStrength)
            {
                return;
            }

            AddAudibleThought(listener, extension.thought, strength, strengths, sourceCounts);
        }

        private static void AddAudibleThought(Pawn listener, ThoughtDef thought, float strength, Dictionary<ThoughtDef, float> strengths, Dictionary<ThoughtDef, int> sourceCounts, int sourceCountIncrement = 1)
        {
            thought = RimAudioUtility.ResolveThoughtForListener(thought, listener);
            if (thought == null)
            {
                return;
            }

            strengths[thought] = strengths.TryGetValue(thought, out float currentStrength) ? currentStrength + strength : strength;
            int countIncrement = Mathf.Max(1, sourceCountIncrement);
            sourceCounts[thought] = sourceCounts.TryGetValue(thought, out int currentCount) ? currentCount + countIncrement : countIncrement;
        }

        private static bool CountsTowardTree(Pawn listener, Plant plant)
        {
            if (listener?.Map == null || plant == null || plant.Destroyed || plant.def?.plant == null || !plant.def.plant.IsTree)
            {
                return false;
            }

            return RimAudioUtility.SharesAudibleSpace(listener, plant.GetRoom());
        }

        private static bool CountsTowardCrowd(Pawn listener, Pawn source)
        {
            if (listener == null || source == null || source.Dead || !source.Awake() || !source.RaceProps.Humanlike)
            {
                return false;
            }

            return RimAudioUtility.SharesAudibleSpace(listener, source.GetRoom());
        }

        private static bool IsBird(Pawn pawn)
        {
            return pawn?.def?.race?.body?.defName == "Bird";
        }

        private static bool IsPetFor(Pawn listener, Pawn source)
        {
            if (listener == null || source == null || source.Faction == null || source.Faction != listener.Faction)
            {
                return false;
            }

            return source.RaceProps.petness > 0f && !source.RaceProps.predator;
        }

        private static float DistanceFactor(float distance, float radius)
        {
            return Mathf.Clamp01(1f - (distance / Mathf.Max(1f, radius)));
        }

        private static float ThoughtScore(ThoughtDef thought, float strength, int sourceCount)
        {
            return strength * RimAudioUtility.ThoughtMagnitude(thought, sourceCount);
        }

        private void SyncManagedThoughts(Pawn pawn, Dictionary<ThoughtDef, int> targetThoughts)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null)
            {
                return;
            }

            for (int i = 0; i < activeThoughts.Count; i++)
            {
                ThoughtDef thought = activeThoughts[i];
                if (!targetThoughts.ContainsKey(thought))
                {
                    pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(thought);
                }
            }

            foreach (var pair in targetThoughts)
            {
                RimAudioUtility.GainThought(pawn, pair.Key, pair.Value);
            }

            activeThoughts = targetThoughts.Keys.ToList();
            activeThought = null;
        }

        private void ClearManagedThoughts(Pawn pawn)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null)
            {
                return;
            }

            if (activeThought != null)
            {
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(activeThought);
                activeThought = null;
            }

            for (int i = 0; i < activeThoughts.Count; i++)
            {
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(activeThoughts[i]);
            }

            activeThoughts.Clear();
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref audioTickOffset, "audioTickOffset", 0);
            Scribe_Collections.Look(ref activeThoughts, "activeThoughts", LookMode.Def);
            Scribe_Defs.Look(ref activeThought, "activeThought");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                activeThoughts ??= new List<ThoughtDef>();
                if (activeThought != null && !activeThoughts.Contains(activeThought))
                {
                    activeThoughts.Add(activeThought);
                }
            }
        }
    }
}
