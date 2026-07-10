using System;
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
		private static bool _postfixFailureWarned;

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
			try
			{
				StartJobPostfixSafely(newJob, ___pawn);
			}
			catch (Exception ex)
			{
				WarnPostfixFailureOnce(ex);
			}
		}

		private static void StartJobPostfixSafely(Job newJob, Pawn pawn)
		{
			if (!IsPotentialSmallPawn(pawn))
			{
				return;
			}

			if (!Prefs.DevMode && !ToddlerPlayAnimationUtility.HasManagedPlayAnimation(pawn))
			{
				return;
			}

			bool cleared = TryClearManagedPlayAnimationOnJobStart(pawn, newJob);
			if (MutualPlayDiagnostics.IsMutualPlayJob(newJob))
			{
				MutualPlayDiagnostics.Log(
					pawn,
					"StartJobPostfix",
					$"requested={MutualPlayDiagnostics.DescribeJob(newJob)} " +
					$"actualCurrent={MutualPlayDiagnostics.DescribeJob(pawn.jobs?.curJob)} " +
					$"sameInstance={ReferenceEquals(newJob, pawn.jobs?.curJob)}");
			}

			JobDef newJobDef = newJob?.def;
			if (Prefs.DevMode && newJobDef != null)
			{
				Log.Message(
					$"[RimTalk_ToddlersExpansion] Toddler job: {SafePawnLabel(pawn)} -> " +
					$"{newJobDef.defName ?? "<unnamed-job>"}{(cleared ? " (cleared managed play animation)" : string.Empty)}");
			}
		}

		private static bool TryClearManagedPlayAnimationOnJobStart(Pawn pawn, Job newJob)
		{
			if (pawn == null || newJob == null || !IsPotentialSmallPawn(pawn))
			{
				return false;
			}

			try
			{
				AnimationDef before = pawn.Drawer?.renderer?.CurAnimation;

				// Clear our native toddler play animations on every job boundary.
				// Yayo keeps its own render-state in PawnDrawData, while our managed native
				// animations live on PawnRenderer.CurAnimation and must not leak into
				// unrelated jobs such as Ingest/LeaveCrib/Wait_MaintainPosture.
				bool cleared = ToddlerPlayAnimationUtility.ClearManagedNativePlayAnimation(pawn);
				JobDef newJobDef = newJob.def;

				if (Prefs.DevMode && (before != null || newJobDef == JobDefOf.Ingest))
				{
					Log.Message(
						$"[RimTalk_ToddlersExpansion] Job-start animation cleanup: pawn={SafePawnLabel(pawn)} " +
						$"newJob={newJobDef?.defName ?? "<null-def>"} " +
						$"curAnimationBefore={before?.defName ?? "null"} cleared={cleared}");
				}

				return cleared;
			}
			catch (Exception ex)
			{
				WarnPostfixFailureOnce(ex);
				return false;
			}
		}

		private static string SafePawnLabel(Pawn pawn)
		{
			try
			{
				return pawn?.LabelShort ?? "<null-pawn>";
			}
			catch
			{
				return pawn == null ? "<null-pawn>" : $"Pawn#{pawn.thingIDNumber}";
			}
		}

		private static void WarnPostfixFailureOnce(Exception ex)
		{
			if (_postfixFailureWarned)
			{
				return;
			}

			_postfixFailureWarned = true;
			try
			{
				Log.Warning(
					$"[RimTalk_ToddlersExpansion] Toddler job logging failed and was safely ignored: " +
					$"{ex?.GetType().Name ?? "unknown"}: {ex?.Message ?? "no message"}");
			}
			catch
			{
			}
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
