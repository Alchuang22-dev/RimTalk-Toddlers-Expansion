using System;
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
    /// Gathering worker for children's outing.
    /// Finds outdoor spots near the map edge for children to gather.
    /// </summary>
    public class GatheringWorker_ChildrenOuting : GatheringWorker
    {
        /// <summary>
        /// Minimum number of children required to start an outing
        /// </summary>
        private const int MinimumChildrenCount = 2;

        /// <summary>
        /// Distance from map edge to search for spots
        /// </summary>
        private const int EdgeDistance = 25;

        private const int MaxCandidatesPerPool = 320;
        private const int CandidateSampleStride = 3;
        private const int TopScoredCandidateCount = 10;
        private const float SnowDepthThreshold = 0.25f;

        private static readonly string[] ResearchRoomRoleNames = { "Laboratory", "ResearchLaboratory", "ResearchRoom" };
        private static readonly string[] TempleRoomRoleNames = { "Temple" };
        private static readonly string[] KitchenRoomRoleNames = { "Kitchen" };
        private static readonly string[] RecreationRoomRoleNames = { "RecRoom", "RecreationRoom" };
        private static readonly string[] HospitalRoomRoleNames = { "Hospital" };
        private static readonly string[] BedroomLikeRoomRoleNames =
        {
            "Bedroom",
            "Barracks",
            "Dormitory",
            "PrisonCell",
            "PrisonBarracks",
            "GuestRoom"
        };

        private static readonly string[] LandmarkKeywords =
        {
            "Ancient",
            "Ruin",
            "Relic",
            "Monument",
            "Shrine",
            "Spewer",
            "Obelisk",
            "Artifact",
            "Ship",
            "Mech",
            "Terminal"
        };

        private enum OutingDestinationPool
        {
            VanillaEdgeRandom,
            GrowingZone,
            StockpileZone,
            ResearchRoom,
            TempleRoom,
            KitchenRoom,
            RecreationRoom,
            HospitalRoom,
            OtherNonBedroomRooms,
            ThingWithCompsLandmark,
            River,
            Lake,
            Snow,
            Cave,
            Sand,
            AncientRoad
        }

        public override bool CanExecute(Map map, Pawn organizer = null)
        {
            if (organizer == null)
            {
                organizer = FindOrganizer(map);
            }
            
            if (organizer == null)
            {
                return false;
            }
            
            if (!TryFindGatherSpot(organizer, out IntVec3 _))
            {
                return false;
            }
            
            // Check if there are enough children
            if (!HasEnoughChildren(map))
            {
                return false;
            }
            
            return true;
        }
        
        public override bool TryExecute(Map map, Pawn organizer = null)
        {
            if (organizer == null)
            {
                organizer = FindOrganizer(map);
            }
            
            if (organizer == null)
            {
                return false;
            }

            if (!TryFindGatherSpotFromEnabledPools(organizer, out IntVec3 spot, out OutingDestinationPool selectedPool))
            {
                return false;
            }
            
            // Gather all eligible participants
            List<Pawn> participants = GatherParticipants(map, organizer);
            if (participants.Count < MinimumChildrenCount)
            {
                return false;
            }
            
            // Create the lord job with all participants
            LordJob lordJob = CreateLordJob(spot, organizer);
            Lord lord = LordMaker.MakeNewLord(organizer.Faction, lordJob, organizer.Map, participants);
            
            // Interrupt current jobs so participants respond immediately
            foreach (Pawn participant in participants)
            {
                if (participant.jobs?.curJob != null)
                {
                    participant.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
            
            // Send letter
            SendLetter(spot, organizer);

            Log.Message($"[RimTalk_ToddlersExpansion] Children's outing started at {spot}, pool={selectedPool}, organized by {organizer.LabelShort}, participants: {participants.Count}");

            return true;
        }
        
        /// <summary>
        /// Gather all eligible participants for the outing
        /// </summary>
        private List<Pawn> GatherParticipants(Map map, Pawn organizer)
        {
            List<Pawn> participants = new List<Pawn>();
            
            // Always add the organizer first
            participants.Add(organizer);
            
            // Find other children/toddlers to join
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn == organizer)
                {
                    continue;
                }
                
                if (!IsChildOrToddler(pawn))
                {
                    continue;
                }
                
                if (!CanParticipate(pawn))
                {
                    continue;
                }
                
                // Check if can reach the organizer (as a proxy for can reach the gathering)
                if (!pawn.CanReach(organizer, Verse.AI.PathEndMode.Touch, Danger.Some))
                {
                    continue;
                }
                
                participants.Add(pawn);
            }
            
            return participants;
        }
        
        protected override LordJob CreateLordJob(IntVec3 spot, Pawn organizer)
        {
            return new LordJob_ChildrenOuting(spot, organizer, def);
        }
        
        protected override bool TryFindGatherSpot(Pawn organizer, out IntVec3 spot)
        {
            return TryFindGatherSpotFromEnabledPools(organizer, out spot, out _);
        }

        private bool TryFindGatherSpotFromEnabledPools(Pawn organizer, out IntVec3 spot, out OutingDestinationPool selectedPool)
        {
            spot = IntVec3.Invalid;
            selectedPool = OutingDestinationPool.VanillaEdgeRandom;

            if (organizer?.Map == null)
            {
                return false;
            }

            Map map = organizer.Map;

            List<OutingDestinationPool> enabledPools = GetEnabledDestinationPools();
            if (enabledPools.Count == 0)
            {
                return TryFindFallbackSpot(organizer, out spot);
            }

            List<(OutingDestinationPool pool, List<IntVec3> cells)> availablePools =
                new List<(OutingDestinationPool pool, List<IntVec3> cells)>();

            foreach (OutingDestinationPool pool in enabledPools)
            {
                List<IntVec3> rawCandidates = CollectCandidateSpotsForPool(pool, map);
                if (rawCandidates.Count == 0)
                {
                    continue;
                }

                List<IntVec3> validCandidates = rawCandidates
                    .Where(c => IsValidOutingSpot(c, map, organizer))
                    .Distinct()
                    .ToList();

                if (validCandidates.Count == 0)
                {
                    continue;
                }

                availablePools.Add((pool, validCandidates));
            }

            if (availablePools.Count == 0)
            {
                return TryFindFallbackSpot(organizer, out spot);
            }

            if (!availablePools.TryRandomElement(out (OutingDestinationPool pool, List<IntVec3> cells) selected))
            {
                return TryFindFallbackSpot(organizer, out spot);
            }

            selectedPool = selected.pool;
            List<IntVec3> finalCandidates = selected.cells
                .OrderByDescending(c => GetSpotScore(c, map, selected.pool))
                .Take(TopScoredCandidateCount)
                .ToList();

            if (finalCandidates.TryRandomElement(out spot))
            {
                return true;
            }

            return TryFindFallbackSpot(organizer, out spot);
        }

        private List<OutingDestinationPool> GetEnabledDestinationPools()
        {
            ToddlersExpansionSettings settings = ToddlersExpansionMod.Settings;
            if (settings == null)
            {
                return new List<OutingDestinationPool>
                {
                    OutingDestinationPool.VanillaEdgeRandom,
                    OutingDestinationPool.GrowingZone,
                    OutingDestinationPool.StockpileZone,
                    OutingDestinationPool.ResearchRoom,
                    OutingDestinationPool.TempleRoom,
                    OutingDestinationPool.KitchenRoom,
                    OutingDestinationPool.RecreationRoom,
                    OutingDestinationPool.HospitalRoom,
                    OutingDestinationPool.OtherNonBedroomRooms,
                    OutingDestinationPool.ThingWithCompsLandmark,
                    OutingDestinationPool.River,
                    OutingDestinationPool.Lake,
                    OutingDestinationPool.Snow,
                    OutingDestinationPool.Cave,
                    OutingDestinationPool.Sand,
                    OutingDestinationPool.AncientRoad
                };
            }

            List<OutingDestinationPool> pools = new List<OutingDestinationPool>();
            if (settings.EnableOutingPoolVanillaEdgeRandom) pools.Add(OutingDestinationPool.VanillaEdgeRandom);
            if (settings.EnableOutingPoolGrowingZone) pools.Add(OutingDestinationPool.GrowingZone);
            if (settings.EnableOutingPoolStockpileZone) pools.Add(OutingDestinationPool.StockpileZone);
            if (settings.EnableOutingPoolResearchRoom) pools.Add(OutingDestinationPool.ResearchRoom);
            if (settings.EnableOutingPoolTempleRoom) pools.Add(OutingDestinationPool.TempleRoom);
            if (settings.EnableOutingPoolKitchenRoom) pools.Add(OutingDestinationPool.KitchenRoom);
            if (settings.EnableOutingPoolRecreationRoom) pools.Add(OutingDestinationPool.RecreationRoom);
            if (settings.EnableOutingPoolHospitalRoom) pools.Add(OutingDestinationPool.HospitalRoom);
            if (settings.EnableOutingPoolOtherNonBedroomRooms) pools.Add(OutingDestinationPool.OtherNonBedroomRooms);
            if (settings.EnableOutingPoolThingWithCompsLandmark) pools.Add(OutingDestinationPool.ThingWithCompsLandmark);
            if (settings.EnableOutingPoolRiver) pools.Add(OutingDestinationPool.River);
            if (settings.EnableOutingPoolLake) pools.Add(OutingDestinationPool.Lake);
            if (settings.EnableOutingPoolSnow) pools.Add(OutingDestinationPool.Snow);
            if (settings.EnableOutingPoolCave) pools.Add(OutingDestinationPool.Cave);
            if (settings.EnableOutingPoolSand) pools.Add(OutingDestinationPool.Sand);
            if (settings.EnableOutingPoolAncientRoad) pools.Add(OutingDestinationPool.AncientRoad);

            return pools;
        }

        private List<IntVec3> CollectCandidateSpotsForPool(OutingDestinationPool pool, Map map)
        {
            switch (pool)
            {
                case OutingDestinationPool.VanillaEdgeRandom:
                    return CollectVanillaEdgeSpots(map);
                case OutingDestinationPool.GrowingZone:
                    return CollectZoneSpots<Zone_Growing>(map);
                case OutingDestinationPool.StockpileZone:
                    return CollectZoneSpots<Zone_Stockpile>(map);
                case OutingDestinationPool.ResearchRoom:
                    return CollectRoomSpots(map, room => RoomRoleMatches(room, ResearchRoomRoleNames));
                case OutingDestinationPool.TempleRoom:
                    return CollectRoomSpots(map, room => RoomRoleMatches(room, TempleRoomRoleNames));
                case OutingDestinationPool.KitchenRoom:
                    return CollectRoomSpots(map, room => RoomRoleMatches(room, KitchenRoomRoleNames));
                case OutingDestinationPool.RecreationRoom:
                    return CollectRoomSpots(map, room => RoomRoleMatches(room, RecreationRoomRoleNames));
                case OutingDestinationPool.HospitalRoom:
                    return CollectRoomSpots(map, room => RoomRoleMatches(room, HospitalRoomRoleNames));
                case OutingDestinationPool.OtherNonBedroomRooms:
                    return CollectRoomSpots(map, IsOtherNonBedroomRoom);
                case OutingDestinationPool.ThingWithCompsLandmark:
                    return CollectThingWithCompsLandmarkSpots(map);
                case OutingDestinationPool.River:
                    return CollectTerrainSpots(map, (cell, terrain) => IsRiverTerrain(terrain));
                case OutingDestinationPool.Lake:
                    return CollectTerrainSpots(map, (cell, terrain) => IsLakeTerrain(terrain));
                case OutingDestinationPool.Snow:
                    return CollectTerrainSpots(map, (cell, terrain) => map.snowGrid != null && map.snowGrid.GetDepth(cell) >= SnowDepthThreshold);
                case OutingDestinationPool.Cave:
                    return CollectTerrainSpots(map, (cell, terrain) => IsCaveCell(cell, map));
                case OutingDestinationPool.Sand:
                    return CollectTerrainSpots(map, (cell, terrain) => IsSandTerrain(terrain));
                case OutingDestinationPool.AncientRoad:
                    return CollectTerrainSpots(map, (cell, terrain) => IsAncientRoadTerrain(terrain));
                default:
                    return new List<IntVec3>();
            }
        }

        private List<IntVec3> CollectVanillaEdgeSpots(Map map)
        {
            List<IntVec3> candidates = new List<IntVec3>();
            foreach (IntVec3 cell in GetMapEdgeCells(map))
            {
                if (cell.Roofed(map))
                {
                    continue;
                }

                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain != null && terrain.IsWater)
                {
                    continue;
                }

                candidates.Add(cell);
                if (candidates.Count >= MaxCandidatesPerPool)
                {
                    break;
                }
            }

            return candidates;
        }

        private List<IntVec3> CollectZoneSpots<TZone>(Map map) where TZone : Zone
        {
            List<IntVec3> candidates = new List<IntVec3>();
            List<Zone> zones = map?.zoneManager?.AllZones;
            if (zones == null)
            {
                return candidates;
            }

            foreach (Zone zone in zones)
            {
                if (zone is not TZone)
                {
                    continue;
                }

                AddSampledCells(zone.Cells, candidates);
                if (candidates.Count >= MaxCandidatesPerPool)
                {
                    return candidates;
                }
            }

            return candidates;
        }

        private List<IntVec3> CollectRoomSpots(Map map, Func<Room, bool> roomPredicate)
        {
            List<IntVec3> candidates = new List<IntVec3>();
            if (map == null || roomPredicate == null)
            {
                return candidates;
            }

            for (int x = 1; x < map.Size.x - 1; x += CandidateSampleStride)
            {
                for (int z = 1; z < map.Size.z - 1; z += CandidateSampleStride)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    Room room = cell.GetRoom(map);
                    if (room == null || room.PsychologicallyOutdoors)
                    {
                        continue;
                    }

                    if (!roomPredicate(room))
                    {
                        continue;
                    }

                    candidates.Add(cell);
                    if (candidates.Count >= MaxCandidatesPerPool)
                    {
                        return candidates;
                    }
                }
            }

            return candidates;
        }

        private List<IntVec3> CollectThingWithCompsLandmarkSpots(Map map)
        {
            List<IntVec3> candidates = new List<IntVec3>();
            List<Thing> things = map?.listerThings?.AllThings;
            if (things == null)
            {
                return candidates;
            }

            foreach (Thing thing in things)
            {
                if (thing is not ThingWithComps thingWithComps || !thing.Spawned)
                {
                    continue;
                }

                if (!IsPotentialLandmarkThing(thingWithComps))
                {
                    continue;
                }

                IntVec3 cell = FindLandmarkGatherCell(thingWithComps, map);
                if (!cell.IsValid)
                {
                    continue;
                }

                candidates.Add(cell);
                if (candidates.Count >= MaxCandidatesPerPool)
                {
                    break;
                }
            }

            return candidates;
        }

        private List<IntVec3> CollectTerrainSpots(Map map, Func<IntVec3, TerrainDef, bool> predicate)
        {
            List<IntVec3> candidates = new List<IntVec3>();
            if (map == null || predicate == null)
            {
                return candidates;
            }

            for (int x = 1; x < map.Size.x - 1; x += CandidateSampleStride)
            {
                for (int z = 1; z < map.Size.z - 1; z += CandidateSampleStride)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    TerrainDef terrain = cell.GetTerrain(map);
                    if (!predicate(cell, terrain))
                    {
                        continue;
                    }

                    candidates.Add(cell);
                    if (candidates.Count >= MaxCandidatesPerPool)
                    {
                        return candidates;
                    }
                }
            }

            return candidates;
        }

        private void AddSampledCells(IEnumerable<IntVec3> source, List<IntVec3> destination)
        {
            if (source == null || destination == null || destination.Count >= MaxCandidatesPerPool)
            {
                return;
            }

            int index = 0;
            foreach (IntVec3 cell in source)
            {
                if (index % CandidateSampleStride == 0)
                {
                    destination.Add(cell);
                    if (destination.Count >= MaxCandidatesPerPool)
                    {
                        return;
                    }
                }
                index++;
            }
        }

        /// <summary>
        /// Get cells near the map edges
        /// </summary>
        private IEnumerable<IntVec3> GetMapEdgeCells(Map map)
        {
            int mapWidth = map.Size.x;
            int mapHeight = map.Size.z;
            
            // Sample cells near edges (not too close to actual edge for pathfinding)
            int minEdgeDist = 5;
            int maxEdgeDist = EdgeDistance;
            
            // Top edge area
            for (int x = minEdgeDist; x < mapWidth - minEdgeDist; x += 3)
            {
                for (int z = minEdgeDist; z < maxEdgeDist; z += 3)
                {
                    yield return new IntVec3(x, 0, z);
                }
            }
            
            // Bottom edge area
            for (int x = minEdgeDist; x < mapWidth - minEdgeDist; x += 3)
            {
                for (int z = mapHeight - maxEdgeDist; z < mapHeight - minEdgeDist; z += 3)
                {
                    yield return new IntVec3(x, 0, z);
                }
            }
            
            // Left edge area
            for (int x = minEdgeDist; x < maxEdgeDist; x += 3)
            {
                for (int z = minEdgeDist; z < mapHeight - minEdgeDist; z += 3)
                {
                    yield return new IntVec3(x, 0, z);
                }
            }
            
            // Right edge area
            for (int x = mapWidth - maxEdgeDist; x < mapWidth - minEdgeDist; x += 3)
            {
                for (int z = minEdgeDist; z < mapHeight - minEdgeDist; z += 3)
                {
                    yield return new IntVec3(x, 0, z);
                }
            }
        }

        private bool RoomRoleMatches(Room room, IEnumerable<string> roleNames)
        {
            string roleDefName = room?.Role?.defName;
            if (roleDefName.NullOrEmpty() || roleNames == null)
            {
                return false;
            }

            foreach (string name in roleNames)
            {
                if (roleDefName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOtherNonBedroomRoom(Room room)
        {
            if (room == null || room.PsychologicallyOutdoors)
            {
                return false;
            }

            string roleDefName = room.Role?.defName;
            if (roleDefName.NullOrEmpty())
            {
                return true;
            }

            if (BedroomLikeRoomRoleNames.Any(name => roleDefName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (RoomRoleMatches(room, ResearchRoomRoleNames)
                || RoomRoleMatches(room, TempleRoomRoleNames)
                || RoomRoleMatches(room, KitchenRoomRoleNames)
                || RoomRoleMatches(room, RecreationRoomRoleNames)
                || RoomRoleMatches(room, HospitalRoomRoleNames))
            {
                return false;
            }

            return true;
        }

        private bool IsPotentialLandmarkThing(ThingWithComps thing)
        {
            if (thing?.def == null)
            {
                return false;
            }

            string defName = thing.def.defName ?? string.Empty;
            string label = thing.def.label ?? string.Empty;
            string className = thing.def.thingClass?.Name ?? string.Empty;

            if (ContainsAnyKeyword(defName)
                || ContainsAnyKeyword(label)
                || ContainsAnyKeyword(className))
            {
                return true;
            }

            if (thing.AllComps != null)
            {
                foreach (ThingComp comp in thing.AllComps)
                {
                    string compName = comp?.GetType()?.Name ?? string.Empty;
                    if (compName.IndexOf("GameCondition", StringComparison.OrdinalIgnoreCase) >= 0
                        || compName.IndexOf("Spewer", StringComparison.OrdinalIgnoreCase) >= 0
                        || compName.IndexOf("Spawner", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ContainsAnyKeyword(string value)
        {
            if (value.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < LandmarkKeywords.Length; i++)
            {
                if (value.IndexOf(LandmarkKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsRiverTerrain(TerrainDef terrain)
        {
            if (terrain == null || !terrain.IsWater)
            {
                return false;
            }

            string name = terrain.defName ?? string.Empty;
            return name.IndexOf("River", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Moving", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsLakeTerrain(TerrainDef terrain)
        {
            return terrain != null && terrain.IsWater && !IsRiverTerrain(terrain);
        }

        private bool IsSandTerrain(TerrainDef terrain)
        {
            if (terrain == null)
            {
                return false;
            }

            string defName = terrain.defName ?? string.Empty;
            string label = terrain.label ?? string.Empty;
            return defName.IndexOf("Sand", StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("sand", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsAncientRoadTerrain(TerrainDef terrain)
        {
            if (terrain == null)
            {
                return false;
            }

            string defName = terrain.defName ?? string.Empty;
            if (defName.IndexOf("Ancient", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return defName.IndexOf("Road", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Asphalt", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Pave", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Tile", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsCaveCell(IntVec3 cell, Map map)
        {
            RoofDef roof = cell.GetRoof(map);
            if (roof == null)
            {
                return false;
            }

            string roofName = roof.defName ?? string.Empty;
            return roofName.IndexOf("RoofRock", StringComparison.OrdinalIgnoreCase) >= 0
                || roofName.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IntVec3 FindLandmarkGatherCell(Thing thing, Map map)
        {
            if (thing == null || map == null)
            {
                return IntVec3.Invalid;
            }

            IntVec3 interactionCell = thing.InteractionCell;
            if (interactionCell.IsValid && interactionCell.InBounds(map) && interactionCell.Standable(map))
            {
                return interactionCell;
            }

            IntVec3 position = thing.Position;
            if (position.IsValid && position.InBounds(map) && position.Standable(map))
            {
                return position;
            }

            if (CellFinder.TryFindRandomCellNear(
                thing.Position,
                map,
                4,
                c => c.InBounds(map) && c.Standable(map),
                out IntVec3 nearby))
            {
                return nearby;
            }

            return IntVec3.Invalid;
        }

        /// <summary>
        /// Check if a cell is a valid outing spot
        /// </summary>
        private bool IsValidOutingSpot(IntVec3 cell, Map map, Pawn organizer)
        {
            if (!cell.InBounds(map))
            {
                return false;
            }
            
            if (!cell.Standable(map))
            {
                return false;
            }

            // Check for danger
            if (cell.GetDangerFor(organizer, map) != Danger.None)
            {
                return false;
            }

            // Check if forbidden
            if (cell.IsForbidden(organizer))
            {
                return false;
            }

            // Check if reachable
            if (!organizer.CanReach(cell, Verse.AI.PathEndMode.OnCell, Danger.Some))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Score a spot based on natural beauty and features
        /// </summary>
        private float GetSpotScore(IntVec3 cell, Map map, OutingDestinationPool pool)
        {
            float score = 0f;

            // Base beauty score
            score += BeautyUtility.CellBeauty(cell, map);

            // Bonus for nearby plants
            int plantCount = 0;
            foreach (IntVec3 nearbyCell in GenRadial.RadialCellsAround(cell, 5, true))
            {
                if (nearbyCell.InBounds(map))
                {
                    Plant plant = nearbyCell.GetPlant(map);
                    if (plant != null && !plant.IsBurning())
                    {
                        plantCount++;
                    }
                }
            }
            score += plantCount * 0.5f;

            // Bonus for nearby water (but not on water)
            foreach (IntVec3 nearbyCell in GenRadial.RadialCellsAround(cell, 3, true))
            {
                if (nearbyCell.InBounds(map) && nearbyCell.GetTerrain(map).IsWater)
                {
                    score += 2f;
                    break;
                }
            }

            // Light preference tweaks for pool identity.
            TerrainDef terrain = cell.GetTerrain(map);
            if (pool == OutingDestinationPool.River && IsRiverTerrain(terrain))
            {
                score += 2.5f;
            }
            else if (pool == OutingDestinationPool.Lake && IsLakeTerrain(terrain))
            {
                score += 2f;
            }
            else if (pool == OutingDestinationPool.Snow && map.snowGrid != null)
            {
                score += map.snowGrid.GetDepth(cell);
            }
            else if (pool == OutingDestinationPool.Cave && IsCaveCell(cell, map))
            {
                score += 2f;
            }
            else if (pool == OutingDestinationPool.AncientRoad && IsAncientRoadTerrain(terrain))
            {
                score += 2f;
            }

            return score;
        }

        /// <summary>
        /// Fallback: find any outdoor spot
        /// </summary>
        private bool TryFindFallbackSpot(Pawn organizer, out IntVec3 spot)
        {
            return RCellFinder.TryFindRandomSpotJustOutsideColony(organizer, out spot);
        }
        
        protected override Pawn FindOrganizer(Map map)
        {
            // Find the oldest child to be the organizer
            List<Pawn> childCandidates = new List<Pawn>();
            
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (!pawn.DevelopmentalStage.Child())
                {
                    continue;
                }
                
                if (!CanBeOrganizer(pawn))
                {
                    continue;
                }
                
                childCandidates.Add(pawn);
            }
            
            if (childCandidates.Count == 0)
            {
                return null;
            }
            
            // Return the oldest child
            return childCandidates.OrderByDescending(p => p.ageTracker.AgeBiologicalYears).First();
        }
        
        /// <summary>
        /// Check if a pawn can be the organizer
        /// </summary>
        private bool CanBeOrganizer(Pawn pawn)
        {
            if (pawn.Downed || pawn.Dead || !pawn.Spawned)
            {
                return false;
            }
            
            if (ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
            {
                return false;
            }
            
            if (pawn.Drafted)
            {
                return false;
            }
            
            // Check needs
            if (pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < 0.2f)
            {
                return false;
            }
            
            if (pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage < 0.2f)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if there are enough children for an outing
        /// </summary>
        private bool HasEnoughChildren(Map map)
        {
            int count = 0;
            
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (IsChildOrToddler(pawn) && CanParticipate(pawn))
                {
                    count++;
                    if (count >= MinimumChildrenCount)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if a pawn is a child or toddler
        /// </summary>
        private bool IsChildOrToddler(Pawn pawn)
        {
            if (pawn.DevelopmentalStage.Child())
            {
                return true;
            }
            
            if (ToddlersCompatUtility.IsToddler(pawn))
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if a pawn can participate in the outing
        /// </summary>
        private bool CanParticipate(Pawn pawn)
        {
            if (pawn.Downed || pawn.Dead || !pawn.Spawned)
            {
                return false;
            }
            
            if (ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
            {
                return false;
            }
            
            return true;
        }
        
        protected override void SendLetter(IntVec3 spot, Pawn organizer)
        {
            string title = def?.letterTitle ?? "Children's Outing!";
            string text = def?.letterText ?? "{ORGANIZER_labelShort} is organizing an outing! The children will explore and play together.";
            text = text.Replace("{ORGANIZER_labelShort}", organizer.LabelShort);
            
            Find.LetterStack.ReceiveLetter(
                title, 
                text, 
                LetterDefOf.PositiveEvent, 
                new TargetInfo(spot, organizer.Map));
        }
    }
}
