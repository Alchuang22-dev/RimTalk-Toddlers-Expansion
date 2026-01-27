using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimTalk_ToddlersExpansion.UI;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	/// <summary>
	/// 幼儿背负系统的Harmony补丁。
	/// 处理被背幼儿的位置同步和渲染偏移。
	/// </summary>
	public static class Patch_ToddlerCarrying
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			// Patch Pawn.DrawPos getter - 修改被背幼儿的渲染位置
			PropertyInfo drawPosProp = AccessTools.Property(typeof(Pawn), "DrawPos");
			if (drawPosProp != null)
			{
				MethodInfo getter = drawPosProp.GetGetMethod();
				if (getter != null)
				{
					harmony.Patch(getter, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(DrawPos_Postfix)));
				}
			}

			// Patch Pawn.Tick - 同步被背幼儿的位置
			MethodInfo tickMethod = AccessTools.Method(typeof(Pawn), "Tick");
			if (tickMethod != null)
			{
				harmony.Patch(tickMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(Pawn_Tick_Postfix)));
			}

			// Patch Pawn.DeSpawn - 清除背负关系
			MethodInfo despawnMethod = AccessTools.Method(typeof(Pawn), "DeSpawn");
			if (despawnMethod != null)
			{
				harmony.Patch(despawnMethod, prefix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(Pawn_DeSpawn_Prefix)));
			}

			// Patch Pawn.Kill - 清除背负关系
			MethodInfo killMethod = AccessTools.Method(typeof(Pawn), "Kill");
			if (killMethod != null)
			{
				harmony.Patch(killMethod, prefix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(Pawn_Kill_Prefix)));
			}

			// Patch PawnRenderer.RenderPawnAt - 调整被背幼儿的渲染
			MethodInfo renderMethod = AccessTools.Method(typeof(PawnRenderer), "RenderPawnAt");
			if (renderMethod != null)
			{
				harmony.Patch(renderMethod, prefix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(RenderPawnAt_Prefix)));
			}

			// Patch JobDriver.GetReport - 修改载体的Job报告以显示"抱着{幼儿名字}"
			MethodInfo getReportMethod = AccessTools.Method(typeof(JobDriver), "GetReport");
			if (getReportMethod != null)
			{
				harmony.Patch(getReportMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(JobDriver_GetReport_Postfix)));
			}

			// Patch Pawn.GetGizmos - 添加抱着幼儿玩耍的Gizmo按钮
			MethodInfo getGizmosMethod = AccessTools.Method(typeof(Pawn), "GetGizmos");
			if (getGizmosMethod != null)
			{
				harmony.Patch(getGizmosMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(Pawn_GetGizmos_Postfix)));
			}

			Log.Message("[RimTalk_ToddlersExpansion] Toddler carrying patches initialized");
		}

		/// <summary>
		/// 修改被背幼儿的渲染位置，使其显示在载体身上
		/// </summary>
		private static void DrawPos_Postfix(Pawn __instance, ref Vector3 __result)
		{
			if (__instance == null)
			{
				return;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(__instance);
			if (carrier == null || !carrier.Spawned)
			{
				return;
			}

			// 获取载体的渲染位置和朝向
			Vector3 carrierPos = carrier.DrawPos;
			Rot4 carrierRotation = carrier.Rotation;

			// 计算偏移
			Vector3 offset = ToddlerCarryingUtility.GetCarryOffset(carrierRotation);

			// 设置幼儿的渲染位置
			__result = carrierPos + offset;
		}

		/// <summary>
		/// 每tick同步被背幼儿的位置到载体位置
		/// </summary>
		private static void Pawn_Tick_Postfix(Pawn __instance)
		{
			if (__instance == null || !__instance.Spawned)
			{
				return;
			}

			// 如果这个pawn正在被背着，同步位置
			Pawn carrier = ToddlerCarryingUtility.GetCarrier(__instance);
			if (carrier != null && carrier.Spawned && carrier.Map == __instance.Map)
			{
				// 同步位置到载体位置
				if (__instance.Position != carrier.Position)
				{
					// 使用内部方法直接设置位置，避免触发移动相关逻辑
					try
					{
						__instance.Position = carrier.Position;
					}
					catch
					{
						// 忽略位置设置失败
					}
				}
			}

			// 如果这个pawn是载体，检查被背的幼儿
			if (ToddlerCarryingUtility.IsCarryingToddler(__instance))
			{
				var toddlers = ToddlerCarryingUtility.GetCarriedToddlers(__instance);
				for (int i = 0; i < toddlers.Count; i++)
				{
					Pawn toddler = toddlers[i];
					if (toddler == null || toddler.Dead || toddler.Destroyed || !toddler.Spawned)
					{
						ToddlerCarryingUtility.DismountToddler(toddler);
						continue;
					}

					// 确保幼儿在同一地图
					if (toddler.Map != __instance.Map)
					{
						ToddlerCarryingUtility.DismountToddler(toddler);
					}
				}
			}
		}

		/// <summary>
		/// pawn被移除前清除背负关系
		/// </summary>
		private static void Pawn_DeSpawn_Prefix(Pawn __instance)
		{
			if (__instance == null)
			{
				return;
			}

			ToddlerCarryingUtility.ClearAllCarryingRelations(__instance);
		}

		/// <summary>
		/// pawn死亡前清除背负关系
		/// </summary>
		private static void Pawn_Kill_Prefix(Pawn __instance)
		{
			if (__instance == null)
			{
				return;
			}

			ToddlerCarryingUtility.ClearAllCarryingRelations(__instance);
		}

		/// <summary>
		/// 调整被背幼儿的渲染参数
		/// </summary>
		private static bool RenderPawnAt_Prefix(PawnRenderer __instance, Vector3 drawLoc, Rot4? rotOverride, bool neverAimWeapon)
		{
			// 获取渲染器对应的pawn
			Pawn pawn = GetPawnFromRenderer(__instance);
			if (pawn == null)
			{
				return true;
			}

			// 检查是否被背着
			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (carrier == null)
			{
				return true;
			}

			// 被背着的幼儿使用载体的朝向
			// 这个prefix返回true继续执行原方法，但已经通过DrawPos修改了位置
			return true;
		}

		/// <summary>
		/// 从PawnRenderer获取对应的Pawn
		/// </summary>
		private static Pawn GetPawnFromRenderer(PawnRenderer renderer)
		{
			if (renderer == null)
			{
				return null;
			}

			// 尝试通过反射获取pawn字段
			try
			{
				FieldInfo pawnField = AccessTools.Field(typeof(PawnRenderer), "pawn");
				if (pawnField != null)
				{
					return pawnField.GetValue(renderer) as Pawn;
				}
			}
			catch
			{
				// 忽略反射失败
			}

			return null;
		}

		/// <summary>
		/// 修改载体的Job报告，在末尾附加"抱着幼儿名字"
		/// </summary>
		private static void JobDriver_GetReport_Postfix(JobDriver __instance, ref string __result)
		{
			if (__instance == null || string.IsNullOrEmpty(__result))
			{
				return;
			}

			Pawn pawn = __instance.pawn;
			if (pawn == null)
			{
				return;
			}

			// 检查是否正在背着幼儿
			if (!ToddlerCarryingUtility.IsCarryingToddler(pawn))
			{
				return;
			}

			List<Pawn> carriedToddlers = ToddlerCarryingUtility.GetCarriedToddlers(pawn);
			if (carriedToddlers.Count == 0)
			{
				return;
			}

			// 构建幼儿名字列表
			string toddlerNames = string.Join(", ", carriedToddlers.Select(t => t.LabelShort));
			string carryingText = "RimTalk_CarryingToddler".Translate(toddlerNames);

			// 在原有报告后附加背负信息，使用破折号连接
			__result = $"{__result} - {carryingText}";
		}

		/// <summary>
		/// 为抱着幼儿的pawn添加玩耍Gizmo按钮
		/// </summary>
		private static IEnumerable<Gizmo> Pawn_GetGizmos_Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
		{
			// 先返回原有的所有Gizmo
			foreach (Gizmo gizmo in __result)
			{
				yield return gizmo;
			}

			// 如果pawn正在抱着幼儿，添加玩耍按钮
			if (__instance != null && ToddlerCarryingUtility.IsCarryingToddler(__instance))
			{
				foreach (Gizmo playGizmo in Gizmo_CarriedPlay.GetCarriedPlayGizmos(__instance))
				{
					yield return playGizmo;
				}
			}
		}
	}
}