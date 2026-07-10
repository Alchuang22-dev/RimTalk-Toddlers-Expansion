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
		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo startJobTarget = AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob));
			if (startJobTarget != null)
			{
				harmony.Patch(startJobTarget, postfix: new HarmonyMethod(typeof(Patch_ToddlerJobLogging), nameof(StartJob_Postfix)));
			}
		}

		private static void StartJob_Postfix(Job newJob, Pawn ___pawn)
		{
			if (!IsPotentialSmallPawn(___pawn))
			{
				return;
			}

			if (!Prefs.DevMode && !ToddlerPlayAnimationUtility.HasManagedPlayAnimation(___pawn))
			{
				return;
			}

			bool cleared = TryClearManagedPlayAnimationOnJobStart(___pawn, newJob);
			if (MutualPlayDiagnostics.IsMutualPlayJob(newJob))
			{
				MutualPlayDiagnostics.Log(
					___pawn,
					"StartJobPostfix",
					$"requested={MutualPlayDiagnostics.DescribeJob(newJob)} " +
					$"actualCurrent={MutualPlayDiagnostics.DescribeJob(___pawn.jobs?.curJob)} " +
					$"sameInstance={ReferenceEquals(newJob, ___pawn.jobs?.curJob)}");
			}

			if (Prefs.DevMode && newJob?.def != null)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] Toddler job: {___pawn.LabelShort} -> {newJob.def.defName}{(cleared ? " (cleared managed play animation)" : string.Empty)}");
			}
		}

		private static bool TryClearManagedPlayAnimationOnJobStart(Pawn pawn, Job newJob)
		{
			if (pawn == null || newJob == null || !IsPotentialSmallPawn(pawn))
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

		private static bool IsPotentialSmallPawn(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			if (pawn.DevelopmentalStage.Baby() || pawn.DevelopmentalStage.Newborn())
			{
				return true;
			}

			return ToddlersCompatUtility.IsToddler(pawn);
		}
	}
}
