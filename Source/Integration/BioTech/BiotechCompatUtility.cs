using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.BioTech
{
	public static class BiotechCompatUtility
	{
		public static bool IsBiotechActive => ModsConfig.BiotechActive;

		public static bool IsBaby(Pawn pawn)
		{
			if (!IsBiotechActive || pawn == null)
			{
				return false;
			}

			DevelopmentalStage stage = pawn.DevelopmentalStage;
			return stage.Baby() || stage.Newborn();
		}

		public static bool IsBabyOrToddler(Pawn pawn)
		{
			if (!IsBiotechActive || pawn == null)
			{
				return false;
			}

			DevelopmentalStage stage = pawn.DevelopmentalStage;
			return stage.Baby() || stage.Newborn() || stage.Child();
		}

		public static bool IsParentOrGrandparentOf(Pawn adult, Pawn baby)
		{
			if (adult == null || baby == null || adult == baby)
			{
				return false;
			}

			Pawn mother = baby.GetMother();
			Pawn father = baby.GetFather();
			if (adult == mother || adult == father)
			{
				return true;
			}

			if (mother != null && (adult == mother.GetMother() || adult == mother.GetFather()))
			{
				return true;
			}

			if (father != null && (adult == father.GetMother() || adult == father.GetFather()))
			{
				return true;
			}

			return false;
		}

		public static IEnumerable<Pawn> GetRoomPawns(Pawn pawn)
		{
			if (pawn?.Map == null)
			{
				yield break;
			}

			Room room = pawn.GetRoom();
			if (room == null)
			{
				yield break;
			}

			List<Thing> things = room.ContainedAndAdjacentThings;
			for (int i = 0; i < things.Count; i++)
			{
				if (things[i] is Pawn other)
				{
					yield return other;
				}
			}
		}
	}
}
