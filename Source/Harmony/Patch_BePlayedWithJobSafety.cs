using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_BePlayedWithJobSafety
	{
		private static FieldInfo _jobCachedDriverField;
		private static JobDef _bePlayedWithDef;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			try
			{
				_jobCachedDriverField = AccessTools.Field(typeof(Job), "cachedDriver");

				Type childcareUtilityType = AccessTools.TypeByName("RimWorld.ChildcareUtility");
				MethodInfo makeBabyPlayJob = AccessTools.Method(childcareUtilityType, "MakeBabyPlayJob", new[] { typeof(Pawn) });
				if (makeBabyPlayJob != null)
				{
					harmony.Patch(makeBabyPlayJob, postfix: new HarmonyMethod(typeof(Patch_BePlayedWithJobSafety), nameof(MakeBabyPlayJob_Postfix)));
				}

				Type workGiverPlayWithBabyType = AccessTools.TypeByName("RimWorld.WorkGiver_PlayWithBaby");
				MethodInfo jobOnThing = AccessTools.Method(workGiverPlayWithBabyType, "JobOnThing", new[] { typeof(Pawn), typeof(Thing), typeof(bool) });
				if (jobOnThing != null)
				{
					harmony.Patch(jobOnThing, postfix: new HarmonyMethod(typeof(Patch_BePlayedWithJobSafety), nameof(PlayWithBaby_JobOnThing_Postfix)));
				}

				MethodInfo getCachedDriver = AccessTools.Method(typeof(Job), nameof(Job.GetCachedDriver), new[] { typeof(Pawn) });
				if (getCachedDriver != null)
				{
					harmony.Patch(getCachedDriver, prefix: new HarmonyMethod(typeof(Patch_BePlayedWithJobSafety), nameof(Job_GetCachedDriver_Prefix)));
				}

				MethodInfo startJob = AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob), new[]
				{
					typeof(Job),
					typeof(JobCondition),
					typeof(ThinkNode),
					typeof(bool),
					typeof(bool),
					typeof(ThinkTreeDef),
					typeof(JobTag?),
					typeof(bool),
					typeof(bool),
					typeof(bool?),
					typeof(bool),
					typeof(bool),
					typeof(bool)
				});
				if (startJob != null)
				{
					harmony.Patch(startJob, prefix: new HarmonyMethod(typeof(Patch_BePlayedWithJobSafety), nameof(PawnJobTracker_StartJob_Prefix)));
				}

				MethodInfo tryTakeOrderedJob = AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob), new[]
				{
					typeof(Job),
					typeof(JobTag?),
					typeof(bool)
				});
				if (tryTakeOrderedJob != null)
				{
					harmony.Patch(tryTakeOrderedJob, prefix: new HarmonyMethod(typeof(Patch_BePlayedWithJobSafety), nameof(PawnJobTracker_TryTakeOrderedJob_Prefix)));
				}
			}
			catch (Exception ex)
			{
				Log.Warning($"[RimTalk_ToddlersExpansion] Failed to patch BePlayedWith job safety: {ex.Message}");
			}
		}

		private static void MakeBabyPlayJob_Postfix(Pawn feeder, ref Job __result)
		{
			if (!ShouldLogVerbose() || !IsBePlayedWithJob(__result))
			{
				return;
			}

			Log.Message($"[RimTalk_ToddlersExpansion] BePlayedWith job created: adult={DescribePawn(feeder)} job={DescribeJob(__result)}");
		}

		private static void PlayWithBaby_JobOnThing_Postfix(Pawn pawn, Thing t, bool forced, Job __result)
		{
			if (!ShouldLogVerbose() || forced || !(t is Pawn baby))
			{
				return;
			}

			if (__result == null)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] PlayWithBaby yielded no job: adult={DescribePawn(pawn)} baby={DescribePawn(baby)} babyCurJob={baby.CurJobDef?.defName ?? "null"} babyDriver={baby.jobs?.curDriver?.GetType().Name ?? "null"}");
			}
			else if (IsBePlayedWithJob(__result))
			{
				Log.Message($"[RimTalk_ToddlersExpansion] PlayWithBaby job selected: adult={DescribePawn(pawn)} baby={DescribePawn(baby)} job={DescribeJob(__result)}");
			}
		}

		private static void Job_GetCachedDriver_Prefix(Job __instance, Pawn driverPawn)
		{
			if (!IsBePlayedWithJob(__instance) || driverPawn == null || _jobCachedDriverField == null)
			{
				return;
			}

			if (!(_jobCachedDriverField.GetValue(__instance) is JobDriver cachedDriver) || cachedDriver.pawn == null || cachedDriver.pawn == driverPawn)
			{
				return;
			}

			Log.Warning($"[RimTalk_ToddlersExpansion] BePlayedWith cached driver mismatch detected. Resetting cached driver. job={DescribeJob(__instance)} firstPawn={DescribePawn(cachedDriver.pawn)} secondPawn={DescribePawn(driverPawn)}");
			_jobCachedDriverField.SetValue(__instance, null);
		}

		private static void PawnJobTracker_StartJob_Prefix(ref Job newJob, bool fromQueue, Pawn ___pawn)
		{
			if (!EnsureUniqueBePlayedWithJob(ref newJob, ___pawn, fromQueue ? "StartJob(fromQueue)" : "StartJob"))
			{
				return;
			}

			if (ShouldLogVerbose())
			{
				Log.Message($"[RimTalk_ToddlersExpansion] BePlayedWith start: toddler={DescribePawn(___pawn)} adult={DescribePawn(GetAdult(newJob))} curJob={___pawn?.CurJobDef?.defName ?? "null"} newJob={DescribeJob(newJob)}");
			}
		}

		private static void PawnJobTracker_TryTakeOrderedJob_Prefix(ref Job job, bool requestQueueing, Pawn ___pawn)
		{
			if (!EnsureUniqueBePlayedWithJob(ref job, ___pawn, requestQueueing ? "TryTakeOrderedJob(queue)" : "TryTakeOrderedJob"))
			{
				return;
			}

			if (ShouldLogVerbose())
			{
				Log.Message($"[RimTalk_ToddlersExpansion] BePlayedWith ordered: toddler={DescribePawn(___pawn)} adult={DescribePawn(GetAdult(job))} requestQueueing={requestQueueing} curJob={___pawn?.CurJobDef?.defName ?? "null"} job={DescribeJob(job)}");
			}
		}

		private static bool EnsureUniqueBePlayedWithJob(ref Job job, Pawn pawn, string context)
		{
			if (!IsBePlayedWithJob(job) || pawn == null)
			{
				return false;
			}

			Pawn adult = GetAdult(job);
			JobDriver cachedDriver = _jobCachedDriverField?.GetValue(job) as JobDriver;
			bool needsClone = false;
			string reason = null;

			if (cachedDriver?.pawn != null && cachedDriver.pawn != pawn)
			{
				needsClone = true;
				reason = $"cachedDriver belongs to {DescribePawn(cachedDriver.pawn)}";
			}
			else if (job.startTick > 0)
			{
				needsClone = true;
				reason = $"job.startTick={job.startTick}";
			}

			if (needsClone)
			{
				Job replacement = JobMaker.MakeJob(job.def, adult);
				replacement.count = job.count;
				replacement.playerForced = job.playerForced;
				replacement.expiryInterval = job.expiryInterval;
				replacement.checkOverrideOnExpire = job.checkOverrideOnExpire;
				replacement.ignoreForbidden = job.ignoreForbidden;
				replacement.ignoreDesignations = job.ignoreDesignations;
				replacement.reportStringOverride = job.reportStringOverride;
				job = replacement;

				Log.Warning($"[RimTalk_ToddlersExpansion] Replaced reused BePlayedWith job before {context}. toddler={DescribePawn(pawn)} adult={DescribePawn(adult)} reason={reason}");
			}

			return true;
		}

		private static bool IsBePlayedWithJob(Job job)
		{
			if (job?.def == null)
			{
				return false;
			}

			_bePlayedWithDef ??= DefDatabase<JobDef>.GetNamedSilentFail("BePlayedWith");
			return job.def == _bePlayedWithDef || string.Equals(job.def.defName, "BePlayedWith", StringComparison.Ordinal);
		}

		private static Pawn GetAdult(Job job)
		{
			if (job == null)
			{
				return null;
			}

			LocalTargetInfo target = job.GetTarget(TargetIndex.A);
			return target.Thing as Pawn;
		}

		private static string DescribeJob(Job job)
		{
			if (job?.def == null)
			{
				return "null";
			}

			return $"{job.def.defName}@{job.GetHashCode():X8}";
		}

		private static string DescribePawn(Pawn pawn)
		{
			if (pawn == null)
			{
				return "null";
			}

			return $"{pawn.LabelShort}#{pawn.thingIDNumber}";
		}

		private static bool ShouldLogVerbose()
		{
			return Prefs.DevMode;
		}
	}
}
