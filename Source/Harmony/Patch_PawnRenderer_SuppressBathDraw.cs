using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_PawnRenderer_SuppressBathDraw
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			var renderPawnAt = AccessTools.Method(typeof(PawnRenderer), "RenderPawnAt");
			if (renderPawnAt != null)
			{
				harmony.Patch(renderPawnAt, prefix: new HarmonyMethod(typeof(Patch_PawnRenderer_SuppressBathDraw), nameof(RenderPawnAt_Prefix)));
			}
		}

		private static bool RenderPawnAt_Prefix(PawnRenderer __instance)
		{
			Pawn pawn = AccessTools.Field(typeof(PawnRenderer), "pawn")?.GetValue(__instance) as Pawn;
			if (pawn == null)
			{
				return true;
			}

			return !ToddlerSelfBathUtility.ShouldSuppressBathRender(pawn);
		}
	}
}
