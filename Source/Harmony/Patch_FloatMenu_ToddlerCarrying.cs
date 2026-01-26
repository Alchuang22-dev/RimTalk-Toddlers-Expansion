using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	/// <summary>
	/// 添加幼儿背负相关的右键菜单选项
	/// </summary>
	public static class Patch_FloatMenu_ToddlerCarrying
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo target = AccessTools.Method(typeof(FloatMenuMakerMap), "AddHumanlikeOrders",
				new[] { typeof(Vector3), typeof(Pawn), typeof(List<FloatMenuOption>) });
			if (target == null)
			{
				return;
			}

			MethodInfo postfix = AccessTools.Method(typeof(Patch_FloatMenu_ToddlerCarrying), nameof(AddHumanlikeOrders_Postfix));
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
		}

		private static void AddHumanlikeOrders_Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
		{
			if (pawn?.Map == null || opts == null || pawn.Downed || pawn.InMentalState)
			{
				return;
			}

			// 只有玩家控制的殖民者可以使用
			if (pawn.Faction != Faction.OfPlayer)
			{
				return;
			}

			IntVec3 cell = IntVec3.FromVector3(clickPos);
			if (!cell.InBounds(pawn.Map))
			{
				return;
			}

			// 检查是否正在抱着幼儿 - 添加放下选项
			if (ToddlerCarryingUtility.IsCarryingToddler(pawn))
			{
				AddPutDownOptions(pawn, opts);
			}

			// 检查点击位置是否有可以抱起的幼儿
			AddPickUpOptions(pawn, cell, opts);
		}

		/// <summary>
		/// 添加放下幼儿的菜单选项
		/// </summary>
		private static void AddPutDownOptions(Pawn carrier, List<FloatMenuOption> opts)
		{
			List<Pawn> carriedToddlers = ToddlerCarryingUtility.GetCarriedToddlers(carrier);
			
			foreach (Pawn toddler in carriedToddlers)
			{
				string label = "RimTalk_PutDownToddler".Translate(toddler.LabelShort);
				
				// 创建放下选项
				opts.Add(new FloatMenuOption(label, () =>
				{
					ToddlerCarryingUtility.DismountToddler(toddler);
				}));
			}
		}

		/// <summary>
		/// 添加抱起幼儿的菜单选项
		/// </summary>
		private static void AddPickUpOptions(Pawn carrier, IntVec3 cell, List<FloatMenuOption> opts)
		{
			// 检查是否已经抱着幼儿（容量已满）
			if (ToddlerCarryingUtility.GetCarriedToddlerCount(carrier) >= ToddlerCarryingUtility.GetMaxCarryCapacity(carrier))
			{
				return;
			}

			// 检查是否是有效的载体
			if (!ToddlerCarryingUtility.IsValidCarrier(carrier))
			{
				return;
			}

			List<Thing> things = cell.GetThingList(carrier.Map);
			for (int i = 0; i < things.Count; i++)
			{
				if (things[i] is not Pawn targetPawn)
				{
					continue;
				}

				// 检查是否可以被抱起
				if (!ToddlerCarryingUtility.CanBeCarried(targetPawn))
				{
					continue;
				}

				// 不能抱起敌对派系的幼儿
				if (targetPawn.Faction != null && targetPawn.Faction.HostileTo(carrier.Faction))
				{
					continue;
				}

				// 检查能否到达
				if (!carrier.CanReach(targetPawn, PathEndMode.Touch, Danger.Some))
				{
					string failLabel = "RimTalk_PickUpToddler".Translate(targetPawn.LabelShort);
					opts.Add(new FloatMenuOption(failLabel + " (" + "CannotReach".Translate() + ")", null));
					continue;
				}

				string label = "RimTalk_PickUpToddler".Translate(targetPawn.LabelShort);
				
				// 创建抱起选项
				Pawn toddlerCopy = targetPawn; // 避免闭包问题
				opts.Add(new FloatMenuOption(label, () =>
				{
					Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_PickUpToddler, toddlerCopy);
					carrier.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				}));
			}
		}
	}
}