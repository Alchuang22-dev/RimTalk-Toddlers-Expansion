using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	internal static class Patch_ToddlerLonelyPlay
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			harmony.Patch(AccessTools.Method(typeof(WorkGiver_PlayWithBaby), nameof(WorkGiver_PlayWithBaby.HasJobOnThing)),
				prefix: new HarmonyMethod(typeof(Patch_ToddlerLonelyPlay), nameof(HasJobOnThing_Prefix)));
		}

		private static bool HasJobOnThing_Prefix(Pawn pawn, Thing t, bool forced, ref bool __result)
		{
			if (!(t is Pawn toddler) || !toddler.DevelopmentalStage.Baby() || toddler.needs?.play == null)
			{
				return true;
			}

			// Keep the normal path whenever play level already permits the job.
			if (forced
				? !ToddlerLonelinessUtility.WantsMoreCompany(toddler) || toddler.needs.play.CurLevelPercentage < 0.95f
				: !toddler.Awake() || !ToddlerLonelinessUtility.SeeksCompany(toddler) || toddler.needs.play.IsLow)
			{
				return true;
			}

			// Only replace the need check; the normal play workers still check reachability and safety.
			foreach (BabyPlayDef play in DefDatabase<BabyPlayDef>.AllDefs.InRandomOrder())
			{
				if (play.Worker.CanDo(pawn, toddler))
				{
					__result = true;
					return false;
				}
			}

			__result = false;
			return false;
		}
	}
}
