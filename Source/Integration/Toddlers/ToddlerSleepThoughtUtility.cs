using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ToddlerSleepThoughtUtility
	{
		public static void ApplySleepThoughts(Pawn pawn, Building_Bed bed)
		{
			if (pawn?.needs?.mood?.thoughts?.memories == null || !ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return;
			}

			Room room = bed?.GetRoom() ?? pawn.GetRoom();
			if (room == null || room.PsychologicallyOutdoors)
			{
				return;
			}

			ThoughtDef thought = GetSleepThought(pawn, bed, room);
			if (thought == null)
			{
				return;
			}

			MemoryThoughtHandler memories = pawn.needs.mood.thoughts.memories;
			RemoveExistingThoughts(memories);
			memories.TryGainMemory(thought);
		}

		private static ThoughtDef GetSleepThought(Pawn pawn, Building_Bed bed, Room room)
		{
			if (IsSleepingAlone(pawn, bed, room))
			{
				return ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepAlone;
			}

			if (IsSleepingWithOthers(pawn, room))
			{
				return ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepWithOthers;
			}

			return ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepWithParents;
		}

		private static bool IsSleepingAlone(Pawn pawn, Building_Bed bed, Room room)
		{
			if (bed == null || room.Role != RoomRoleDefOf.Bedroom)
			{
				return false;
			}

			var owners = bed.OwnersForReading;
			if (owners == null || owners.Count != 1 || owners[0] != pawn)
			{
				return false;
			}

			var beds = room.ContainedBeds;
			if (beds == null)
			{
				return false;
			}

			int bedCount = 0;
			foreach (Building_Bed roomBed in beds)
			{
				if (roomBed == null)
				{
					continue;
				}

				bedCount++;
				if (bedCount > 1)
				{
					return false;
				}
			}

			return bedCount == 1;
		}

		private static bool IsSleepingWithOthers(Pawn pawn, Room room)
		{
			if (room.Role != RoomRoleDefOf.Barracks)
			{
				return false;
			}

			bool hasBed = false;
			bool hasOtherOwner = false;
			var beds = room.ContainedBeds;
			if (beds == null)
			{
				return false;
			}

			foreach (Building_Bed roomBed in beds)
			{
				if (roomBed == null)
				{
					continue;
				}

				hasBed = true;
				var owners = roomBed.OwnersForReading;
				if (owners == null || owners.Count == 0)
				{
					continue;
				}

				for (int j = 0; j < owners.Count; j++)
				{
					Pawn owner = owners[j];
					if (owner == null || owner == pawn)
					{
						continue;
					}

					hasOtherOwner = true;
					if (IsRelated(pawn, owner))
					{
						return false;
					}
				}
			}

			if (!hasBed)
			{
				return false;
			}

			return hasOtherOwner;
		}

		private static bool IsRelated(Pawn pawn, Pawn other)
		{
			var relations = pawn?.relations;
			if (relations == null)
			{
				return false;
			}

			var related = relations.RelatedPawns;
			if (related == null)
			{
				return false;
			}

			foreach (Pawn relatedPawn in related)
			{
				if (relatedPawn == other)
				{
					return true;
				}
			}

			return false;
		}

		private static void RemoveExistingThoughts(MemoryThoughtHandler memories)
		{
			if (memories == null)
			{
				return;
			}

			if (ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepAlone != null)
			{
				memories.RemoveMemoriesOfDef(ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepAlone);
			}

			if (ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepWithOthers != null)
			{
				memories.RemoveMemoriesOfDef(ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepWithOthers);
			}

			if (ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepWithParents != null)
			{
				memories.RemoveMemoriesOfDef(ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepWithParents);
			}
		}
	}
}
