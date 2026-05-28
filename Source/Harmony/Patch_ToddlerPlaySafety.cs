using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerPlaySafety
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type floorDrawingType = AccessTools.TypeByName("Toddlers.ToddlerPlayGiver_Floordrawing");
			if (floorDrawingType == null)
			{
				return;
			}

			MethodInfo canFloorDrawFrom = AccessTools.Method(floorDrawingType, "CanFloorDrawFrom", new[] { typeof(IntVec3), typeof(Pawn) });
			if (canFloorDrawFrom == null)
			{
				return;
			}

			harmony.Patch(canFloorDrawFrom, postfix: new HarmonyMethod(typeof(Patch_ToddlerPlaySafety), nameof(CanFloorDrawFrom_Postfix)));
		}

		private static void CanFloorDrawFrom_Postfix(IntVec3 spot, Pawn drawer, ref bool __result)
		{
			if (!__result)
			{
				return;
			}

			if (!ToddlerPlaySafetyUtility.IsSafePlayCell(drawer, spot))
			{
				__result = false;
			}
		}
	}
}
