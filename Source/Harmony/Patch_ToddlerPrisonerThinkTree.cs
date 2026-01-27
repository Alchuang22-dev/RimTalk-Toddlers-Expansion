using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerPrisonerThinkTree
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo prisonerSatisfied = AccessTools.Method(typeof(ThinkNode_ConditionalPrisoner), "Satisfied", new[] { typeof(Pawn) });
			if (prisonerSatisfied != null)
			{
				harmony.Patch(prisonerSatisfied, postfix: new HarmonyMethod(typeof(Patch_ToddlerPrisonerThinkTree), nameof(ConditionalPrisoner_Postfix)));
			}

			MethodInfo colonistSatisfied = AccessTools.Method(typeof(ThinkNode_ConditionalColonist), "Satisfied", new[] { typeof(Pawn) });
			if (colonistSatisfied != null)
			{
				harmony.Patch(colonistSatisfied, postfix: new HarmonyMethod(typeof(Patch_ToddlerPrisonerThinkTree), nameof(ConditionalColonist_Postfix)));
			}
		}

		private static void ConditionalPrisoner_Postfix(Pawn pawn, ref bool __result)
		{
			if (!__result || pawn == null)
			{
				return;
			}

			if (pawn.IsPrisoner && ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				__result = false;
			}
		}

		private static void ConditionalColonist_Postfix(Pawn pawn, ref bool __result)
		{
			if (__result || pawn == null)
			{
				return;
			}

			if (pawn.IsPrisoner && ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				__result = true;
			}
		}
	}
}
