using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Harmony
{
	/// <summary>
	/// 修复Hospitality mod兼容性问题：
	/// Hospitality的访客生成逻辑会截断我们注入的toddler pawns，
	/// 导致来访的幼儿没有婴儿食品。
	///
	/// 此补丁在toddler实际spawn到地图后：
	/// 1. 检查并补充婴儿食品
	/// 2. 尝试为未被背负的幼儿分配背负关系
	/// </summary>
	public static class Patch_VisitorToddlerBabyFood
	{
		private const float BabyFoodBaseUnits = 5f;
		private const float BabyFoodUnitsPerToddlerAgeYear = 2f;
		private const float MinBabyFoodUnits = 3f;
		private const float MaxBabyFoodUnits = 15f;

		// 缓存已经处理过的pawn，避免重复添加食品
		private static HashSet<int> _processedPawnIds = new HashSet<int>();
		// 缓存已经处理过背负关系的pawn
		private static HashSet<int> _processedCarryingIds = new HashSet<int>();
		private static int _lastCleanupTick = 0;
		private const int CleanupInterval = 60000; // 每小时清理一次缓存

		public static void Init(HarmonyLib.Harmony harmony)
		{
			// Patch GenSpawn.Spawn 来捕获所有pawn的spawn
			MethodInfo spawnMethod = AccessTools.Method(typeof(GenSpawn), "Spawn", 
				new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) });
			
			if (spawnMethod != null)
			{
				MethodInfo postfix = AccessTools.Method(typeof(Patch_VisitorToddlerBabyFood), nameof(GenSpawn_Spawn_Postfix));
				harmony.Patch(spawnMethod, postfix: new HarmonyMethod(postfix));
				
				if (Prefs.DevMode)
				{
					Log.Message("[RimTalk_ToddlersExpansion] Patched GenSpawn.Spawn for visitor toddler baby food injection");
				}
			}
			else
			{
				Log.Warning("[RimTalk_ToddlersExpansion] Failed to find GenSpawn.Spawn method for patching");
			}
		}

		private static void GenSpawn_Spawn_Postfix(Thing __result)
		{
			// 快速类型检查 - 大部分spawn的东西不是Pawn
			if (__result == null || !(__result is Pawn pawn))
			{
				return;
			}

			// 快速排除 - 大部分pawn不是baby/toddler阶段
			// 这个检查非常轻量，避免后续更重的检查
			if (!pawn.RaceProps?.Humanlike == true)
			{
				return;
			}

			// 检查发育阶段 - 快速排除成年人
			DevelopmentalStage stage = pawn.DevelopmentalStage;
			if (stage != DevelopmentalStage.Baby && stage != DevelopmentalStage.Newborn && stage != DevelopmentalStage.Child)
			{
				// 如果不是婴儿/幼儿/儿童阶段，检查Toddlers mod的判断
				if (!ToddlersCompatUtility.IsToddlerOrBaby(pawn))
				{
					return;
				}
			}

			// 检查是否已经处理过（在更重的检查之前）
			if (_processedPawnIds.Contains(pawn.thingIDNumber))
			{
				return;
			}

			// 清理过期的缓存
			CleanupCacheIfNeeded();

			// 检查是否是访客幼儿（包含更多条件检查）
			if (!IsVisitorToddler(pawn))
			{
				return;
			}

			// 标记为已处理
			_processedPawnIds.Add(pawn.thingIDNumber);

			// 检查并补充婴儿食品
			TryEnsureBabyFood(pawn);
			ToddlerPawnGenerationUtility.EnsureToddlerFallbackApparel(pawn, pawn.MapHeld?.Tile ?? -1);

			// 延后处理背负关系（需要等其他成员也spawn完成）
			// 使用延迟调用，在下一帧处理背负关系
			TryScheduleCarryingAssignment(pawn);
		}

		/// <summary>
		/// 尝试安排背负关系分配
		/// 使用延迟机制，确保同一Lord的所有成员都已spawn
		/// </summary>
		private static void TryScheduleCarryingAssignment(Pawn toddler)
		{
			if (toddler == null || _processedCarryingIds.Contains(toddler.thingIDNumber))
			{
				return;
			}

			// 标记为已处理背负关系
			_processedCarryingIds.Add(toddler.thingIDNumber);

			// 如果已经被背着，不需要处理
			if (ToddlerCarryingUtility.IsBeingCarried(toddler))
			{
				return;
			}

			// 延迟60 ticks（约1秒）后处理，确保同一Lord的所有成员都已spawn
			// 使用LongEventHandler来避免在spawn过程中修改状态
			LongEventHandler.QueueLongEvent(() =>
			{
				TryAssignCarryingForToddler(toddler);
			}, null, false, null);

			if (Prefs.DevMode)
			{
				Log.Message($"[RimTalk_ToddlersExpansion][CarryDebug] Scheduled carry assignment for {toddler.LabelShort} ({toddler.thingIDNumber}).");
			}
		}

		/// <summary>
		/// 尝试为单个幼儿分配背负关系
		/// </summary>
		private static void TryAssignCarryingForToddler(Pawn toddler)
		{
			if (toddler == null || toddler.Dead || toddler.Destroyed || !toddler.Spawned)
			{
				return;
			}

			// 如果已经被背着，不需要处理
			if (ToddlerCarryingUtility.IsBeingCarried(toddler))
			{
				return;
			}

			// 检查是否仍然是访客幼儿
			if (!IsVisitorToddler(toddler))
			{
				return;
			}

			// 获取幼儿所属的Lord
			Lord lord = toddler.GetLord();
			if (lord == null)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion][CarryDebug] Skip carry assignment for {toddler.LabelShort}: lord is null.");
				}
				return;
			}

			if (lord.CurLordToil == null)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion][CarryDebug] Skip carry assignment for {toddler.LabelShort}: lord {lord.loadID} CurLordToil is null.");
				}
				return;
			}

			// 在Lord成员中找到可以背负幼儿的成年人
			List<Pawn> lordMembers = lord.ownedPawns;
			if (lordMembers == null || lordMembers.Count == 0)
			{
				return;
			}

			// 找到一个可用的carrier
			foreach (Pawn potentialCarrier in lordMembers)
			{
				if (potentialCarrier == null || potentialCarrier == toddler)
				{
					continue;
				}

				if (!ToddlerCarryingUtility.IsValidCarrier(potentialCarrier))
				{
					continue;
				}

				if (!potentialCarrier.Spawned || potentialCarrier.MapHeld != toddler.MapHeld)
				{
					if (Prefs.DevMode)
					{
						Log.Message($"[RimTalk_ToddlersExpansion][CarryDebug] Ignore carrier {potentialCarrier.LabelShort} for {toddler.LabelShort}: not spawned or different map.");
					}
					continue;
				}

				// 检查这个carrier是否还有容量
				if (ToddlerCarryingUtility.GetCarriedToddlerCount(potentialCarrier) >= ToddlerCarryingUtility.GetMaxCarryCapacity(potentialCarrier))
				{
					continue;
				}

				// 尝试分配背负关系
				if (ToddlerCarryingUtility.TryMountToddler(potentialCarrier, toddler))
				{
					if (Prefs.DevMode)
					{
						Log.Message($"[RimTalk_ToddlersExpansion] 延后分配背负: {potentialCarrier.Name} 背起访客幼儿 {toddler.Name}");
					}
					return;
				}

				if (Prefs.DevMode)
				{
					Log.Message($"[RimTalk_ToddlersExpansion][CarryDebug] TryMountToddler failed: carrier={potentialCarrier.LabelShort}, toddler={toddler.LabelShort}");
				}
			}

			if (Prefs.DevMode)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] 无法为访客幼儿 {toddler.Name} 找到可用的carrier");
			}
		}

		private static bool IsVisitorToddler(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || pawn.Destroyed)
			{
				return false;
			}

			// 必须是幼儿或婴儿
			if (!ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return false;
			}

			// 必须是非玩家派系
			Faction faction = pawn.Faction;
			if (faction == null || faction == Faction.OfPlayer)
			{
				return false;
			}

			// 不能是敌对派系
			if (faction.HostileTo(Faction.OfPlayer))
			{
				return false;
			}

			// 不能是囚犯
			if (pawn.IsPrisoner || pawn.IsPrisonerOfColony)
			{
				return false;
			}

			// 必须在玩家的地图上（确保是来访的幼儿，而不是在世界地图上的幼儿）
			Map map = pawn.Map;
			if (map == null || !map.IsPlayerHome)
			{
				return false;
			}

			return true;
		}

		private static void TryEnsureBabyFood(Pawn toddler)
		{
			if (toddler == null || toddler.inventory == null)
			{
				return;
			}

			// 检查是否已经有婴儿食品
			int existingFoodCount = GetExistingBabyFoodCount(toddler);
			if (existingFoodCount >= MinBabyFoodUnits)
			{
				if (Prefs.DevMode)
				{
					Log.Message($"[RimTalk_ToddlersExpansion] 访客幼儿 {toddler.Name} 已有 {existingFoodCount} 个婴儿食品，无需补充");
				}
				return;
			}

			// 计算需要的婴儿食品数量
			float ageYears = toddler.ageTracker?.AgeBiologicalYearsFloat ?? 1f;
			float targetFoodUnits = BabyFoodBaseUnits + (ageYears * BabyFoodUnitsPerToddlerAgeYear);
			targetFoodUnits = Mathf.Clamp(targetFoodUnits, MinBabyFoodUnits, MaxBabyFoodUnits);

			int targetCount = Mathf.RoundToInt(targetFoodUnits);
			int toAdd = targetCount - existingFoodCount;

			if (toAdd <= 0)
			{
				return;
			}

			try
			{
				Thing babyFood = ThingMaker.MakeThing(ThingDefOf.BabyFood);
				if (babyFood != null)
				{
					babyFood.stackCount = toAdd;
					toddler.inventory.innerContainer.TryAdd(babyFood);

					if (Prefs.DevMode)
					{
						Log.Message($"[RimTalk_ToddlersExpansion] 为访客幼儿 {toddler.Name} 补充了 {toAdd} 个婴儿食品 (原有: {existingFoodCount}, 目标: {targetCount})");
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warning($"[RimTalk_ToddlersExpansion] 为访客幼儿补充婴儿食品失败: {ex.Message}");
			}
		}

		private static int GetExistingBabyFoodCount(Pawn pawn)
		{
			if (pawn?.inventory?.innerContainer == null)
			{
				return 0;
			}

			int count = 0;
			foreach (Thing thing in pawn.inventory.innerContainer)
			{
				if (thing.def == ThingDefOf.BabyFood)
				{
					count += thing.stackCount;
				}
			}

			return count;
		}

		private static void CleanupCacheIfNeeded()
		{
			int currentTick = Find.TickManager?.TicksGame ?? 0;
			if (currentTick - _lastCleanupTick < CleanupInterval)
			{
				return;
			}

			_lastCleanupTick = currentTick;
			
			// 清理缓存中不再存在的pawn IDs
			// 为了简单起见，我们只在缓存过大时清空
			if (_processedPawnIds.Count > 1000 || _processedCarryingIds.Count > 1000)
			{
				_processedPawnIds.Clear();
				_processedCarryingIds.Clear();
				
				if (Prefs.DevMode)
				{
					Log.Message("[RimTalk_ToddlersExpansion] 清理访客幼儿处理缓存");
				}
			}
		}

		/// <summary>
		/// 在游戏加载或新游戏开始时清理缓存
		/// </summary>
		public static void ClearCache()
		{
			_processedPawnIds.Clear();
			_processedCarryingIds.Clear();
			_lastCleanupTick = 0;
		}

		/// <summary>
		/// 清理无效的背负关系（carrier不存在或已离开地图）
		/// 这可以在GameComponent的tick中定期调用
		/// </summary>
		public static void CleanupInvalidCarryingRelations()
		{
			// 这个功能由ToddlerCarryingTracker.CleanupInvalidEntries处理
			// 这里只是提供一个入口点
		}
	}
}
