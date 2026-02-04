using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_DBHFixtureFloatMenu
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type fixtureType = AccessTools.TypeByName("DubsBadHygiene.Building_AssignableFixture");
			if (fixtureType == null)
			{
				return;
			}

			MethodInfo target = AccessTools.Method(fixtureType, "GetFloatMenuOptions", new[] { typeof(Pawn) });
			if (target == null)
			{
				return;
			}

			MethodInfo postfix = AccessTools.Method(typeof(Patch_DBHFixtureFloatMenu), nameof(GetFloatMenuOptions_Postfix));
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
		}

		private static IEnumerable<FloatMenuOption> GetFloatMenuOptions_Postfix(IEnumerable<FloatMenuOption> __result, Thing __instance, Pawn selPawn)
		{
			if (selPawn == null || !ToddlersCompatUtility.IsToddler(selPawn))
			{
				return __result;
			}

			if (!ToddlerSelfBathUtility.IsBathFixture(__instance))
			{
				return __result;
			}

			return Array.Empty<FloatMenuOption>();
		}
	}
}
