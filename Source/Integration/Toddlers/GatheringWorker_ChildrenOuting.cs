using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
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
            
            if (!TryFindGatherSpot(organizer, out IntVec3 spot))
            {
                return false;
            }
            
            // Create the lord job
            LordJob lordJob = CreateLordJob(spot, organizer);
            Lord lord = LordMaker.MakeNewLord(organizer.Faction, lordJob, organizer.Map, new Pawn[] { organizer });
            
            // Send letter
            SendLetter(spot, organizer);
            
            Log.Message($"[RimTalk_ToddlersExpansion] Children's outing started at {spot}, organized by {organizer.LabelShort}");
            
            return true;
        }
        
        protected override LordJob CreateLordJob(IntVec3 spot, Pawn organizer)
        {
            return new LordJob_ChildrenOuting(spot, organizer, def);
        }
        
        protected override bool TryFindGatherSpot(Pawn organizer, out IntVec3 spot)
        {
            spot = IntVec3.Invalid;
            
            if (organizer?.Map == null)
            {
                return false;
            }
            
            Map map = organizer.Map;
            
            // Try to find a nice outdoor spot near the map edge
            // Priority: areas with plants, water, or other natural features
            
            List<IntVec3> candidateSpots = new List<IntVec3>();
            
            // Search near map edges
            foreach (IntVec3 cell in GetMapEdgeCells(map))
            {
                if (IsValidOutingSpot(cell, map, organizer))
                {
                    candidateSpots.Add(cell);
                }
            }
            
            if (candidateSpots.Count == 0)
            {
                // Fallback: try to find any outdoor spot
                return TryFindFallbackSpot(organizer, out spot);
            }
            
            // Score spots based on natural beauty
            candidateSpots = candidateSpots
                .OrderByDescending(c => GetSpotScore(c, map))
                .Take(10)
                .ToList();
            
            if (candidateSpots.TryRandomElement(out spot))
            {
                return true;
            }
            
            return false;
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
            
            // Prefer outdoor spots
            if (cell.Roofed(map))
            {
                return false;
            }
            
            // Check for danger
            if (cell.GetDangerFor(organizer, map) != Danger.None)
            {
                return false;
            }
            
            // Check if terrain is dangerous
            if (cell.GetTerrain(map).IsWater)
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
        private float GetSpotScore(IntVec3 cell, Map map)
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
            
            if (pawn.InMentalState)
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
            
            if (pawn.InMentalState)
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