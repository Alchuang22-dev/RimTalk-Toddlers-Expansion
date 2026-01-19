using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlersJobsAvailability
	{
		private static readonly WorkGiver_ToddlerSelfPlay SelfPlayGiver = new WorkGiver_ToddlerSelfPlay();
		private static readonly WorkGiver_ToddlerMutualPlay MutualPlayGiver = new WorkGiver_ToddlerMutualPlay();
		private const float OverrideChance = 0.5f;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			if (!ToddlersCompatUtility.IsToddlersActive)
			{
				return;
			}

			Type jobGiverType = AccessTools.TypeByName("Toddlers.JobGiver_ToddlerPlay");
			if (jobGiverType == null)
			{
				return;
			}

			MethodInfo target = AccessTools.Method(jobGiverType, "TryGiveJob", new[] { typeof(Pawn) });
			if (target == null)
			{
				return;
			}

			MethodInfo postfix = AccessTools.Method(typeof(Patch_ToddlersJobsAvailability), nameof(TryGiveJob_Postfix));
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
		}

		private static void TryGiveJob_Postfix(Pawn pawn, ref Job __result)
		{
			if (pawn == null)
			{
				return;
			}

			Job job = MutualPlayGiver.TryGiveJob(pawn) ?? SelfPlayGiver.TryGiveJob(pawn);
			if (job == null)
			{
				return;
			}

			if (__result == null || Rand.Chance(OverrideChance))
			{
				__result = job;
			}
		}
	}
}
