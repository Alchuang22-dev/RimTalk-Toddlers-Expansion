using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerPrisonerApparel
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo tryGiveJob = AccessTools.Method(typeof(JobGiver_OptimizeApparel), "TryGiveJob", new[] { typeof(Pawn) });
			if (tryGiveJob != null)
			{
				harmony.Patch(tryGiveJob, prefix: new HarmonyMethod(typeof(Patch_ToddlerPrisonerApparel), nameof(TryGiveJob_Prefix)));
			}
		}

		private static bool TryGiveJob_Prefix(Pawn pawn, ref Job __result)
		{
			if (pawn != null && pawn.IsPrisoner && ToddlersCompatUtility.IsToddler(pawn))
			{
				__result = null;
				return false;
			}

			return true;
		}
	}
}
