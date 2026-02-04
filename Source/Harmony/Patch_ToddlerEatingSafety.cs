using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;

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

			if (IsEating(baby) || IsBathing(baby))
			{
				__result = true;
			}
		}

		private static bool IsEating(Pawn pawn)
		{
			Need_Food foodNeed = pawn.needs?.food;
			return foodNeed != null && foodNeed.GainingFood();
		}

		private static bool IsBathing(Pawn pawn)
		{
			return pawn.CurJobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfBath;
		}
	}
}
