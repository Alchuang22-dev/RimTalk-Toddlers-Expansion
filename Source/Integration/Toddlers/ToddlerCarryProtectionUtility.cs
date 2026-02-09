using RimTalk_ToddlersExpansion.Core;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ToddlerCarryProtectionUtility
	{
		private static HediffDef _carryProtectionDef;
		private static bool _checked;

		public static void SetCarryProtectionActive(Pawn pawn, bool active)
		{
			if (!IsValidTargetPawn(pawn))
			{
				return;
			}

			EnsureDefLoaded();
			if (_carryProtectionDef == null)
			{
				return;
			}

			Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(_carryProtectionDef);
			if (active)
			{
				if (existing == null)
				{
					pawn.health.AddHediff(_carryProtectionDef);
				}

				return;
			}

			if (existing != null)
			{
				pawn.health.RemoveHediff(existing);
			}
		}

		public static bool HasCarryProtection(Pawn pawn)
		{
			if (!IsValidTargetPawn(pawn))
			{
				return false;
			}

			EnsureDefLoaded();
			return _carryProtectionDef != null && pawn.health.hediffSet.HasHediff(_carryProtectionDef);
		}

		private static bool IsValidTargetPawn(Pawn pawn)
		{
			// Apply protection to all carry targets in this mod (toddlers + biotech newborn/baby).
			return pawn != null
				&& ToddlersCompatUtility.IsToddlerOrBaby(pawn)
				&& pawn.health?.hediffSet != null;
		}

		private static void EnsureDefLoaded()
		{
			if (_checked)
			{
				return;
			}

			_checked = true;
			_carryProtectionDef = ToddlersExpansionHediffDefOf.RimTalk_CarriedProtection
				?? DefDatabase<HediffDef>.GetNamedSilentFail("RimTalk_CarriedProtection");
		}
	}
}
