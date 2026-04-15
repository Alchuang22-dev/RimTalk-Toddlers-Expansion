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

			MethodInfo hostileToThings = AccessTools.Method(typeof(GenHostility), "HostileTo", new[] { typeof(Thing), typeof(Thing) });
			if (hostileToThings != null)
			{
				harmony.Patch(hostileToThings, postfix: new HarmonyMethod(typeof(Patch_YoungPawnCombatSafety), nameof(HostileTo_ThingThing_Postfix)));
			}

			MethodInfo hostileToFaction = AccessTools.Method(typeof(GenHostility), "HostileTo", new[] { typeof(Thing), typeof(Faction) });
			if (hostileToFaction != null)
			{
				harmony.Patch(hostileToFaction, postfix: new HarmonyMethod(typeof(Patch_YoungPawnCombatSafety), nameof(HostileTo_ThingFaction_Postfix)));
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

			__instance.Pawn.jobs?.EndCurrentJob(JobCondition.Incompletable);
			__result = false;
			return false;
		}

		private static bool TryStartAttack_Prefix(Pawn __instance, ref bool __result)
		{
			if (!YoungPawnCombatUtility.IsNonViolentYoungPawn(__instance))
			{
				return true;
			}

			__instance.jobs?.EndCurrentJob(JobCondition.Incompletable);
			__result = false;
			return false;
		}

		/// <summary>
		/// Prevents hostile toddlers from being considered hostile to player pawns/things,
		/// which blocks AttackTargetsCache registration and all downstream attack paths.
		/// </summary>
		private static void HostileTo_ThingThing_Postfix(Thing a, Thing b, ref bool __result)
		{
			if (!__result || !Core.ToddlersExpansionSettings.preventColonistAttackingHostileToddler)
			{
				return;
			}

			Pawn pawnA = a as Pawn;
			Pawn pawnB = b as Pawn;

			if (pawnA != null && YoungPawnCombatUtility.ShouldPreventColonistAttackingHostileToddler(pawnA)
			    && (pawnB == null || pawnB.Faction == Faction.OfPlayer))
			{
				__result = false;
				return;
			}

			if (pawnB != null && YoungPawnCombatUtility.ShouldPreventColonistAttackingHostileToddler(pawnB)
			    && (pawnA == null || pawnA.Faction == Faction.OfPlayer))
			{
				__result = false;
			}
		}

		/// <summary>
		/// Prevents hostile toddlers from being registered as hostile to Faction.OfPlayer
		/// in AttackTargetsCache, DangerWatcher, AutoUndrafter, etc.
		/// </summary>
		private static void HostileTo_ThingFaction_Postfix(Thing t, Faction fac, ref bool __result)
		{
			if (!__result || !Core.ToddlersExpansionSettings.preventColonistAttackingHostileToddler || fac != Faction.OfPlayer)
			{
				return;
			}

			if (t is Pawn pawn && YoungPawnCombatUtility.ShouldPreventColonistAttackingHostileToddler(pawn))
			{
				__result = false;
			}
		}
	}
}
