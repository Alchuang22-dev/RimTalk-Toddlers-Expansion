using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlersWashBabyBathRules
	{
		private static bool _skip;
		private static MethodInfo _getWashJob;
		private static MethodInfo _findBathOrTub;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type type = AccessTools.TypeByName("Toddlers.WashBabyUtility");
			if (type == null)
			{
				return;
			}

			_getWashJob = AccessTools.Method(type, "GetWashJob", new[] { typeof(Pawn), typeof(Pawn), typeof(bool) });
			if (_getWashJob == null)
			{
				return;
			}

			_findBathOrTub = AccessTools.Method(type, "FindBathOrTub", new[] { typeof(Pawn), typeof(Pawn), typeof(Thing).MakeByRefType() });

			harmony.Patch(_getWashJob, prefix: new HarmonyMethod(typeof(Patch_ToddlersWashBabyBathRules), nameof(GetWashJob_Prefix)));
		}

		private static bool GetWashJob_Prefix(Pawn carer, Pawn baby, bool allowBath, ref Job __result)
		{
			if (_skip || carer == null || baby == null)
			{
				return true;
			}

			if (!allowBath)
			{
				return true;
			}

			if (!ToddlersCompatUtility.IsToddler(baby))
			{
				return true;
			}

			if (HealthAIUtility.ShouldSeekMedicalRest(baby))
			{
				return true;
			}

			if (Rand.Value < 0.2f && TryFindBathOrTub(carer, baby, out Thing bath))
			{
				JobDef bathDef = DefDatabase<JobDef>.GetNamedSilentFail("CYB_BatheToddler");
				if (bathDef != null)
				{
					__result = JobMaker.MakeJob(bathDef, baby, bath);
					__result.count = 1;
					return false;
				}
			}

			_skip = true;
			try
			{
				__result = _getWashJob.Invoke(null, new object[] { carer, baby, false }) as Job;
			}
			finally
			{
				_skip = false;
			}

			return false;
		}

		private static bool TryFindBathOrTub(Pawn carer, Pawn baby, out Thing bath)
		{
			bath = null;
			if (_findBathOrTub == null)
			{
				return false;
			}

			object[] args = { carer, baby, null };
			bool found = false;
			try
			{
				found = (bool)_findBathOrTub.Invoke(null, args);
				bath = args[2] as Thing;
			}
			catch
			{
				found = false;
			}

			return found && bath != null;
		}
	}
}
