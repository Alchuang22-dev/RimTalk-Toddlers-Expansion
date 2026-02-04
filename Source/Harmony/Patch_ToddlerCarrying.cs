using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimTalk_ToddlersExpansion.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

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

			// Patch Pawn_HealthTracker.MakeDowned - 成人倒地时清除背负关系
			MethodInfo makeDownedMethod = AccessTools.Method(typeof(Pawn_HealthTracker), "MakeDowned");
			if (makeDownedMethod != null)
			{
				harmony.Patch(makeDownedMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(HealthTracker_MakeDowned_Postfix)));
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

			// Patch PawnRenderer.BodyAngle - 修复被抱婴儿的渲染角度（让婴儿直立而非横躺）
			MethodInfo bodyAngleMethod = AccessTools.Method(typeof(PawnRenderer), "BodyAngle");
			if (bodyAngleMethod != null)
			{
				harmony.Patch(bodyAngleMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(BodyAngle_Postfix)));
			}

			// Patch PawnRenderer.LayingFacing - 修复被抱婴儿的朝向
			MethodInfo layingFacingMethod = AccessTools.Method(typeof(PawnRenderer), "LayingFacing");
			if (layingFacingMethod != null)
			{
				harmony.Patch(layingFacingMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(LayingFacing_Postfix)));
			}

			// Patch Pawn.CarriedBy getter - 让原版渲染逻辑识别我们的背负系统
			// 原版代码在渲染时检查 pawn.CarriedBy 来判断是否被携带
			// 如果 CarriedBy 返回正确的载体，所有原版渲染逻辑（角度、朝向、层级）都会自动正确处理
			PropertyInfo carriedByProp = AccessTools.Property(typeof(Pawn), "CarriedBy");
			if (carriedByProp != null)
			{
				MethodInfo getter = carriedByProp.GetGetMethod();
				if (getter != null)
				{
					harmony.Patch(getter, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(CarriedBy_Postfix)));
				}
			}

			// Patch ChildcareUtility.FindUnsafeBaby - 阻止"送到安全位置"工作处理正在被背负的婴儿
			Type childcareUtilityType = AccessTools.TypeByName("RimWorld.ChildcareUtility");
			if (childcareUtilityType != null)
			{
				MethodInfo findUnsafeBabyMethod = AccessTools.Method(childcareUtilityType, "FindUnsafeBaby");
				if (findUnsafeBabyMethod != null)
				{
					harmony.Patch(findUnsafeBabyMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(FindUnsafeBaby_Postfix)));
				}
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

			// 计算基础偏移
			Vector3 offset = ToddlerCarryingUtility.GetCarryOffset(carrierRotation);

			// 添加动画偏移（飞高高、逗弄、转圈时的额外位移）
			Vector3 animOffset = CarriedPlayAnimationTracker.GetAnimationOffset(carrier, __instance);

			// 设置幼儿的渲染位置（基础偏移 + 动画偏移）
			__result = carrierPos + offset + animOffset;
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

			// Keep carried protection hediff in sync during runtime and after load.
			bool isBeingCarried = ToddlerCarryingUtility.IsBeingCarried(__instance);
			if (isBeingCarried)
			{
				ToddlerCarryProtectionUtility.SetCarryProtectionActive(__instance, true);
			}
			else if (ToddlerCarryProtectionUtility.HasCarryProtection(__instance))
			{
				ToddlerCarryProtectionUtility.SetCarryProtectionActive(__instance, false);
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

			TryExitCarriedToddlersWithCarrier(__instance);
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

		private static void TryExitCarriedToddlersWithCarrier(Pawn carrier)
		{
			if (carrier == null || !carrier.Spawned || !ToddlerCarryingUtility.IsCarryingToddler(carrier))
			{
				return;
			}

			if (carrier.Faction == Faction.OfPlayer)
			{
				return;
			}

			if (!PawnUtility.IsExitingMap(carrier))
			{
				return;
			}

			Lord lord = carrier.GetLord();
			bool inExitToil = lord?.CurLordToil is LordToil_ExitMap ||
			                  lord?.CurLordToil?.GetType().Name == "LordToil_ExitMapAndEscortCarriers";
			if (!inExitToil)
			{
				return;
			}

			List<Pawn> carriedToddlers = ToddlerCarryingUtility.GetCarriedToddlers(carrier);
			for (int i = 0; i < carriedToddlers.Count; i++)
			{
				Pawn toddler = carriedToddlers[i];
				if (toddler == null || toddler.Dead || toddler.Destroyed || !toddler.Spawned || toddler.Map != carrier.Map)
				{
					continue;
				}

				if (!ToddlersCompatUtility.IsToddlerOrBaby(toddler) && toddler.DevelopmentalStage != DevelopmentalStage.Child)
				{
					continue;
				}

				if (!TryExitMapImmediately(toddler, carrier.Rotation))
				{
					TryQueueExitMapJob(toddler);
				}
			}
		}

		private static bool TryExitMapImmediately(Pawn pawn, Rot4 facing)
		{
			if (_pawnExitMapMethod == null || pawn == null)
			{
				return false;
			}

			try
			{
				_pawnExitMapMethod.Invoke(pawn, new object[] { false, facing });
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void TryQueueExitMapJob(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned)
			{
				return;
			}

			if (PawnUtility.IsExitingMap(pawn) || pawn.jobs?.curJob?.exitMapOnArrival == true)
			{
				return;
			}

			if (!RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 spot, TraverseMode.ByPawn, canBash: false))
			{
				return;
			}

			Job job = JobMaker.MakeJob(JobDefOf.Goto, spot);
			job.exitMapOnArrival = true;
			job.locomotionUrgency = LocomotionUrgency.Jog;
			pawn.jobs?.StartJob(job, JobCondition.InterruptForced);
		}

		/// <summary>
		/// pawn倒地后清除背负关系（成人倒地时放下幼儿）
		/// </summary>
		private static void HealthTracker_MakeDowned_Postfix(Pawn_HealthTracker __instance)
		{
			if (__instance == null)
			{
				return;
			}

			// pawn字段是私有的，需要通过反射获取
			Pawn pawn = _healthTrackerPawnField?.GetValue(__instance) as Pawn;
			if (pawn == null)
			{
				return;
			}

			// 清除该pawn的所有背负关系（无论是作为载体还是被背者）
			ToddlerCarryingUtility.ClearAllCarryingRelations(pawn);
		}

		// 缓存反射字段以提高性能
		private static readonly FieldInfo _healthTrackerPawnField = AccessTools.Field(typeof(Pawn_HealthTracker), "pawn");
		private static readonly MethodInfo _pawnExitMapMethod = AccessTools.Method(typeof(Pawn), "ExitMap", new[] { typeof(bool), typeof(Rot4) });

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

		/// <summary>
		/// 修复被抱婴儿的渲染角度。
		/// 婴儿默认是Downed状态，会返回wiggler.downedAngle（横躺角度）。
		/// 此补丁让被我们系统抱着的婴儿返回0度（直立）。
		/// </summary>
		private static void BodyAngle_Postfix(PawnRenderer __instance, ref float __result)
		{
			Pawn pawn = GetPawnFromRenderer(__instance);
			if (pawn == null)
			{
				return;
			}

			// 只处理婴儿（Baby阶段）
			if (!pawn.DevelopmentalStage.Baby())
			{
				return;
			}

			// 检查是否被我们的系统抱着
			if (!ToddlerCarryingUtility.IsBeingCarried(pawn))
			{
				return;
			}

			// 返回直立角度（0度），而非原本的横躺角度
			__result = 0f;
		}

		/// <summary>
		/// 修复被抱婴儿的朝向。
		/// 让被抱的婴儿面向与载体相同的方向。
		/// 只针对Baby（0-1岁），不影响Toddler（1-3岁）。
		/// </summary>
		private static void LayingFacing_Postfix(PawnRenderer __instance, ref Rot4 __result)
		{
			Pawn pawn = GetPawnFromRenderer(__instance);
			if (pawn == null)
			{
				return;
			}

			// 只处理婴儿（Baby阶段）
			if (!pawn.DevelopmentalStage.Baby())
			{
				return;
			}

			// 获取载体
			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (carrier == null)
			{
				return;
			}

			// 使用载体的朝向
			__result = carrier.Rotation;
		}

		/// <summary>
		/// 让原版的 CarriedBy 属性识别我们的背负系统。
		/// 原版代码在多处检查 pawn.CarriedBy 来判断是否被携带：
		/// - PawnRenderer.GetBodyPos() 检查 CarriedBy 决定渲染层级
		/// - 其他各种逻辑也依赖 CarriedBy
		///
		/// 通过让 CarriedBy 返回我们系统的载体，可以让原版渲染逻辑自动正确处理。
		/// </summary>
		private static void CarriedBy_Postfix(Pawn __instance, ref Pawn __result)
		{
			// 如果原版已经返回了载体（通过 Pawn_CarryTracker），不做修改
			if (__result != null)
			{
				return;
			}

			// 检查是否被我们的背负系统抱着
			Pawn carrier = ToddlerCarryingUtility.GetCarrier(__instance);
			if (carrier != null)
			{
				__result = carrier;
			}
		}

		/// <summary>
		/// 阻止"送到安全位置"工作处理正在被我们系统背负的婴儿。
		/// 当成人抱着婴儿时，不应该触发"送到安全位置"的保育工作。
		/// </summary>
		private static void FindUnsafeBaby_Postfix(Pawn mom, AutofeedMode priorityLevel, ref Pawn __result)
		{
			if (__result == null)
			{
				return;
			}

			// 如果找到的婴儿正在被我们的系统背负，返回null（视为没有不安全的婴儿）
			if (ToddlerCarryingUtility.IsBeingCarried(__result))
			{
				__result = null;
			}
		}
	}
}
