using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;
using Verse.AI.Group;
using RimWorld;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_TravelingLord
	{
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
		}

		private static void Lord_Postfix(Lord __instance, LordJob lordJob)
		{
			// 检查是否是需要处理的旅行类 Lord
			if (!IsTravelingLord(lordJob))
				return;

			// 为所有的 toddlers 设置监护人
			for (int i = 0; i < __instance.ownedPawns.Count; i++)
			{
				Pawn pawn = __instance.ownedPawns[i];
				if (ToddlersCompatUtility.IsToddler(pawn) || pawn.DevelopmentalStage == DevelopmentalStage.Child)
				{
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
				Pawn guardian = FindGuardian(__instance, p);
				if (guardian != null)
				{
					TryMakeToddlerFollow(p, guardian);
				}
			}
		}

		private static void RemovePawn_Postfix(Lord __instance, Pawn p)
		{
			// 当 Pawn 离开 Lord 时（比如商队解散）
			// 检查是否有 toddlers 失去了监护人
			if (__instance.ownedPawns.NullOrEmpty())
				return;

			// 如果被移除的是成年人，检查是否有需要重新分配监护人的 toddlers
			if (!(ToddlersCompatUtility.IsToddler(p) || p.DevelopmentalStage == DevelopmentalStage.Child))
			{
				TryReassignGuardians(__instance, p);
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
	}
}
