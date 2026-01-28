using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 幼儿背负系统的开发模式调试命令
	/// </summary>
	public static class ToddlerCarryingDebugActions
	{
		[DebugAction("RimTalk Toddlers", "Show carrying status", allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void ShowCarryingStatus()
		{
			List<Pawn> carriers = ToddlerCarryingTracker.GetAllCarriers();
			List<Pawn> carried = ToddlerCarryingTracker.GetAllCarriedToddlers();

			Log.Message($"[ToddlerCarrying] === 背负状态 ===");
			Log.Message($"[ToddlerCarrying] 载体数量: {carriers.Count}");
			Log.Message($"[ToddlerCarrying] 被背幼儿数量: {carried.Count}");

			foreach (Pawn carrier in carriers)
			{
				List<Pawn> toddlers = ToddlerCarryingTracker.GetCarriedToddlers(carrier);
				string toddlerNames = string.Join(", ", toddlers.ConvertAll(t => t.Name?.ToStringShort ?? "Unknown"));
				Log.Message($"[ToddlerCarrying] {carrier.Name?.ToStringShort ?? "Unknown"} 正在背着: {toddlerNames}");
			}
		}

		[DebugAction("RimTalk Toddlers", "Force mount selected toddler", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.ToolMapForPawns)]
		private static void ForceMountToddler(Pawn toddler)
		{
			if (!ToddlersCompatUtility.IsToddler(toddler))
			{
				Log.Warning($"[ToddlerCarrying] {toddler.Name} 不是幼儿");
				return;
			}

			// 找到最近的成年人
			Pawn nearestAdult = null;
			float minDist = float.MaxValue;

			foreach (Pawn pawn in toddler.Map.mapPawns.AllPawnsSpawned)
			{
				if (!ToddlerCarryingUtility.IsValidCarrier(pawn))
				{
					continue;
				}

				float dist = pawn.Position.DistanceTo(toddler.Position);
				if (dist < minDist)
				{
					minDist = dist;
					nearestAdult = pawn;
				}
			}

			if (nearestAdult == null)
			{
				Log.Warning("[ToddlerCarrying] 找不到可用的成年人");
				return;
			}

			if (ToddlerCarryingUtility.TryMountToddler(nearestAdult, toddler))
			{
				Log.Message($"[ToddlerCarrying] 成功: {nearestAdult.Name} 背起了 {toddler.Name}");
			}
			else
			{
				Log.Warning($"[ToddlerCarrying] 失败: 无法让 {nearestAdult.Name} 背起 {toddler.Name}");
			}
		}

		[DebugAction("RimTalk Toddlers", "Force dismount selected toddler", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.ToolMapForPawns)]
		private static void ForceDismountToddler(Pawn toddler)
		{
			if (!ToddlerCarryingUtility.IsBeingCarried(toddler))
			{
				Log.Warning($"[ToddlerCarrying] {toddler.Name} 没有被背着");
				return;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(toddler);
			if (ToddlerCarryingUtility.DismountToddler(toddler))
			{
				Log.Message($"[ToddlerCarrying] 成功: {toddler.Name} 从 {carrier?.Name} 身上下来了");
			}
			else
			{
				Log.Warning($"[ToddlerCarrying] 失败: 无法让 {toddler.Name} 下来");
			}
		}

		[DebugAction("RimTalk Toddlers", "Clear all carrying relations", allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void ClearAllCarryingRelations()
		{
			int count = ToddlerCarryingTracker.GetAllCarriedToddlers().Count;
			ToddlerCarryingTracker.ClearAll();
			Log.Message($"[ToddlerCarrying] 已清除 {count} 个背负关系");
		}

		[DebugAction("RimTalk Toddlers", "Test auto-assign carrying for visitors", allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void TestAutoAssignCarrying()
		{
			Map map = Find.CurrentMap;
			if (map == null)
			{
				return;
			}

			// 收集所有非玩家派系的pawn
			List<Pawn> nonPlayerPawns = new List<Pawn>();
			foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
			{
				if (pawn.Faction != null && pawn.Faction != Faction.OfPlayer && pawn.RaceProps.Humanlike)
				{
					nonPlayerPawns.Add(pawn);
				}
			}

			if (nonPlayerPawns.Count == 0)
			{
				Log.Warning("[ToddlerCarrying] 地图上没有非玩家派系的人形生物");
				return;
			}

			ToddlerCarryingUtility.AutoAssignCarryingForGroup(nonPlayerPawns);
			Log.Message($"[ToddlerCarrying] 已尝试为 {nonPlayerPawns.Count} 个非玩家pawn自动分配背负关系");
		}

		// ==================== 新野游系统 Debug Actions ====================

		[DebugAction("RimTalk Toddlers", "Force child to nature run", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.ToolMapForPawns)]
		private static void ForceChildNatureRun(Pawn child)
		{
			if (child == null)
			{
				Log.Warning("[NatureRunning] 未选择pawn");
				return;
			}

			if (!child.DevelopmentalStage.Child())
			{
				Log.Warning($"[NatureRunning] {child.LabelShort} 不是儿童");
				return;
			}

			// 创建 NatureRunning job (使用我们自己的 DefOf 引用)
			JobDef natureRunningDef = ToddlersExpansionJobDefOf.NatureRunning;
			if (natureRunningDef == null)
			{
				Log.Warning("[NatureRunning] 找不到 NatureRunning JobDef（需要 Biotech DLC）");
				return;
			}
			
			// 找一个较远的目标位置来跑向（类似原版 NatureRunning 的行为）
			IntVec3 targetPos = child.Position;
			Map map = child.Map;
			
			// 尝试找一个较远的可达位置
			if (CellFinder.TryFindRandomCellNear(child.Position, map, 30,
				cell => cell.InBounds(map) && cell.Standable(map) &&
				        !cell.IsForbidden(child) &&
				        child.CanReach(cell, PathEndMode.OnCell, Danger.Some) &&
				        cell.DistanceTo(child.Position) >= 15f,
				out IntVec3 foundPos))
			{
				targetPos = foundPos;
			}
			else if (CellFinder.TryFindRandomCellNear(child.Position, map, 20,
				cell => cell.InBounds(map) && cell.Standable(map) &&
				        child.CanReach(cell, PathEndMode.OnCell, Danger.Some),
				out IntVec3 fallbackPos))
			{
				targetPos = fallbackPos;
			}
			
			Job job = JobMaker.MakeJob(natureRunningDef, targetPos);
			job.locomotionUrgency = LocomotionUrgency.Sprint;
			
			// 结束当前 job 并强制启动新 job
			child.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
			child.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
			
			string curJobName = child.CurJob?.def?.defName ?? "null";
			Log.Message($"[NatureRunning] 已让 {child.LabelShort} 开始野游（目标: {targetPos}），当前job: {curJobName}，附近的其他儿童应该会自动跟随");
		}

		[DebugAction("RimTalk Toddlers", "Start children gathering outing", allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void StartChildrenGatheringOuting()
		{
			Map map = Find.CurrentMap;
			if (map == null)
			{
				Log.Warning("[ChildrenOuting] 没有当前地图");
				return;
			}

			// 查找 GatheringDef
			GatheringDef gatheringDef = DefDatabase<GatheringDef>.GetNamedSilentFail("RimTalk_ChildrenOuting");
			if (gatheringDef == null)
			{
				Log.Warning("[ChildrenOuting] 找不到 RimTalk_ChildrenOuting GatheringDef");
				return;
			}

			// 创建 GatheringWorker 并尝试执行
			GatheringWorker worker = (GatheringWorker)System.Activator.CreateInstance(gatheringDef.workerClass);
			worker.def = gatheringDef;

			if (worker.TryExecute(map))
			{
				Log.Message("[ChildrenOuting] 儿童野游聚会已成功启动！");
			}
			else
			{
				Log.Warning("[ChildrenOuting] 无法启动儿童野游聚会（可能没有足够的儿童或没有合适的地点）");
			}
		}

		[DebugAction("RimTalk Toddlers", "Start children outing (selected organizer)", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.ToolMapForPawns)]
		private static void StartChildrenOutingWithOrganizer(Pawn organizer)
		{
			if (organizer == null)
			{
				Log.Warning("[ChildrenOuting] 未选择pawn");
				return;
			}

			if (!organizer.DevelopmentalStage.Child())
			{
				Log.Warning($"[ChildrenOuting] {organizer.LabelShort} 不是儿童，无法作为组织者");
				return;
			}

			Map map = organizer.Map;
			if (map == null)
			{
				Log.Warning("[ChildrenOuting] pawn不在地图上");
				return;
			}

			// 查找 GatheringDef
			GatheringDef gatheringDef = DefDatabase<GatheringDef>.GetNamedSilentFail("RimTalk_ChildrenOuting");
			if (gatheringDef == null)
			{
				Log.Warning("[ChildrenOuting] 找不到 RimTalk_ChildrenOuting GatheringDef");
				return;
			}

			// 创建 GatheringWorker 并尝试执行
			GatheringWorker worker = (GatheringWorker)System.Activator.CreateInstance(gatheringDef.workerClass);
			worker.def = gatheringDef;

			if (worker.TryExecute(map, organizer))
			{
				Log.Message($"[ChildrenOuting] {organizer.LabelShort} 发起的儿童野游聚会已成功启动！");
			}
			else
			{
				Log.Warning($"[ChildrenOuting] 无法由 {organizer.LabelShort} 启动儿童野游聚会");
			}
		}

		[DebugAction("RimTalk Toddlers", "Show active lords", allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void ShowActiveLords()
		{
			Map map = Find.CurrentMap;
			if (map == null)
			{
				return;
			}

			Log.Message("[Lords] === 活动中的Lord ===");
			List<Lord> lords = map.lordManager.lords;
			
			if (lords.Count == 0)
			{
				Log.Message("[Lords] 当前没有活动中的Lord");
				return;
			}

			foreach (Lord lord in lords)
			{
				string lordJobType = lord.LordJob?.GetType().Name ?? "Unknown";
				Log.Message($"[Lords] Lord: {lordJobType}, 参与者: {lord.ownedPawns.Count}人");
				
				foreach (Pawn pawn in lord.ownedPawns)
				{
					string duty = pawn.mindState?.duty?.def?.defName ?? "none";
					Log.Message($"  - {pawn.LabelShort}: Duty={duty}");
				}
			}
		}

		[DebugAction("RimTalk Toddlers", "Show follow nature runner status", allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void ShowFollowNatureRunnerStatus()
		{
			Map map = Find.CurrentMap;
			if (map == null)
			{
				return;
			}

			Log.Message("[FollowNatureRunner] === 跟随野游状态 ===");

			int natureRunners = 0;
			int followers = 0;

			foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
			{
				if (pawn.CurJob == null)
				{
					continue;
				}

				if (pawn.CurJob.def == ToddlersExpansionJobDefOf.NatureRunning)
				{
					natureRunners++;
					Log.Message($"[FollowNatureRunner] 野游中: {pawn.LabelShort}");
				}
				else if (pawn.CurJob.def == ToddlersExpansionJobDefOf.RimTalk_FollowNatureRunner)
				{
					followers++;
					Pawn leader = pawn.CurJob.targetA.Pawn;
					string leaderName = leader?.LabelShort ?? "Unknown";
					Log.Message($"[FollowNatureRunner] 跟随中: {pawn.LabelShort} -> 跟随 {leaderName}");
				}
			}

			if (natureRunners == 0 && followers == 0)
			{
				Log.Message("[FollowNatureRunner] 当前没有野游或跟随活动");
			}
			else
			{
				Log.Message($"[FollowNatureRunner] 总计: {natureRunners}个野游者, {followers}个跟随者");
			}
		}
	}
}