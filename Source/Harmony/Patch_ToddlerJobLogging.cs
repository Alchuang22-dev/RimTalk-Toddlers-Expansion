using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
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
				// Still perform cleanup even when not logging.
				Pawn nonLoggedPawn = GetPawnFromJobTracker(__instance);
				TryClearManagedPlayAnimationOnJobStart(nonLoggedPawn, newJob);
				return;
			}

			Pawn pawn = GetPawnFromJobTracker(__instance);
			if (pawn == null || !ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return;
			}

			bool cleared = TryClearManagedPlayAnimationOnJobStart(pawn, newJob);
			Log.Message($"[RimTalk_ToddlersExpansion] Toddler job: {pawn.LabelShort} -> {newJob.def.defName}{(cleared ? " (cleared managed play animation)" : string.Empty)}");
		}

		private static bool TryClearManagedPlayAnimationOnJobStart(Pawn pawn, Job newJob)
		{
			if (pawn == null || newJob == null || !ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return false;
			}

			AnimationDef before = pawn.Drawer?.renderer?.CurAnimation;

			// Clear our native toddler play animations on every job boundary.
			// Yayo keeps its own render-state in PawnDrawData, while our managed native
			// animations live on PawnRenderer.CurAnimation and must not leak into
			// unrelated jobs such as Ingest/LeaveCrib/Wait_MaintainPosture.
			bool cleared = ToddlerPlayAnimationUtility.ClearManagedNativePlayAnimation(pawn);

			if (Prefs.DevMode && (before != null || newJob.def == JobDefOf.Ingest))
			{
				Log.Message(
					$"[RimTalk_ToddlersExpansion] Job-start animation cleanup: pawn={pawn.LabelShort} " +
					$"newJob={newJob.def.defName} curAnimationBefore={before?.defName ?? "null"} cleared={cleared}");
			}

			return cleared;
		}
	}
}
