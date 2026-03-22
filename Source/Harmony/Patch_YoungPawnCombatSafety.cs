using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_YoungPawnCombatSafety
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo threatDisabled = AccessTools.Method(typeof(Pawn), nameof(Pawn.ThreatDisabled), new[] { typeof(IAttackTargetSearcher) });
			if (threatDisabled != null)
			{
				harmony.Patch(threatDisabled, postfix: new HarmonyMethod(typeof(Patch_YoungPawnCombatSafety), nameof(ThreatDisabled_Postfix)));
			}

			MethodInfo tryMeleeAttack = AccessTools.Method(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryMeleeAttack), new[] { typeof(Thing), typeof(Verb), typeof(bool) });
			if (tryMeleeAttack != null)
			{
				harmony.Patch(tryMeleeAttack, prefix: new HarmonyMethod(typeof(Patch_YoungPawnCombatSafety), nameof(TryMeleeAttack_Prefix)));
			}

			MethodInfo tryStartAttack = AccessTools.Method(typeof(Pawn), nameof(Pawn.TryStartAttack), new[] { typeof(LocalTargetInfo) });
			if (tryStartAttack != null)
			{
				harmony.Patch(tryStartAttack, prefix: new HarmonyMethod(typeof(Patch_YoungPawnCombatSafety), nameof(TryStartAttack_Prefix)));
			}
		}

		private static void ThreatDisabled_Postfix(Pawn __instance, ref bool __result)
		{
			if (__result)
			{
				return;
			}

			if (YoungPawnCombatUtility.IsNonViolentYoungPawn(__instance))
			{
				__result = true;
			}
		}

		private static bool TryMeleeAttack_Prefix(Pawn_MeleeVerbs __instance, ref bool __result)
		{
			if (!YoungPawnCombatUtility.IsNonViolentYoungPawn(__instance?.Pawn))
			{
				return true;
			}

			__result = false;
			return false;
		}

		private static bool TryStartAttack_Prefix(Pawn __instance, ref bool __result)
		{
			if (!YoungPawnCombatUtility.IsNonViolentYoungPawn(__instance))
			{
				return true;
			}

			__result = false;
			return false;
		}
	}
}
