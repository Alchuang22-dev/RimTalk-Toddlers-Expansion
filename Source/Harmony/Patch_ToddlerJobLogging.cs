using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerJobLogging
	{
		private static FieldInfo _pawnJobTrackerPawnField;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo startJobTarget = AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob));
			if (startJobTarget != null)
			{
				harmony.Patch(startJobTarget, postfix: new HarmonyMethod(typeof(Patch_ToddlerJobLogging), nameof(StartJob_Postfix)));
			}
		}

		private static Pawn GetPawnFromJobTracker(Pawn_JobTracker jobTracker)
		{
			if (_pawnJobTrackerPawnField == null)
			{
				_pawnJobTrackerPawnField = typeof(Pawn_JobTracker).GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic);
			}

			return _pawnJobTrackerPawnField?.GetValue(jobTracker) as Pawn;
		}

		private static void StartJob_Postfix(Pawn_JobTracker __instance, Job newJob)
		{
			if (!Prefs.DevMode || newJob?.def == null)
			{
				return;
			}

			Pawn pawn = GetPawnFromJobTracker(__instance);
			if (pawn == null || !ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return;
			}

			Log.Message($"[RimTalk_ToddlersExpansion] Toddler job: {pawn.LabelShort} -> {newJob.def.defName}");
		}
	}
}
