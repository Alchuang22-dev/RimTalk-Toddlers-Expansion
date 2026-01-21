using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.BioTech
{
	public static class BedroomThoughtsPatchHelper
	{
		public static bool ShouldReplaceWithMyBabyThought(Pawn pawn, Room roomOrBedRoom)
		{
			if (pawn == null || roomOrBedRoom == null || roomOrBedRoom.PsychologicallyOutdoors)
			{
				return false;
			}

			if (!BiotechCompatUtility.IsBiotechActive || BiotechCompatUtility.IsBaby(pawn))
			{
				return false;
			}

			foreach (Pawn other in BiotechCompatUtility.GetRoomPawns(pawn))
			{
				if (other == pawn)
				{
					continue;
				}

				if (BiotechCompatUtility.IsBabyOrToddler(other) && BiotechCompatUtility.IsParentOrGrandparentOf(pawn, other))
				{
					return true;
				}
			}

			return false;
		}

		public static ThoughtState GetMyBabyNearbyThought(Pawn pawn)
		{
			Room room = pawn?.GetRoom();
			if (!ShouldReplaceWithMyBabyThought(pawn, room))
			{
				return ThoughtState.Inactive;
			}

			return ThoughtState.ActiveAtStage(0);
		}
	}
}
