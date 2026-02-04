using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerEatingSpeed
	{
		private const string ToddlersHarmonyId = "cyanobot.toddlers";

		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo chewDurationGetter = AccessTools.PropertyGetter(typeof(JobDriver_Ingest), "ChewDurationMultiplier");
			if (chewDurationGetter == null)
			{
				return;
			}

			var postfix = new HarmonyMethod(typeof(Patch_ToddlerEatingSpeed), nameof(ChewDurationMultiplier_Postfix))
			{
				after = new[] { ToddlersHarmonyId },
				priority = Priority.Last
			};

			harmony.Patch(chewDurationGetter, postfix: postfix);
		}

		private static void ChewDurationMultiplier_Postfix(JobDriver_Ingest __instance, ref float __result)
		{
			Pawn pawn = __instance?.pawn;
			if (pawn == null || !ToddlersCompatUtility.IsToddler(pawn))
			{
				return;
			}

			float speedFactor = ToddlersExpansionSettings.toddlerEatingSpeedFactor;
			if (speedFactor <= 0f || speedFactor == 1f)
			{
				return;
			}

			__result /= speedFactor;
		}
	}
}
