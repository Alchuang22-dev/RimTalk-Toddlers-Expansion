using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class YoungPawnCombatUtility
	{
		public static bool IsNonViolentYoungPawn(Pawn pawn)
		{
			if (!ToddlersExpansionSettings.enableHostileToddlerColonistBehavior)
			{
				return false;
			}

			if (pawn == null || !ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return false;
			}

			return IsViolenceDisabled(pawn);
		}

		public static bool IsViolenceDisabled(Pawn pawn)
		{
			if (pawn == null)
			{
				return true;
			}

			if (pawn.kindDef?.canMeleeAttack == false)
			{
				return true;
			}

			return pawn.WorkTagIsDisabled(WorkTags.Violent);
		}
	}
}
