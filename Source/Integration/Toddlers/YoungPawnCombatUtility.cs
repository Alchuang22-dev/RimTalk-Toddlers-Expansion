using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class YoungPawnCombatUtility
	{
		public static bool IsNonViolentYoungPawn(Pawn pawn)
		{
			if (pawn == null || !ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return false;
			}

			return ShouldPreventColonistAttackingHostileToddler(pawn) || IsViolenceDisabled(pawn);
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

		/// <summary>
		/// Whether a hostile toddler/baby on a player home map should use the colonist think tree.
		/// Controlled by enableHostileToddlerColonistThinkTree setting.
		/// </summary>
		public static bool ShouldApplyHostileToddlerColonistThinkTree(Pawn pawn)
		{
			return ToddlersExpansionSettings.enableHostileToddlerColonistThinkTree
				&& IsHostileYoungPawnOnPlayerMap(pawn);
		}

		/// <summary>
		/// Whether colonists should be prevented from attacking a hostile toddler/baby,
		/// and the toddler should be treated as a non-threat.
		/// Controlled by preventColonistAttackingHostileToddler setting.
		/// </summary>
		public static bool ShouldPreventColonistAttackingHostileToddler(Pawn pawn)
		{
			return ToddlersExpansionSettings.preventColonistAttackingHostileToddler
				&& IsHostileYoungPawnOnPlayerMap(pawn);
		}

		private static bool IsHostileYoungPawnOnPlayerMap(Pawn pawn)
		{
			if (pawn == null || !ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return false;
			}

			if (pawn.IsPrisoner)
			{
				return false;
			}

			if (pawn.Map == null || !pawn.Map.IsPlayerHome)
			{
				return false;
			}

			return pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer);
		}
	}
}
