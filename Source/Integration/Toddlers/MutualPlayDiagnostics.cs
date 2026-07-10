using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	internal static class MutualPlayDiagnostics
	{
		private const int SearchFailureLogInterval = 300;
		private static readonly Dictionary<int, int> NextSearchFailureLogTick = new Dictionary<int, int>();

		public static bool Enabled => ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs;

		public static bool IsMutualPlayJob(Job job)
		{
			return job?.def == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob
				|| job?.def == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayPartnerJob;
		}

		public static void Log(Pawn pawn, string phase, string detail, Pawn other = null)
		{
			if (!Enabled)
			{
				return;
			}

			string otherState = other == null ? string.Empty : $" other=[{DescribePawn(other)}]";
			Verse.Log.Message(
				$"[RimTalk_ToddlersExpansion][MutualPlay][{phase}] {detail} " +
				$"pawn=[{DescribePawn(pawn)}]{otherState}");
		}

		public static void LogSearchFailure(Pawn pawn, string detail)
		{
			if (!Enabled || pawn == null)
			{
				return;
			}

			int now = Find.TickManager?.TicksGame ?? 0;
			int id = pawn.thingIDNumber;
			if (NextSearchFailureLogTick.TryGetValue(id, out int nextTick) && now < nextTick)
			{
				return;
			}

			NextSearchFailureLogTick[id] = now + SearchFailureLogInterval;
			if (NextSearchFailureLogTick.Count > 2048)
			{
				NextSearchFailureLogTick.Clear();
			}

			Log(pawn, "SearchRejected", detail);
		}

		public static string DescribePawn(Pawn pawn)
		{
			if (pawn == null)
			{
				return "null";
			}

			string label = pawn.LabelShort ?? "unnamed";
			string position = pawn.Spawned ? pawn.Position.ToString() : "unspawned";
			string map = pawn.MapHeld?.uniqueID.ToString() ?? "null";
			string play = pawn.needs?.play != null
				? pawn.needs.play.CurLevelPercentage.ToString("0.000")
				: pawn.needs?.joy?.CurLevelPercentage.ToString("0.000") ?? "null";
			string mental = pawn.MentalStateDef?.defName ?? "none";
			string job = DescribeJob(pawn.CurJob);
			string driver = pawn.jobs?.curDriver?.GetType().Name ?? "null";

			return $"{label}#{pawn.thingIDNumber} faction={pawn.Faction?.def?.defName ?? "null"} " +
				$"map={map} pos={position} stage={pawn.DevelopmentalStage} play={play} " +
				$"awake={SafeAwake(pawn)} downed={pawn.Downed} drafted={pawn.Drafted} mental={mental} " +
				$"job={job} driver={driver}";
		}

		public static string DescribeJob(Job job)
		{
			if (job == null)
			{
				return "null";
			}

			return $"{job.def?.defName ?? "null"}(id={job.loadID},A={DescribeTarget(job.targetA)},B={DescribeTarget(job.targetB)})";
		}

		private static string DescribeTarget(LocalTargetInfo target)
		{
			if (!target.IsValid)
			{
				return "invalid";
			}

			if (target.Thing is Pawn pawn)
			{
				return $"{pawn.LabelShort ?? "unnamed"}#{pawn.thingIDNumber}";
			}

			return target.Thing != null ? target.Thing.LabelShort : target.Cell.ToString();
		}

		private static bool SafeAwake(Pawn pawn)
		{
			try
			{
				return pawn.Awake();
			}
			catch
			{
				return false;
			}
		}
	}
}
