using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

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

			Type childcareUtilityType = AccessTools.TypeByName("RimWorld.ChildcareUtility");
			if (childcareUtilityType != null)
			{
				MethodInfo wantsSuckle = AccessTools.Method(childcareUtilityType, "WantsSuckle", new[] { typeof(Pawn), typeof(ChildcareUtility.BreastfeedFailReason?).MakeByRefType() });
				if (wantsSuckle != null)
				{
					harmony.Patch(wantsSuckle, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(WantsSuckle_Postfix)));
				}

				MethodInfo findAutofeedBaby = AccessTools.Method(childcareUtilityType, "FindAutofeedBaby", new[] { typeof(Pawn), typeof(AutofeedMode), typeof(Thing).MakeByRefType() });
				if (findAutofeedBaby != null)
				{
					harmony.Patch(findAutofeedBaby, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(FindAutofeedBaby_Postfix)));
				}

				MethodInfo findUnsafeBaby = AccessTools.Method(childcareUtilityType, "FindUnsafeBaby", new[] { typeof(Pawn), typeof(AutofeedMode) });
				if (findUnsafeBaby != null)
				{
					harmony.Patch(findUnsafeBaby, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(FindUnsafeBaby_Postfix)));
				}

				MethodInfo makeAutofeedBabyJob = AccessTools.Method(childcareUtilityType, "MakeAutofeedBabyJob", new[] { typeof(Pawn), typeof(Pawn), typeof(Thing) });
				if (makeAutofeedBabyJob != null)
				{
					harmony.Patch(makeAutofeedBabyJob, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(MakeAutofeedBabyJob_Postfix)));
				}
			}

			Type manualFeedWorkGiverType = AccessTools.TypeByName("RimWorld.WorkGiver_FeedBabyManually");
			if (manualFeedWorkGiverType != null)
			{
				MethodInfo canCreateManualFeedingJob = AccessTools.Method(manualFeedWorkGiverType, "CanCreateManualFeedingJob", new[] { typeof(Pawn), typeof(Thing), typeof(bool) });
				if (canCreateManualFeedingJob != null)
				{
					harmony.Patch(canCreateManualFeedingJob, prefix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(CanCreateManualFeedingJob_Prefix)));
				}
			}

			MethodInfo breastfeedToil = AccessTools.Method(typeof(JobDriver_Breastfeed), "Breastfeed");
			if (breastfeedToil != null)
			{
				harmony.Patch(breastfeedToil, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(Breastfeed_Postfix)));
			}

			MethodInfo bottleFeedToil = AccessTools.Method(typeof(JobDriver_BottleFeedBaby), "FeedBabyFoodFromInventory");
			if (bottleFeedToil != null)
			{
				harmony.Patch(bottleFeedToil, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(BottleFeedBabyFoodFromInventory_Postfix)));
			}

			Type workGiverPlayWithBabyType = AccessTools.TypeByName("RimWorld.WorkGiver_PlayWithBaby");
			if (workGiverPlayWithBabyType != null)
			{
				MethodInfo hasJobOnThing = AccessTools.Method(workGiverPlayWithBabyType, "HasJobOnThing", new[] { typeof(Pawn), typeof(Thing), typeof(bool) });
				if (hasJobOnThing != null)
				{
					harmony.Patch(hasJobOnThing, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(PlayWithBaby_HasJobOnThing_Postfix)));
				}

				MethodInfo jobOnThing = AccessTools.Method(workGiverPlayWithBabyType, "JobOnThing", new[] { typeof(Pawn), typeof(Thing), typeof(bool) });
				if (jobOnThing != null)
				{
					harmony.Patch(jobOnThing, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(PlayWithBaby_JobOnThing_Postfix)));
				}
			}

			Type workGiverBringBabyToSafetyType = AccessTools.TypeByName("RimWorld.WorkGiver_BringBabyToSafety");
			if (workGiverBringBabyToSafetyType != null)
			{
				MethodInfo nonScanJob = AccessTools.Method(workGiverBringBabyToSafetyType, "NonScanJob", new[] { typeof(Pawn) });
				if (nonScanJob != null)
				{
					harmony.Patch(nonScanJob, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(WorkGiver_BringBabyToSafety_Postfix)));
				}
			}

			Type jobGiverBringBabyToSafetyType = AccessTools.TypeByName("RimWorld.JobGiver_BringBabyToSafety");
			if (jobGiverBringBabyToSafetyType != null)
			{
				MethodInfo tryGiveJob = AccessTools.Method(jobGiverBringBabyToSafetyType, "TryGiveJob", new[] { typeof(Pawn) });
				if (tryGiveJob != null)
				{
					harmony.Patch(tryGiveJob, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(JobGiver_BringBabyToSafety_Postfix)));
				}
			}

			Type playWithSadBabyType = AccessTools.TypeByName("Toddlers.JobGiver_PlayWithSadBaby");
			if (playWithSadBabyType != null)
			{
				MethodInfo findSadBaby = AccessTools.Method(playWithSadBabyType, "FindSadBaby", new[] { typeof(Pawn) });
				if (findSadBaby != null)
				{
					harmony.Patch(findSadBaby, postfix: new HarmonyMethod(typeof(Patch_ToddlerEatingSafety), nameof(FindSadBaby_Postfix)));
				}
			}

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

			if (IsAutoTargetProtected(baby))
			{
				__result = true;
			}
		}

		private static void WantsSuckle_Postfix(Pawn baby, ref bool __result)
		{
			if (__result && IsAutoTargetProtected(baby))
			{
				__result = false;
			}
		}

		private static void FindAutofeedBaby_Postfix(ref Pawn __result)
		{
			if (__result != null && IsAutoTargetProtected(__result))
			{
				__result = null;
			}
		}

		private static void MakeAutofeedBabyJob_Postfix(Pawn baby, ref Job __result)
		{
			if (__result != null && IsAutoTargetProtected(baby))
			{
				__result = null;
			}
		}

		private static void FindUnsafeBaby_Postfix(ref Pawn __result)
		{
			if (__result != null && IsAutoTargetProtected(__result))
			{
				__result = null;
			}
		}

		private static void FindSadBaby_Postfix(ref Pawn __result)
		{
			if (__result != null && IsAutoTargetProtected(__result))
			{
				__result = null;
			}
		}

		private static void PlayWithBaby_HasJobOnThing_Postfix(Thing t, bool forced, ref bool __result)
		{
			if (!__result || forced || !(t is Pawn targetPawn))
			{
				return;
			}

			if (IsAutoTargetProtected(targetPawn))
			{
				__result = false;
			}
		}

		private static void PlayWithBaby_JobOnThing_Postfix(Thing t, bool forced, ref Job __result)
		{
			if (__result == null || forced || !(t is Pawn targetPawn))
			{
				return;
			}

			if (IsAutoTargetProtected(targetPawn))
			{
				__result = null;
			}
		}

		private static void WorkGiver_BringBabyToSafety_Postfix(ref Job __result)
		{
			CancelIfProtectedTarget(ref __result);
		}

		private static void JobGiver_BringBabyToSafety_Postfix(ref Job __result)
		{
			CancelIfProtectedTarget(ref __result);
		}

		private static bool CanCreateManualFeedingJob_Prefix(Thing t, bool forced, ref bool __result)
		{
			if (forced)
			{
				return true;
			}

			if (!(t is Pawn toddler))
			{
				return true;
			}

			if (IsAutoTargetProtected(toddler))
			{
				__result = false;
				return false;
			}

			return true;
		}

		private static void Breastfeed_Postfix(JobDriver_Breastfeed __instance, ref Toil __result)
		{
			if (__result == null || __instance == null)
			{
				return;
			}

			__result.AddFailCondition(() => ShouldBlockAutomaticFeeding(GetFeedJobTargetToddler(__instance.job), __instance.job));
		}

		private static void BottleFeedBabyFoodFromInventory_Postfix(JobDriver_BottleFeedBaby __instance, ref Toil __result)
		{
			if (__result == null || __instance == null)
			{
				return;
			}

			__result.AddFailCondition(() => ShouldBlockAutomaticFeeding(GetFeedJobTargetToddler(__instance.job), __instance.job));
		}

		private static bool ShouldBlockAutomaticFeeding(Pawn baby, Job feederJob)
		{
			if (!IsAutoTargetProtected(baby))
			{
				return false;
			}

			return feederJob == null || !feederJob.playerForced;
		}

		private static Pawn GetFeedJobTargetToddler(Job job)
		{
			if (job == null || !job.targetA.HasThing)
			{
				return null;
			}

			return job.targetA.Thing as Pawn;
		}

		private static bool IsAutoTargetProtected(Pawn pawn)
		{
			if (pawn == null || !ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			// Includes prisoner toddlers: same protection rule during eat/bath.
			return IsEating(pawn) || IsBathing(pawn);
		}

		private static bool IsEating(Pawn pawn)
		{
			if (pawn?.jobs?.curDriver is JobDriver_Ingest)
			{
				return true;
			}

			JobDef curJobDef = pawn?.CurJobDef;
			if (curJobDef == JobDefOf.Ingest)
			{
				return true;
			}

			if (curJobDef == JobDefOf.TakeFromOtherInventory
				&& pawn.CurJob != null
				&& pawn.CurJob.targetA.HasThing
				&& FoodUtility.WillEat(pawn, pawn.CurJob.targetA.Thing))
			{
				return true;
			}

			return false;
		}

		private static bool IsBathing(Pawn pawn)
		{
			return pawn.CurJobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfBath;
		}

		private static void CancelIfProtectedTarget(ref Job job)
		{
			if (job == null)
			{
				return;
			}

			Pawn targetPawn = GetJobTargetPawn(job);
			if (targetPawn != null && IsAutoTargetProtected(targetPawn))
			{
				job = null;
			}
		}

		private static Pawn GetJobTargetPawn(Job job)
		{
			if (job == null)
			{
				return null;
			}

			return GetPawnFromTarget(job.targetA)
				?? GetPawnFromTarget(job.targetB)
				?? GetPawnFromTarget(job.targetC);
		}

		private static Pawn GetPawnFromTarget(LocalTargetInfo target)
		{
			return target.IsValid && target.HasThing ? target.Thing as Pawn : null;
		}
	}
}
