using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerEatingSafety
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type utilityType = AccessTools.TypeByName("Toddlers.ToddlerUtility");
			if (utilityType == null)
			{
				return;
			}

			MethodInfo isBabyBusy = AccessTools.Method(utilityType, "IsBabyBusy", new[] { typeof(Pawn) });
			if (isBabyBusy == null)
			{
				return;
			}

			harmony.Patch(isBabyBusy, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(IsBabyBusy_Postfix)));

			Type childcareUtilityType = AccessTools.TypeByName("RimWorld.ChildcareUtility");
			if (childcareUtilityType != null)
			{
				MethodInfo findAutofeedBaby = AccessTools.Method(childcareUtilityType, "FindAutofeedBaby", new[] { typeof(Pawn), typeof(AutofeedMode), typeof(Thing).MakeByRefType() });
				if (findAutofeedBaby != null)
				{
					harmony.Patch(findAutofeedBaby, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(FindAutofeedBaby_Postfix)));
				}

				MethodInfo findUnsafeBaby = AccessTools.Method(childcareUtilityType, "FindUnsafeBaby", new[] { typeof(Pawn), typeof(AutofeedMode) });
				if (findUnsafeBaby != null)
				{
					harmony.Patch(findUnsafeBaby, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(FindUnsafeBaby_Postfix)));
				}
			}

			Type playWithSadBabyType = AccessTools.TypeByName("Toddlers.JobGiver_PlayWithSadBaby");
			if (playWithSadBabyType != null)
			{
				MethodInfo findSadBaby = AccessTools.Method(playWithSadBabyType, "FindSadBaby", new[] { typeof(Pawn) });
				if (findSadBaby != null)
				{
					harmony.Patch(findSadBaby, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(FindSadBaby_Postfix)));
				}
			}
		}

		private static void IsBabyBusy_Postfix(Pawn baby, ref bool __result)
		{
			if (__result || baby == null)
			{
				return;
			}

			if (!ToddlersCompatUtility.IsToddler(baby))
			{
				return;
			}

			if (IsAutoTargetProtected(baby))
			{
				__result = true;
			}
		}

		private static void FindAutofeedBaby_Postfix(ref Pawn __result)
		{
			if (__result != null && IsAutoTargetProtected(__result))
			{
				__result = null;
			}
		}

		private static void FindUnsafeBaby_Postfix(ref Pawn __result)
		{
			if (__result != null && IsAutoTargetProtected(__result))
			{
				__result = null;
			}
		}

		private static void FindSadBaby_Postfix(ref Pawn __result)
		{
			if (__result != null && IsAutoTargetProtected(__result))
			{
				__result = null;
			}
		}

		private static bool IsAutoTargetProtected(Pawn pawn)
		{
			if (pawn == null || !ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			return IsEating(pawn) || IsBathing(pawn);
		}

		private static bool IsEating(Pawn pawn)
		{
			if (pawn?.jobs?.curDriver is JobDriver_Ingest)
			{
				return true;
			}

			JobDef curJobDef = pawn?.CurJobDef;
			if (curJobDef == JobDefOf.Ingest)
			{
				return true;
			}

			Need_Food foodNeed = pawn.needs?.food;
			return foodNeed != null && foodNeed.GainingFood();
		}

		private static bool IsBathing(Pawn pawn)
		{
			return pawn.CurJobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfBath;
		}
	}
}
