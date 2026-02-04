using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;
using Verse.AI.Group;
using RimWorld;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_TravelingLord
	{
		private static Type _lordToilExitMapAndEscortCarriersType;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			// 当一个新的 Lord (群体) 创建时
			var lordCtor = AccessTools.Constructor(typeof(Lord), new[] { typeof(LordJob), typeof(Faction), typeof(IntVec3), typeof(Quest) });
			if (lordCtor != null)
			{
				harmony.Patch(lordCtor, postfix: new HarmonyMethod(typeof(Patch_TravelingLord), nameof(Lord_Postfix)));
			}

			// 当 Pawn 加入 Lord 时
			var addPawn = AccessTools.Method(typeof(Lord), nameof(Lord.AddPawn));
			if (addPawn != null)
			{
				harmony.Patch(addPawn, postfix: new HarmonyMethod(typeof(Patch_TravelingLord), nameof(AddPawn_Postfix)));
			}

			// 当 Pawn 离开 Lord 时
			var removePawn = AccessTools.Method(typeof(Lord), nameof(Lord.RemovePawn));
			if (removePawn != null)
			{
				harmony.Patch(removePawn, postfix: new HarmonyMethod(typeof(Patch_TravelingLord), nameof(RemovePawn_Postfix)));
			}

			// 当 LordToil 更新所有 duty 时 - 修复幼儿/儿童 duty 不更新的问题
			// Patch LordToil_ExitMapAndEscortCarriers.UpdateAllDuties (商人商队)
			_lordToilExitMapAndEscortCarriersType = AccessTools.TypeByName("RimWorld.LordToil_ExitMapAndEscortCarriers");
			if (_lordToilExitMapAndEscortCarriersType != null)
			{
				var updateAllDuties = AccessTools.Method(_lordToilExitMapAndEscortCarriersType, "UpdateAllDuties");
				if (updateAllDuties != null)
				{
					harmony.Patch(updateAllDuties, postfix: new HarmonyMethod(typeof(Patch_TravelingLord), nameof(ExitMapAndEscortCarriers_UpdateAllDuties_Postfix)));
				}
			}

			// Patch LordToil_ExitMap.UpdateAllDuties (访客等)
			var lordToilExitMapType = typeof(LordToil_ExitMap);
			var exitMapUpdateAllDuties = AccessTools.Method(lordToilExitMapType, "UpdateAllDuties");
			if (exitMapUpdateAllDuties != null)
			{
				harmony.Patch(exitMapUpdateAllDuties, postfix: new HarmonyMethod(typeof(Patch_TravelingLord), nameof(LordToil_ExitMap_UpdateAllDuties_Postfix)));
			}
		}

		private static void Lord_Postfix(Lord __instance, LordJob lordJob)
			{
				// 检查是否是需要处理的旅行类 Lord
				if (!IsTravelingLord(lordJob))
					return;
	
				// 自动分配背负关系：让成年人背着幼儿
				List<Pawn> pawnList = __instance.ownedPawns.ToList();
				ToddlerCarryingUtility.AutoAssignCarryingForGroup(pawnList);
	
				// 为所有的 toddlers 设置监护人（非幼儿或未被背的）
				for (int i = 0; i < __instance.ownedPawns.Count; i++)
				{
					Pawn pawn = __instance.ownedPawns[i];
					if (ToddlersCompatUtility.IsToddler(pawn) || pawn.DevelopmentalStage == DevelopmentalStage.Child)
					{
						// 如果已经被背着，不需要跟随任务
						if (ToddlerCarryingUtility.IsBeingCarried(pawn))
						{
							continue;
						}
	
						Pawn guardian = FindGuardian(__instance, pawn);
						if (guardian != null)
						{
							// 尝试让幼儿跟随守卫者
							TryMakeToddlerFollow(pawn, guardian);
						}
					}
				}
			}

		private static void AddPawn_Postfix(Lord __instance, Pawn p)
			{
				// 当新的 toddler 或 child 加入时，确保他们有监护人
				if (ToddlersCompatUtility.IsToddler(p) || p.DevelopmentalStage == DevelopmentalStage.Child)
				{
					// 尝试让成年人背着这个幼儿
					Pawn guardian = FindGuardian(__instance, p);
					if (guardian != null)
					{
						// 优先尝试背着
						if (ToddlerCarryingUtility.TryMountToddler(guardian, p))
						{
							return;
						}
						// 如果不能背，则跟随
						TryMakeToddlerFollow(p, guardian);
					}
				}
			}

		private static void RemovePawn_Postfix(Lord __instance, Pawn p)
			{
				// 当 Pawn 离开 Lord 时（比如商队解散）
				// 清除该pawn的所有背负关系
				ToddlerCarryingUtility.ClearAllCarryingRelations(p);
				EnsureExitMapDutyForUncarriedToddlers(__instance);
	
				// 检查是否有 toddlers 失去了监护人
				if (__instance.ownedPawns.NullOrEmpty())
					return;
	
				// 如果被移除的是成年人，检查是否有需要重新分配监护人的 toddlers
				if (!(ToddlersCompatUtility.IsToddler(p) || p.DevelopmentalStage == DevelopmentalStage.Child))
				{
					TryReassignGuardians(__instance, p);
				}
			}

		private static void EnsureExitMapDutyForUncarriedToddlers(Lord lord)
		{
			if (lord == null || lord.ownedPawns.NullOrEmpty())
			{
				return;
			}

			if (lord.CurLordToil is not LordToil_ExitMap)
			{
				return;
			}

			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn pawn = lord.ownedPawns[i];
				if (pawn == null)
				{
					continue;
				}

				bool isYoungPawn = ToddlersCompatUtility.IsToddlerOrBaby(pawn) ||
				                   pawn.DevelopmentalStage == DevelopmentalStage.Child;
				if (!isYoungPawn)
				{
					continue;
				}

				if (ToddlerCarryingUtility.IsBeingCarried(pawn))
				{
					continue;
				}

				pawn.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest);
			}
		}

		private static bool IsTravelingLord(LordJob lordJob)
		{
			if (lordJob == null)
				return false;

			// 检查是否是各种旅行类任务
			return lordJob is LordJob_VisitColony ||
				   lordJob is LordJob_TradeWithColony ||
				   lordJob is LordJob_FormAndSendCaravan;
		}

		private static Pawn FindGuardian(Lord lord, Pawn toddler)
		{
			// 优先找之前的监护人
			if (lord.ownedPawns.TryRandomElement(out Pawn result))
			{
				// 尝试找一个不是 toddler 或 child 的成年人
				for (int i = 0; i < lord.ownedPawns.Count; i++)
				{
					Pawn pawn = lord.ownedPawns[i];
					if (!ToddlersCompatUtility.IsToddler(pawn) &&
					    pawn.DevelopmentalStage != DevelopmentalStage.Child &&
					    pawn.RaceProps.Humanlike)
					{
						return pawn;
					}
				}
			}

			return null;
		}

		private static void TryMakeToddlerFollow(Pawn toddler, Pawn guardian)
		{
			// 只有当 toddler 没有工作且没有跟随任务时才分配跟随
			if (toddler?.CurJob?.def == null || toddler.CurJob.def == RimWorld.JobDefOf.Goto)
			{
				var job = JobMaker.MakeJob(RimWorld.JobDefOf.Follow, guardian);
				job.expiryInterval = 120;
				toddler.jobs?.TryTakeOrderedJob(job);
			}
		}

		private static void TryReassignGuardians(Lord lord, Pawn removedGuardian)
		{
			if (lord?.ownedPawns == null)
				return;

			// 找到所有失去了监护人的 toddlers
			List<Pawn> orphanedToddlers = new List<Pawn>();
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn pawn = lord.ownedPawns[i];
				if (ToddlersCompatUtility.IsToddler(pawn) || pawn.DevelopmentalStage == DevelopmentalStage.Child)
				{
					orphanedToddlers.Add(pawn);
				}
			}

			if (orphanedToddlers.NullOrEmpty())
				return;

			// 剩下的成年人
			List<Pawn> remainingAdults = lord.ownedPawns.Where(p =>
				!ToddlersCompatUtility.IsToddler(p) &&
				p.DevelopmentalStage != DevelopmentalStage.Child &&
				p.RaceProps.Humanlike).ToList();

			if (remainingAdults.NullOrEmpty())
			{
				// 没有成年人了，强制解散或让所有人离开地图
				for (int i = 0; i < orphanedToddlers.Count; i++)
				{
					Pawn toddler = orphanedToddlers[i];
					ForcePawnLeaveMap(toddler);
				}
			}
			else
			{
				// 为每个 toddler 分配一个新的监护人
				for (int i = 0; i < orphanedToddlers.Count; i++)
				{
					Pawn toddler = orphanedToddlers[i];
					Pawn newGuardian = remainingAdults.RandomElement();
					TryMakeToddlerFollow(toddler, newGuardian);
				}
			}
		}

		private static void ForcePawnLeaveMap(Pawn pawn)
		{
			if (pawn == null || pawn.Map == null)
				return;

			// 创建离开地图的任务
			var leaveJob = JobMaker.MakeJob(RimWorld.JobDefOf.Goto, FindExitCell(pawn));
			leaveJob.exitMapOnArrival = true;
			pawn.jobs?.TryTakeOrderedJob(leaveJob);
		}

		private static IntVec3 FindExitCell(Pawn pawn)
		{
			// 找到最近的地图边缘
			if (pawn?.Map == null)
				return IntVec3.Invalid;

			// 简单地返回一个远离中心的边缘位置
			var map = pawn.Map;
			IntVec3 pawnPos = pawn.Position;

			// 如果靠近北边，去北边
			if (pawnPos.z > map.Size.z / 2)
				return new IntVec3(pawnPos.x, 0, map.Size.z - 1);

			// 否则去南边
			return new IntVec3(pawnPos.x, 0, 0);
		}

		/// <summary>
		/// 在 LordToil_ExitMapAndEscortCarriers.UpdateAllDuties 之后执行
		/// 确保幼儿/儿童也能获得正确的离开地图的 duty
		/// 注意：抱幼儿的逻辑现在由 JobGiver_PickUpUncarriedToddler 处理（通过 XML Patch 注入到 Duty 中）
		/// </summary>
		private static void ExitMapAndEscortCarriers_UpdateAllDuties_Postfix(LordToil __instance)
		{
			try
			{
				Lord lord = __instance.lord;
				if (lord == null || lord.ownedPawns.NullOrEmpty())
				{
					return;
				}

				// 找到商队领队（trader）
				Pawn trader = TraderCaravanUtility.FindTrader(lord);
				
				// 修复幼儿的 duty（确保他们有正确的 Escort duty）
				FixToddlerDuties(lord, trader);
			}
			catch (Exception ex)
			{
				Log.Error($"[RimTalk_ToddlersExpansion] ExitMapAndEscortCarriers_UpdateAllDuties_Postfix 出错: {ex}");
			}
		}

		/// <summary>
		/// 在 LordToil_ExitMap.UpdateAllDuties 之后执行
		/// 用于访客等使用 LordToil_ExitMap 的情况
		/// 注意：抱幼儿的逻辑现在由 JobGiver_PickUpUncarriedToddler 处理（通过 XML Patch 注入到 Duty 中）
		/// </summary>
		private static void LordToil_ExitMap_UpdateAllDuties_Postfix(LordToil __instance)
		{
			try
			{
				Lord lord = __instance.lord;
				if (lord == null || lord.ownedPawns.NullOrEmpty())
				{
					return;
				}

				// 修复幼儿的 duty
				FixToddlerDuties(lord, null);
			}
			catch (Exception ex)
			{
				Log.Error($"[RimTalk_ToddlersExpansion] LordToil_ExitMap_UpdateAllDuties_Postfix 出错: {ex}");
			}
		}

		/// <summary>
		/// 修复幼儿/儿童的 duty，确保他们能正确离开地图
		/// 问题原因：
		/// 1. RimWorld 原版代码会跳过某些条件下的幼儿/儿童，导致他们的 duty 保持为旧的 TravelOrLeave
		/// 2. 被背着的幼儿不应该获得 ExitMapBestAndDefendSelf 这种领头 duty，否则其他人会围着他们转
		/// 3. 如果被背着的幼儿是 trader（有 ExitMapBestAndDefendSelf duty），需要把这个 duty 转移给载体
		/// </summary>
		private static void FixToddlerDuties(Lord lord, Pawn trader)
		{
			// 收集需要成为领路人的载体（因为他们背着的幼儿是 trader）
			HashSet<Pawn> carriersNeedLeaderDuty = new HashSet<Pawn>();
			
			// 第一遍：处理被背着的幼儿，检查是否有 trader 幼儿
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn pawn = lord.ownedPawns[i];
				
				// 检查是否被背着
				bool isBeingCarried = ToddlerCarryingUtility.IsBeingCarried(pawn);
				if (!isBeingCarried)
					continue;
				
				Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
				if (carrier == null)
					continue;
				
				bool hasLeaderDuty = pawn.mindState?.duty?.def?.defName == "ExitMapBestAndDefendSelf";
				
				// 如果被背着的幼儿有领头 duty（说明他是 trader），把 duty 转移给载体
				if (hasLeaderDuty)
				{
					carriersNeedLeaderDuty.Add(carrier);
				}
				
				// 被背着的幼儿应该跟随背着他的载体
				pawn.mindState.duty = new PawnDuty(DutyDefOf.Follow, carrier, 5f);
			}
			
			// 第二遍：给需要成为领路人的载体分配 ExitMapBestAndDefendSelf duty
			foreach (Pawn carrier in carriersNeedLeaderDuty)
			{
				carrier.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBestAndDefendSelf);
				carrier.mindState.duty.radius = 18f;
				carrier.mindState.duty.locomotion = LocomotionUrgency.Jog;
			}
			
			// 第三遍：处理未被背着的幼儿
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn pawn = lord.ownedPawns[i];
				
				// 跳过已经处理的被背着的幼儿
				if (ToddlerCarryingUtility.IsBeingCarried(pawn))
					continue;
				
				// 判断是否是需要特殊处理的幼儿/婴儿/儿童
				bool isToddler = ToddlersCompatUtility.IsToddler(pawn);
				bool isBaby = pawn.DevelopmentalStage.Baby();
				bool isChild = pawn.DevelopmentalStage == DevelopmentalStage.Child;
				bool isYoung = IsYoungHuman(pawn);
				bool isYoungPawn = isToddler || isBaby || isChild || isYoung;
				
				if (!isYoungPawn)
					continue;
				
				// 只处理仍然是 TravelOrLeave 的幼儿
				if (pawn.mindState?.duty?.def != DutyDefOf.TravelOrLeave)
				{
					continue;
				}

				// 找出实际的领路人（可能是原来的 trader，也可能是背着 trader 幼儿的载体）
				Pawn actualLeader = trader;
				if (actualLeader != null && ToddlerCarryingUtility.IsBeingCarried(actualLeader))
				{
					// trader 被背着了，找到背着他的人作为领路人
					Pawn traderCarrier = ToddlerCarryingUtility.GetCarrier(actualLeader);
					if (traderCarrier != null)
					{
						actualLeader = traderCarrier;
					}
				}

				// 为未被背的幼儿/儿童分配正确的离开地图 duty
				if (actualLeader != null)
				{
					pawn.mindState.duty = new PawnDuty(DutyDefOf.Escort, actualLeader, 14f);
				}
				else
				{
					pawn.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest);
					pawn.mindState.duty.locomotion = LocomotionUrgency.Jog;
				}
			}
		}

		/// <summary>
		/// 检查是否是年幼的人类（小于13岁）
		/// </summary>
		private static bool IsYoungHuman(Pawn pawn)
		{
			if (pawn?.ageTracker == null || pawn?.RaceProps?.Humanlike != true)
				return false;
			
			float age = pawn.ageTracker.AgeBiologicalYearsFloat;
			// 小于13岁（RimWorld 的成年年龄）
			return age < 13f;
		}
	}
}
