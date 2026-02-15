using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	/// <summary>
	/// 处理幼儿洗澡时的渲染：
	/// 1. 脱衣服效果（移除衣服和头饰渲染）
	/// 2. 调整浴缸中的位置使幼儿"泡在水里"
	/// </summary>
	public static class Patch_ToddlerBathRendering
	{
		private static HediffDef _washingHediff;
		private static bool _hediffChecked;
		private static readonly FieldInfo _pawnRendererPawnField = AccessTools.Field(typeof(PawnRenderer), "pawn");

		public static void Init(HarmonyLib.Harmony harmony)
		{
			// Patch PawnRenderer.GetDrawParms - 脱衣服效果
			MethodInfo getDrawParms = AccessTools.Method(typeof(PawnRenderer), "GetDrawParms");
			if (getDrawParms != null)
			{
				harmony.Patch(getDrawParms, postfix: new HarmonyMethod(typeof(Patch_ToddlerBathRendering), nameof(GetDrawParms_Postfix)));
			}

			// Patch Pawn.DrawPos getter - 调整浴缸中的渲染位置
			PropertyInfo drawPosProp = AccessTools.Property(typeof(Pawn), "DrawPos");
			if (drawPosProp != null)
			{
				MethodInfo getter = drawPosProp.GetGetMethod();
				if (getter != null)
				{
					harmony.Patch(getter, postfix: new HarmonyMethod(typeof(Patch_ToddlerBathRendering), nameof(DrawPos_Postfix)));
				}
			}

			// Patch PawnRenderer.BodyAngle - 调整身体角度
			MethodInfo bodyAngle = AccessTools.Method(typeof(PawnRenderer), "BodyAngle");
			if (bodyAngle != null)
			{
				harmony.Patch(bodyAngle, postfix: new HarmonyMethod(typeof(Patch_ToddlerBathRendering), nameof(BodyAngle_Postfix)));
			}

			Log.Message("[RimTalk Toddlers] Bath rendering patches initialized");
		}

		/// <summary>
		/// 使幼儿在洗澡时不渲染衣服和头饰（裸体效果）
		/// </summary>
		private static void GetDrawParms_Postfix(PawnRenderer __instance, ref PawnDrawParms __result)
		{
			Pawn pawn = GetPawnFromRenderer(__instance);
			if (pawn == null)
			{
				return;
			}

			// 检查是否是幼儿在执行洗澡任务且有 Washing hediff
			if (!IsToddlerBathing(pawn))
			{
				return;
			}

			// 移除衣服和头饰渲染标志
			__result.flags &= ~PawnRenderFlags.Headgear;
			__result.flags &= ~PawnRenderFlags.Clothes;
		}

		/// <summary>
		/// 调整幼儿在浴缸中的渲染位置，使其"泡在水里"
		/// </summary>
		private static void DrawPos_Postfix(Pawn __instance, ref Vector3 __result)
		{
			if (__instance == null)
			{
				return;
			}

			// 检查是否是幼儿在浴缸中洗澡
			if (__instance.CurJobDef != ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfBath)
			{
				return;
			}

			if (!ToddlersCompatUtility.IsToddler(__instance))
			{
				return;
			}

			Thing bath = __instance.jobs?.curJob?.GetTarget(TargetIndex.A).Thing;
			if (bath == null || !ToddlerSelfBathUtility.IsBath(bath))
			{
				return;
			}

			// 只有当幼儿确实在浴缸位置时才调整（已经进入浴缸后）
			if (__instance.Position != bath.Position && !bath.OccupiedRect().Contains(__instance.Position))
			{
				return;
			}

			// DBH 的水面层 Y 偏移是 1.843f（WaterOffset）
			// 我们需要将幼儿的渲染 Y 位置调低一些，使水面能够覆盖身体
			// 但不要调太低，否则头也会被覆盖
			// 正常 pawn 的 DrawPos.y 基于 Altitude，约为 1.5f 左右
			// 我们将幼儿下沉一点点，让水面层能正确覆盖
			
			// 计算目标 Y 位置：浴缸位置的 Y + 一个小偏移使身体在水下
			float bathBaseY = bath.DrawPos.y;
			// 幼儿应该在一个较低的层级，这样水面（y + 1.843）可以覆盖身体
			// 但头部仍然露出
			__result.y = bathBaseY + 0.1f;
		}

		/// <summary>
		/// 调整幼儿在浴缸中的身体角度，使其与浴缸方向匹配
		/// </summary>
		private static void BodyAngle_Postfix(PawnRenderer __instance, ref float __result)
		{
			Pawn pawn = GetPawnFromRenderer(__instance);
			if (pawn == null)
			{
				return;
			}

			if (!IsToddlerBathing(pawn))
			{
				return;
			}

			Thing bath = pawn.jobs?.curJob?.GetTarget(TargetIndex.A).Thing;
			if (bath == null || !ToddlerSelfBathUtility.IsBath(bath))
			{
				return;
			}

			// 根据浴缸的朝向设置身体角度
			Rot4 rotation = bath.Rotation;
			rotation.AsInt += 2; // 旋转180度使头朝向正确
			__result = rotation.AsAngle;
		}

		/// <summary>
		/// 检查 pawn 是否是正在洗澡的幼儿
		/// </summary>
		private static bool IsToddlerBathing(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			// 检查是否在执行幼儿洗澡任务
			if (pawn.CurJobDef != ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfBath)
			{
				return false;
			}

			// 检查是否有 Washing hediff
			if (!HasWashingHediff(pawn))
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// 检查 pawn 是否有 Washing hediff
		/// </summary>
		private static bool HasWashingHediff(Pawn pawn)
		{
			if (pawn?.health?.hediffSet == null)
			{
				return false;
			}

			EnsureWashingHediffLoaded();
			if (_washingHediff == null)
			{
				return false;
			}

			return pawn.health.hediffSet.HasHediff(_washingHediff);
		}

		/// <summary>
		/// 确保 Washing HediffDef 已加载
		/// </summary>
		private static void EnsureWashingHediffLoaded()
		{
			if (_hediffChecked)
			{
				return;
			}

			_hediffChecked = true;
			_washingHediff = DefDatabase<HediffDef>.GetNamedSilentFail("Washing");
		}

		/// <summary>
		/// 从 PawnRenderer 获取对应的 Pawn
		/// </summary>
		private static Pawn GetPawnFromRenderer(PawnRenderer renderer)
		{
			if (renderer == null || _pawnRendererPawnField == null)
			{
				return null;
			}

			return _pawnRendererPawnField.GetValue(renderer) as Pawn;
		}
	}
}