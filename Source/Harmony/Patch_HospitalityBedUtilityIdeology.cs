using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_HospitalityBedUtilityIdeology
	{
		private const string HospitalityBedUtilityTypeName = "Hospitality.Utilities.BedUtility";
		private const string HospitalityClaimBedJobGiverTypeName = "Hospitality.JobGiver_ClaimBed";
		private const string HospitalitySleepJobGiverTypeName = "Hospitality.JobGiver_Sleep";

		public static void Init(HarmonyLib.Harmony harmony)
		{
			PatchIdeologyFulfillment(harmony);
			PatchClaimBedForYoungGuests(harmony);
			PatchSleepForYoungGuests(harmony);
		}

		private static void PatchIdeologyFulfillment(HarmonyLib.Harmony harmony)
		{
			Type bedUtilityType = AccessTools.TypeByName(HospitalityBedUtilityTypeName);
			if (bedUtilityType == null)
			{
				return;
			}

			MethodInfo ideologyMethod = AccessTools.Method(
				bedUtilityType,
				"Ideology_GetFulfillment",
				new[] { typeof(Building_Bed), typeof(Pawn) });
			if (ideologyMethod == null || ideologyMethod.ReturnType != typeof(int))
			{
				return;
			}

			MethodInfo prefix = AccessTools.Method(
				typeof(Patch_HospitalityBedUtilityIdeology),
				nameof(Ideology_GetFulfillment_Prefix));
			harmony.Patch(ideologyMethod, prefix: new HarmonyMethod(prefix));
		}

		private static void PatchClaimBedForYoungGuests(HarmonyLib.Harmony harmony)
		{
			Type claimBedType = AccessTools.TypeByName(HospitalityClaimBedJobGiverTypeName);
			if (claimBedType == null)
			{
				return;
			}

			MethodInfo tryGiveJob = AccessTools.Method(claimBedType, "TryGiveJob", new[] { typeof(Pawn) });
			if (tryGiveJob == null || tryGiveJob.DeclaringType != claimBedType || tryGiveJob.ReturnType != typeof(Job))
			{
				return;
			}

			MethodInfo prefix = AccessTools.Method(
				typeof(Patch_HospitalityBedUtilityIdeology),
				nameof(ClaimBed_TryGiveJob_Prefix));
			harmony.Patch(tryGiveJob, prefix: new HarmonyMethod(prefix));
		}

		private static void PatchSleepForYoungGuests(HarmonyLib.Harmony harmony)
		{
			Type sleepType = AccessTools.TypeByName(HospitalitySleepJobGiverTypeName);
			if (sleepType == null)
			{
				return;
			}

			MethodInfo tryIssueJobPackage = AccessTools.Method(
				sleepType,
				"TryIssueJobPackage",
				new[] { typeof(Pawn), typeof(JobIssueParams) });
			if (tryIssueJobPackage == null || tryIssueJobPackage.DeclaringType != sleepType
				|| tryIssueJobPackage.ReturnType != typeof(ThinkResult))
			{
				return;
			}

			MethodInfo prefix = AccessTools.Method(
				typeof(Patch_HospitalityBedUtilityIdeology),
				nameof(Sleep_TryIssueJobPackage_Prefix));
			harmony.Patch(tryIssueJobPackage, prefix: new HarmonyMethod(prefix));
		}

		private static bool Ideology_GetFulfillment_Prefix(Building_Bed __0, Pawn __1, ref int __result)
		{
			Pawn guest = __1;
			if (!ModsConfig.IdeologyActive || __0 == null || guest == null
				|| ToddlersCompatUtility.IsToddlerOrBaby(guest)
				|| guest.ideo == null || guest.Ideo?.PreceptsListForReading == null)
			{
				__result = 0;
				return false;
			}

			return true;
		}

		private static bool ClaimBed_TryGiveJob_Prefix(Pawn __0, ref Job __result)
		{
			if (!ToddlersCompatUtility.IsToddlerOrBaby(__0))
			{
				return true;
			}

			__result = null;
			return false;
		}

		private static bool Sleep_TryIssueJobPackage_Prefix(Pawn __0, ThinkNode __instance, ref ThinkResult __result)
		{
			Pawn pawn = __0;
			if (!ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return true;
			}

			if (pawn?.CurJob != null)
			{
				__result = new ThinkResult(pawn.CurJob, __instance);
				return false;
			}

			if (pawn?.needs?.rest == null || pawn.MapHeld == null || !pawn.Spawned)
			{
				__result = ThinkResult.NoJob;
				return false;
			}

			Pawn_MindState mindState = pawn.mindState;
			if (mindState != null && Find.TickManager != null
				&& Find.TickManager.TicksGame - mindState.lastDisturbanceTick < 400)
			{
				__result = ThinkResult.NoJob;
				return false;
			}

			IntVec3 vec = CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.MapHeld, 4);
			if (!vec.IsValid || !pawn.CanReserve(vec))
			{
				__result = ThinkResult.NoJob;
				return false;
			}

			__result = new ThinkResult(new Job(JobDefOf.LayDown, vec), __instance);
			return false;
		}
	}
}
