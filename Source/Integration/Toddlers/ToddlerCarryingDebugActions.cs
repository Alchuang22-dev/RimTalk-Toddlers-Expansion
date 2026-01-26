using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using Verse;

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
	}
}