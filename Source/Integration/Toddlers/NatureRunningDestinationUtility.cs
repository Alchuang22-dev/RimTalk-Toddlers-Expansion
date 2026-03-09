using System;
using System.Collections.Generic;
using System.Linq;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public enum NatureRunningDestinationPool
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

	public sealed class NatureRunningDestinationContext
	{
		public NatureRunningDestinationPool Pool;
		public IntVec3 TargetCell;
		public string ActivityText;
	}

	public static class NatureRunningDestinationUtility
	{
		private const int CandidateSampleStride = 3;
		private const int MaxCandidatesPerPool = 280;
		private const int EdgeDistance = 25;
		private const float SnowDepthThreshold = 0.25f;

		private static readonly Dictionary<int, NatureRunningDestinationContext> ActiveContexts = new Dictionary<int, NatureRunningDestinationContext>();
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
		private static readonly string[] RecreationBuildingKeywords =
		{
			"television",
			"chess",
			"billiards",
			"poker",
			"arcade",
			"horseshoes",
			"hoopstone",
			"recreation",
			"game"
		};

		private struct DestinationCandidate
		{
			public IntVec3 Cell;
			public string ExtraLabel;
		}

		public static bool TryAssignDestinationAndText(Pawn leader, Job job)
		{
			if (leader?.Map == null || job == null)
			{
				return false;
			}

			Map map = leader.Map;
			List<NatureRunningDestinationPool> enabledPools = GetEnabledDestinationPools();
			List<(NatureRunningDestinationPool pool, List<DestinationCandidate> cells)> availablePools =
				new List<(NatureRunningDestinationPool pool, List<DestinationCandidate> cells)>();

			foreach (NatureRunningDestinationPool pool in enabledPools)
			{
				List<DestinationCandidate> candidates = CollectCandidatesForPool(pool, map, leader);
				if (candidates.Count > 0)
				{
					availablePools.Add((pool, candidates));
				}
			}

			if (availablePools.Count == 0)
			{
				ClearContext(leader);
				return false;
			}

			if (!availablePools.TryRandomElement(out (NatureRunningDestinationPool pool, List<DestinationCandidate> cells) selectedPool)
				|| !selectedPool.cells.TryRandomElement(out DestinationCandidate selectedCandidate))
			{
				ClearContext(leader);
				return false;
			}

			job.targetA = selectedCandidate.Cell;

			string activityText = BuildActivityText(selectedPool.pool, selectedCandidate);
			if (!activityText.NullOrEmpty())
			{
				job.reportStringOverride = activityText;
			}

			ActiveContexts[leader.thingIDNumber] = new NatureRunningDestinationContext
			{
				Pool = selectedPool.pool,
				TargetCell = selectedCandidate.Cell,
				ActivityText = activityText
			};

			return true;
		}

		public static void EnsureContextForStartedNatureRunning(Pawn leader, Job natureJob)
		{
			if (leader == null || natureJob == null)
			{
				return;
			}

			if (ActiveContexts.ContainsKey(leader.thingIDNumber))
			{
				return;
			}

			string activityText = natureJob.reportStringOverride;
			if (activityText.NullOrEmpty())
			{
				activityText = T("RimTalk_NatureRunning_Activity_Default");
				natureJob.reportStringOverride = activityText;
			}

			ActiveContexts[leader.thingIDNumber] = new NatureRunningDestinationContext
			{
				Pool = NatureRunningDestinationPool.VanillaEdgeRandom,
				TargetCell = natureJob.targetA.Cell,
				ActivityText = activityText
			};
		}

		public static void ClearContext(Pawn pawn)
		{
			if (pawn == null)
			{
				return;
			}

			ActiveContexts.Remove(pawn.thingIDNumber);
		}

		public static bool TryGetContext(Pawn leader, out NatureRunningDestinationContext context)
		{
			context = null;
			if (leader == null)
			{
				return false;
			}

			return ActiveContexts.TryGetValue(leader.thingIDNumber, out context);
		}

		public static void ApplyFollowJobReport(Job followJob, Pawn leader)
		{
			if (followJob == null || leader == null)
			{
				return;
			}

			if (!TryGetContext(leader, out NatureRunningDestinationContext context) || context.ActivityText.NullOrEmpty())
			{
				return;
			}

			followJob.reportStringOverride = T("RimTalk_FollowNatureRunner_WithActivity", leader.LabelShort, context.ActivityText);
		}

		public static string GetFollowReport(Pawn leader)
		{
			if (!TryGetContext(leader, out NatureRunningDestinationContext context) || context.ActivityText.NullOrEmpty())
			{
				return null;
			}

			return T("RimTalk_FollowNatureRunner_WithActivity", leader.LabelShort, context.ActivityText);
		}

		private static List<NatureRunningDestinationPool> GetEnabledDestinationPools()
		{
			ToddlersExpansionSettings settings = ToddlersExpansionMod.Settings;
			if (settings == null)
			{
				return new List<NatureRunningDestinationPool>
				{
					NatureRunningDestinationPool.VanillaEdgeRandom,
					NatureRunningDestinationPool.GrowingZone,
					NatureRunningDestinationPool.StockpileZone,
					NatureRunningDestinationPool.ResearchRoom,
					NatureRunningDestinationPool.TempleRoom,
					NatureRunningDestinationPool.KitchenRoom,
					NatureRunningDestinationPool.RecreationRoom,
					NatureRunningDestinationPool.HospitalRoom,
					NatureRunningDestinationPool.OtherNonBedroomRooms,
					NatureRunningDestinationPool.ThingWithCompsLandmark,
					NatureRunningDestinationPool.River,
					NatureRunningDestinationPool.Lake,
					NatureRunningDestinationPool.Snow,
					NatureRunningDestinationPool.Cave,
					NatureRunningDestinationPool.Sand,
					NatureRunningDestinationPool.AncientRoad
				};
			}

			List<NatureRunningDestinationPool> pools = new List<NatureRunningDestinationPool>();
			if (settings.EnableOutingPoolVanillaEdgeRandom) pools.Add(NatureRunningDestinationPool.VanillaEdgeRandom);
			if (settings.EnableOutingPoolGrowingZone) pools.Add(NatureRunningDestinationPool.GrowingZone);
			if (settings.EnableOutingPoolStockpileZone) pools.Add(NatureRunningDestinationPool.StockpileZone);
			if (settings.EnableOutingPoolResearchRoom) pools.Add(NatureRunningDestinationPool.ResearchRoom);
			if (settings.EnableOutingPoolTempleRoom) pools.Add(NatureRunningDestinationPool.TempleRoom);
			if (settings.EnableOutingPoolKitchenRoom) pools.Add(NatureRunningDestinationPool.KitchenRoom);
			if (settings.EnableOutingPoolRecreationRoom) pools.Add(NatureRunningDestinationPool.RecreationRoom);
			if (settings.EnableOutingPoolHospitalRoom) pools.Add(NatureRunningDestinationPool.HospitalRoom);
			if (settings.EnableOutingPoolOtherNonBedroomRooms) pools.Add(NatureRunningDestinationPool.OtherNonBedroomRooms);
			if (settings.EnableOutingPoolThingWithCompsLandmark) pools.Add(NatureRunningDestinationPool.ThingWithCompsLandmark);
			if (settings.EnableOutingPoolRiver) pools.Add(NatureRunningDestinationPool.River);
			if (settings.EnableOutingPoolLake) pools.Add(NatureRunningDestinationPool.Lake);
			if (settings.EnableOutingPoolSnow) pools.Add(NatureRunningDestinationPool.Snow);
			if (settings.EnableOutingPoolCave) pools.Add(NatureRunningDestinationPool.Cave);
			if (settings.EnableOutingPoolSand) pools.Add(NatureRunningDestinationPool.Sand);
			if (settings.EnableOutingPoolAncientRoad) pools.Add(NatureRunningDestinationPool.AncientRoad);

			return pools;
		}

		private static List<DestinationCandidate> CollectCandidatesForPool(NatureRunningDestinationPool pool, Map map, Pawn leader)
		{
			switch (pool)
			{
				case NatureRunningDestinationPool.VanillaEdgeRandom:
					return CollectVanillaEdgeCandidates(map, leader);
				case NatureRunningDestinationPool.GrowingZone:
					return CollectZoneCandidates<Zone_Growing>(map, leader, includeCropLabel: true);
				case NatureRunningDestinationPool.StockpileZone:
					return CollectZoneCandidates<Zone_Stockpile>(map, leader, includeCropLabel: false);
				case NatureRunningDestinationPool.ResearchRoom:
					return CollectRoomCandidates(map, leader, room => RoomRoleMatches(room, ResearchRoomRoleNames), includeRecreationBuilding: false);
				case NatureRunningDestinationPool.TempleRoom:
					return CollectRoomCandidates(map, leader, room => RoomRoleMatches(room, TempleRoomRoleNames), includeRecreationBuilding: false);
				case NatureRunningDestinationPool.KitchenRoom:
					return CollectRoomCandidates(map, leader, room => RoomRoleMatches(room, KitchenRoomRoleNames), includeRecreationBuilding: false);
				case NatureRunningDestinationPool.RecreationRoom:
					return CollectRoomCandidates(map, leader, room => RoomRoleMatches(room, RecreationRoomRoleNames), includeRecreationBuilding: true);
				case NatureRunningDestinationPool.HospitalRoom:
					return CollectRoomCandidates(map, leader, room => RoomRoleMatches(room, HospitalRoomRoleNames), includeRecreationBuilding: false);
				case NatureRunningDestinationPool.OtherNonBedroomRooms:
					return CollectRoomCandidates(map, leader, IsOtherNonBedroomRoom, includeRecreationBuilding: false);
				case NatureRunningDestinationPool.ThingWithCompsLandmark:
					return CollectLandmarkCandidates(map, leader);
				case NatureRunningDestinationPool.River:
					return CollectTerrainCandidates(map, leader, (cell, terrain) => IsRiverTerrain(terrain));
				case NatureRunningDestinationPool.Lake:
					return CollectTerrainCandidates(map, leader, (cell, terrain) => IsLakeTerrain(terrain));
				case NatureRunningDestinationPool.Snow:
					return CollectTerrainCandidates(map, leader, (cell, terrain) => map.snowGrid != null && map.snowGrid.GetDepth(cell) >= SnowDepthThreshold);
				case NatureRunningDestinationPool.Cave:
					return CollectTerrainCandidates(map, leader, (cell, terrain) => IsCaveCell(cell, map));
				case NatureRunningDestinationPool.Sand:
					return CollectTerrainCandidates(map, leader, (cell, terrain) => IsSandTerrain(terrain));
				case NatureRunningDestinationPool.AncientRoad:
					return CollectTerrainCandidates(map, leader, (cell, terrain) => IsAncientRoadTerrain(terrain));
				default:
					return new List<DestinationCandidate>();
			}
		}

		private static List<DestinationCandidate> CollectVanillaEdgeCandidates(Map map, Pawn leader)
		{
			List<DestinationCandidate> candidates = new List<DestinationCandidate>();
			if (map == null)
			{
				return candidates;
			}

			foreach (IntVec3 cell in EnumerateMapEdgeCells(map))
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

				if (!IsValidNatureRunningTarget(leader, cell, map))
				{
					continue;
				}

				candidates.Add(new DestinationCandidate { Cell = cell });
				if (candidates.Count >= MaxCandidatesPerPool)
				{
					break;
				}
			}

			return candidates;
		}

		private static IEnumerable<IntVec3> EnumerateMapEdgeCells(Map map)
		{
			int mapWidth = map.Size.x;
			int mapHeight = map.Size.z;
			int minEdgeDist = 5;
			int maxEdgeDist = EdgeDistance;

			for (int x = minEdgeDist; x < mapWidth - minEdgeDist; x += CandidateSampleStride)
			{
				for (int z = minEdgeDist; z < maxEdgeDist; z += CandidateSampleStride)
				{
					yield return new IntVec3(x, 0, z);
				}
			}

			for (int x = minEdgeDist; x < mapWidth - minEdgeDist; x += CandidateSampleStride)
			{
				for (int z = mapHeight - maxEdgeDist; z < mapHeight - minEdgeDist; z += CandidateSampleStride)
				{
					yield return new IntVec3(x, 0, z);
				}
			}

			for (int x = minEdgeDist; x < maxEdgeDist; x += CandidateSampleStride)
			{
				for (int z = minEdgeDist; z < mapHeight - minEdgeDist; z += CandidateSampleStride)
				{
					yield return new IntVec3(x, 0, z);
				}
			}

			for (int x = mapWidth - maxEdgeDist; x < mapWidth - minEdgeDist; x += CandidateSampleStride)
			{
				for (int z = minEdgeDist; z < mapHeight - minEdgeDist; z += CandidateSampleStride)
				{
					yield return new IntVec3(x, 0, z);
				}
			}
		}

		private static List<DestinationCandidate> CollectZoneCandidates<TZone>(Map map, Pawn leader, bool includeCropLabel) where TZone : Zone
		{
			List<DestinationCandidate> candidates = new List<DestinationCandidate>();
			List<Zone> zones = map?.zoneManager?.AllZones;
			if (zones == null)
			{
				return candidates;
			}

			foreach (Zone zone in zones)
			{
				if (zone is not TZone typedZone)
				{
					continue;
				}

				List<IntVec3> zoneCells = typedZone.Cells.ToList();
				if (zoneCells.Count == 0)
				{
					continue;
				}

				HashSet<IntVec3> zoneCellSet = new HashSet<IntVec3>(zoneCells);
				string cropLabel = includeCropLabel ? TryGetCropLabelFromZone(zoneCells, map) : null;
				int addedForThisZone = 0;

				for (int i = 0; i < zoneCells.Count; i += CandidateSampleStride)
				{
					IntVec3 cell = zoneCells[i];
					if (!HasZoneBuffer(cell, zoneCellSet, map))
					{
						continue;
					}

					if (!IsValidNatureRunningTarget(leader, cell, map))
					{
						continue;
					}

					candidates.Add(new DestinationCandidate
					{
						Cell = cell,
						ExtraLabel = cropLabel
					});

					addedForThisZone++;
					if (addedForThisZone >= MaxCandidatesPerPool / 2 || candidates.Count >= MaxCandidatesPerPool)
					{
						break;
					}
				}

				if (candidates.Count >= MaxCandidatesPerPool)
				{
					break;
				}
			}

			return candidates;
		}

		private static List<DestinationCandidate> CollectRoomCandidates(Map map, Pawn leader, Func<Room, bool> roomPredicate, bool includeRecreationBuilding)
		{
			List<DestinationCandidate> candidates = new List<DestinationCandidate>();
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
					if (room == null || room.PsychologicallyOutdoors || !roomPredicate(room))
					{
						continue;
					}

					if (!HasRoomBuffer(cell, room, map))
					{
						continue;
					}

					if (!IsValidNatureRunningTarget(leader, cell, map))
					{
						continue;
					}

					candidates.Add(new DestinationCandidate
					{
						Cell = cell,
						ExtraLabel = includeRecreationBuilding ? TryGetRecreationBuildingLabel(room) : null
					});

					if (candidates.Count >= MaxCandidatesPerPool)
					{
						return candidates;
					}
				}
			}

			return candidates;
		}

		private static List<DestinationCandidate> CollectLandmarkCandidates(Map map, Pawn leader)
		{
			List<DestinationCandidate> candidates = new List<DestinationCandidate>();
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

				IntVec3 targetCell = FindLandmarkGatherCell(thingWithComps, map);
				if (!targetCell.IsValid || !IsValidNatureRunningTarget(leader, targetCell, map))
				{
					continue;
				}

				candidates.Add(new DestinationCandidate
				{
					Cell = targetCell,
					ExtraLabel = thingWithComps.LabelNoCount
				});

				if (candidates.Count >= MaxCandidatesPerPool)
				{
					break;
				}
			}

			return candidates;
		}

		private static List<DestinationCandidate> CollectTerrainCandidates(Map map, Pawn leader, Func<IntVec3, TerrainDef, bool> predicate)
		{
			List<DestinationCandidate> candidates = new List<DestinationCandidate>();
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

					if (!IsValidNatureRunningTarget(leader, cell, map))
					{
						continue;
					}

					candidates.Add(new DestinationCandidate { Cell = cell });
					if (candidates.Count >= MaxCandidatesPerPool)
					{
						return candidates;
					}
				}
			}

			return candidates;
		}

		private static bool HasZoneBuffer(IntVec3 cell, HashSet<IntVec3> zoneCells, Map map)
		{
			int total = 0;
			int inZone = 0;
			foreach (IntVec3 offset in GenRadial.RadialCellsAround(IntVec3.Zero, 2f, true))
			{
				IntVec3 nearby = cell + offset;
				if (!nearby.InBounds(map))
				{
					continue;
				}

				total++;
				if (zoneCells.Contains(nearby))
				{
					inZone++;
				}
			}

			return total > 0 && inZone * 1.0f / total >= 0.65f;
		}

		private static bool HasRoomBuffer(IntVec3 cell, Room room, Map map)
		{
			int total = 0;
			int inRoom = 0;
			foreach (IntVec3 offset in GenRadial.RadialCellsAround(IntVec3.Zero, 2f, true))
			{
				IntVec3 nearby = cell + offset;
				if (!nearby.InBounds(map))
				{
					continue;
				}

				total++;
				if (nearby.GetRoom(map) == room)
				{
					inRoom++;
				}
			}

			return total > 0 && inRoom * 1.0f / total >= 0.65f;
		}

		private static bool IsValidNatureRunningTarget(Pawn leader, IntVec3 cell, Map map)
		{
			if (leader == null || map == null || !cell.InBounds(map) || !cell.Standable(map))
			{
				return false;
			}

			if (cell.IsForbidden(leader))
			{
				return false;
			}

			return leader.CanReach(cell, PathEndMode.OnCell, Danger.Some);
		}

		private static bool RoomRoleMatches(Room room, IEnumerable<string> roleNames)
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

		private static bool IsOtherNonBedroomRoom(Room room)
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

		private static bool IsPotentialLandmarkThing(ThingWithComps thing)
		{
			if (thing?.def == null)
			{
				return false;
			}

			string defName = thing.def.defName ?? string.Empty;
			string label = thing.def.label ?? string.Empty;
			string className = thing.def.thingClass?.Name ?? string.Empty;

			if (ContainsAnyKeyword(defName) || ContainsAnyKeyword(label) || ContainsAnyKeyword(className))
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

		private static bool ContainsAnyKeyword(string value)
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

		private static bool IsRiverTerrain(TerrainDef terrain)
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

		private static bool IsLakeTerrain(TerrainDef terrain)
		{
			return terrain != null && terrain.IsWater && !IsRiverTerrain(terrain);
		}

		private static bool IsSandTerrain(TerrainDef terrain)
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

		private static bool IsAncientRoadTerrain(TerrainDef terrain)
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

		private static bool IsCaveCell(IntVec3 cell, Map map)
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

		private static IntVec3 FindLandmarkGatherCell(Thing thing, Map map)
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

		private static string TryGetCropLabelFromZone(List<IntVec3> zoneCells, Map map)
		{
			if (zoneCells == null || map == null)
			{
				return null;
			}

			List<string> crops = new List<string>();
			for (int i = 0; i < zoneCells.Count; i += CandidateSampleStride)
			{
				Plant plant = zoneCells[i].GetPlant(map);
				if (plant?.def?.label != null)
				{
					crops.Add(plant.def.label);
				}
			}

			return crops.Count > 0 ? crops.RandomElement() : null;
		}

		private static string TryGetRecreationBuildingLabel(Room room)
		{
			if (room?.ContainedAndAdjacentThings == null)
			{
				return null;
			}

			foreach (Thing thing in room.ContainedAndAdjacentThings)
			{
				if (thing?.def?.building == null)
				{
					continue;
				}

				string defName = thing.def.defName ?? string.Empty;
				string label = thing.LabelNoCount ?? thing.def.label ?? string.Empty;
				for (int i = 0; i < RecreationBuildingKeywords.Length; i++)
				{
					if (defName.IndexOf(RecreationBuildingKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0
						|| label.IndexOf(RecreationBuildingKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return thing.LabelNoCount;
					}
				}
			}

			return null;
		}

		private static string BuildActivityText(NatureRunningDestinationPool pool, DestinationCandidate candidate)
		{
			switch (pool)
			{
				case NatureRunningDestinationPool.GrowingZone:
					return PickTranslated(
						T("RimTalk_NatureRunning_Activity_Growing_ObservePlants"),
						T("RimTalk_NatureRunning_Activity_Growing_CatchInsects"),
						T("RimTalk_NatureRunning_Activity_Growing_CollectCrop", candidate.ExtraLabel.NullOrEmpty() ? T("RimTalk_NatureRunning_Label_DefaultCrop") : candidate.ExtraLabel));
				case NatureRunningDestinationPool.StockpileZone:
					return PickTranslated(
						T("RimTalk_NatureRunning_Activity_Stockpile_SearchStockpile"),
						T("RimTalk_NatureRunning_Activity_Stockpile_FindToy"));
				case NatureRunningDestinationPool.ResearchRoom:
					return PickTranslated(
						T("RimTalk_NatureRunning_Activity_Research_Experiment"),
						T("RimTalk_NatureRunning_Activity_Research_FindBooks"),
						T("RimTalk_NatureRunning_Activity_Research_ViewSpecimens"));
				case NatureRunningDestinationPool.TempleRoom:
					return PickTranslated(
						T("RimTalk_NatureRunning_Activity_Temple_ObserveDecor"),
						T("RimTalk_NatureRunning_Activity_Temple_RitualGame"));
				case NatureRunningDestinationPool.KitchenRoom:
					return T("RimTalk_NatureRunning_Activity_Kitchen_HousePlay");
				case NatureRunningDestinationPool.RecreationRoom:
					return PickTranslated(
						T("RimTalk_NatureRunning_Activity_Recreation_PlayOnBuilding", candidate.ExtraLabel.NullOrEmpty() ? T("RimTalk_NatureRunning_Label_DefaultRecreationBuilding") : candidate.ExtraLabel),
						T("RimTalk_NatureRunning_Activity_Recreation_Roughhouse"),
						T("RimTalk_NatureRunning_Activity_Recreation_HideAndSeek"));
				case NatureRunningDestinationPool.HospitalRoom:
					return T("RimTalk_NatureRunning_Activity_Hospital_PlayDoctor");
				case NatureRunningDestinationPool.OtherNonBedroomRooms:
					return T("RimTalk_NatureRunning_Activity_OtherRoom_WanderAround");
				case NatureRunningDestinationPool.ThingWithCompsLandmark:
					return T("RimTalk_NatureRunning_Activity_Landmark_Visit", candidate.ExtraLabel.NullOrEmpty() ? T("RimTalk_NatureRunning_Label_DefaultLandmark") : candidate.ExtraLabel);
				case NatureRunningDestinationPool.River:
					return PickTranslated(
						T("RimTalk_NatureRunning_Activity_River_CatchFish"),
						T("RimTalk_NatureRunning_Activity_River_PlaySand"));
				case NatureRunningDestinationPool.Lake:
					return PickTranslated(
						T("RimTalk_NatureRunning_Activity_Lake_PlayWater"),
						T("RimTalk_NatureRunning_Activity_Lake_CatchTadpoles"));
				case NatureRunningDestinationPool.Snow:
					return PickTranslated(
						T("RimTalk_NatureRunning_Activity_Snow_BuildSnowman"),
						T("RimTalk_NatureRunning_Activity_Snow_SnowballFight"));
				case NatureRunningDestinationPool.Cave:
					return T("RimTalk_NatureRunning_Activity_Cave_Explore");
				case NatureRunningDestinationPool.Sand:
					return T("RimTalk_NatureRunning_Activity_Sand_SurvivalGame");
				case NatureRunningDestinationPool.AncientRoad:
					return T("RimTalk_NatureRunning_Activity_AncientRoad_Run");
				default:
					return T("RimTalk_NatureRunning_Activity_Default");
			}
		}

		private static string PickTranslated(params string[] options)
		{
			if (options == null || options.Length == 0)
			{
				return null;
			}

			return options.RandomElement();
		}

		private static string T(string key)
		{
			return key.Translate().ToString();
		}

		private static string T(string key, params object[] args)
		{
			if (args == null || args.Length == 0)
			{
				return key.Translate().ToString();
			}

#pragma warning disable CS0618
			if (args.Length == 1)
			{
				return key.Translate(args[0]).ToString();
			}

			if (args.Length == 2)
			{
				return key.Translate(args[0], args[1]).ToString();
			}

			if (args.Length == 3)
			{
				return key.Translate(args[0], args[1], args[2]).ToString();
			}

			return key.Translate(args).ToString();
#pragma warning restore CS0618
		}
	}
}
