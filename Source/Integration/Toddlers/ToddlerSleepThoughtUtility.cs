using System;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.BioTech;
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

			if (bed == null && pawn != null)
			{
				bed = pawn.CurrentBed();
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

			try
			{
				memories.TryGainMemory(thought);
			}
			catch (Exception ex)
			{
				Log.Error($"[RimTalk_ToddlersExpansion] Failed to add thought '{thought.defName}' to pawn: {pawn.Name}. Error: {ex.Message}");
			}
		}

		private static ThoughtDef GetSleepThought(Pawn pawn, Building_Bed bed, Room room)
		{
			if (room == null)
			{
				return null;
			}

			SleepRoomInfo info = GetRoomInfo(pawn, room);
			if (info.BedCount == 1 && !info.HasOtherOwner)
			{
				LogSleepCheck("Alone", pawn, room, info);
				return ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepAlone;
			}

			if (info.HasParentOrGrandparent)
			{
				LogSleepCheck("WithParents", pawn, room, info);
				return ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepWithParents;
			}

			if (info.HasNonBabyToddler)
			{
				LogSleepCheck("WithOthers", pawn, room, info);
				return ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepWithOthers;
			}

			if (info.BedCount > 1 || info.HasOtherOwner)
			{
				LogSleepCheck("Nursery", pawn, room, info);
				return ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepInNursery;
			}

			LogSleepCheck("NoMatch", pawn, room, info);
			return null;
		}

		private static SleepRoomInfo GetRoomInfo(Pawn pawn, Room room)
		{
			SleepRoomInfo info = new SleepRoomInfo();
			if (room == null)
			{
				return info;
			}

			var beds = room.ContainedBeds;
			if (beds == null)
			{
				return info;
			}

			int bedCount = 0;
			foreach (Building_Bed roomBed in beds)
			{
				if (roomBed == null)
				{
					continue;
				}

				bedCount++;
				var owners = roomBed.OwnersForReading;
				if (owners == null || owners.Count == 0)
				{
					continue;
				}

				info.OwnerCount += owners.Count;
				for (int i = 0; i < owners.Count; i++)
				{
					Pawn owner = owners[i];
					if (owner == null)
					{
						continue;
					}

					if (owner != pawn)
					{
						info.HasOtherOwner = true;
					}

					if (BiotechCompatUtility.IsParentOrGrandparentOf(owner, pawn))
					{
						info.HasParentOrGrandparent = true;
					}

					if (!ToddlersCompatUtility.IsToddlerOrBaby(owner))
					{
						info.HasNonBabyToddler = true;
					}
				}
			}

			info.BedCount = bedCount;
			return info;
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

			if (ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepInNursery != null)
			{
				memories.RemoveMemoriesOfDef(ToddlersExpansionThoughtDefOf.RimTalk_ToddlerSleepInNursery);
			}
		}

		private static void LogSleepCheck(string branch, Pawn pawn, Room room, SleepRoomInfo info)
		{
			if (!Prefs.DevMode)
			{
				return;
			}

			string pawnLabel = pawn?.Name?.ToStringShort ?? "null";
			string roomRole = room?.Role?.defName ?? "null";
			Log.Message($"[RimTalk_ToddlersExpansion] SleepCheck {branch} pawn={pawnLabel}, roomRole={roomRole}, bedCount={info.BedCount}, ownerCount={info.OwnerCount}, otherOwner={info.HasOtherOwner}, parentOrGrandparent={info.HasParentOrGrandparent}, nonBabyToddler={info.HasNonBabyToddler}");
		}

		private struct SleepRoomInfo
		{
			public int BedCount;
			public int OwnerCount;
			public bool HasOtherOwner;
			public bool HasParentOrGrandparent;
			public bool HasNonBabyToddler;
		}
	}
}
