using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_LearningGiver_NatureRunning
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			var target = AccessTools.Method(typeof(LearningGiver_NatureRunning), nameof(LearningGiver_NatureRunning.TryGiveJob));
			if (target == null)
			{
				Log.Warning("[RimTalk_ToddlersExpansion] Could not find LearningGiver_NatureRunning.TryGiveJob.");
				return;
			}

			harmony.Patch(target, prefix: new HarmonyMethod(typeof(Patch_LearningGiver_NatureRunning), nameof(TryGiveJob_Prefix)));
		}

		private static bool TryGiveJob_Prefix(Pawn pawn, ref Job __result)
		{
			if (pawn == null || pawn.Map == null)
			{
				return true;
			}

			if (pawn.Faction != Faction.OfPlayer)
			{
				return true;
			}

			if (!pawn.DevelopmentalStage.Child() && !ToddlersCompatUtility.IsToddler(pawn))
			{
				return true;
			}

			ToddlerOutingMapComponent component = pawn.Map.GetComponent<ToddlerOutingMapComponent>();
			if (component == null)
			{
				return true;
			}

			if (!component.TryStartOuting(pawn, out ToddlerOutingSession session))
			{
				return true;
			}

			Job job = component.CreateOutingJob(pawn, session);
			if (job == null)
			{
				return true;
			}

			__result = job;
			return false;
		}
	}
}
