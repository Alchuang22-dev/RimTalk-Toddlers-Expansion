using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_PawnGroupMakerUtility
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type targetType = typeof(PawnGroupMakerUtility);
			MethodInfo target = AccessTools.Method(targetType, "GeneratePawns", new[] { typeof(PawnGroupMakerParms), typeof(bool) })
				?? AccessTools.Method(targetType, "GeneratePawns", new[] { typeof(PawnGroupMakerParms) });
			if (target == null)
			{
				return;
			}

			MethodInfo postfix = AccessTools.Method(typeof(Patch_PawnGroupMakerUtility), nameof(GeneratePawns_Postfix));
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
		}

		private static void GeneratePawns_Postfix(PawnGroupMakerParms parms, ref IEnumerable<Pawn> __result)
		{
			TravelingPawnInjectionUtility.TryInjectToddlerOrChildPawns(parms, ref __result);
		}
	}
}
