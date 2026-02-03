using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Harmony
{
	/// <summary>
	/// Patch LordToil_ExitMapAndEscortCarriers.UpdateDutyForChattelOrGuard
	/// 确保被背着的幼儿不会获得不合适的 duty（如 ExitMapBestAndDefendSelf）
	/// 而是跟随背着他们的载体
	/// </summary>
	public static class Patch_ExitMapDuty
	{
		private static Type _lordToilExitMapAndEscortCarriersType;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			// Patch LordToil_ExitMapAndEscortCarriers.UpdateDutyForChattelOrGuard
			_lordToilExitMapAndEscortCarriersType = AccessTools.TypeByName("RimWorld.LordToil_ExitMapAndEscortCarriers");
			if (_lordToilExitMapAndEscortCarriersType != null)
			{
				var updateDutyMethod = AccessTools.Method(_lordToilExitMapAndEscortCarriersType, "UpdateDutyForChattelOrGuard");
				if (updateDutyMethod != null)
				{
					harmony.Patch(updateDutyMethod,
						prefix: new HarmonyMethod(typeof(Patch_ExitMapDuty), nameof(UpdateDutyForChattelOrGuard_Prefix)));
				}
			}
		}

		/// <summary>
		/// 在 UpdateDutyForChattelOrGuard 之前拦截
		/// 如果 pawn 是被背着的幼儿，让他跟随载体而不是执行原方法的逻辑
		/// </summary>
		private static bool UpdateDutyForChattelOrGuard_Prefix(Pawn p, Pawn trader)
		{
			try
			{
				// 检查是否是被背着的幼儿
				if (!ToddlerCarryingUtility.IsBeingCarried(p))
				{
					return true; // 不是被背着的，继续执行原方法
				}

				// 获取载体
				Pawn carrier = ToddlerCarryingUtility.GetCarrier(p);
				if (carrier != null)
				{
					// 被背着的幼儿应该跟随载体
					p.mindState.duty = new PawnDuty(DutyDefOf.Follow, carrier, 5f);
					return false; // 跳过原方法
				}

				// 没有载体，继续执行原方法
				return true;
			}
			catch (Exception ex)
			{
				Log.Error($"[RimTalk_ToddlersExpansion] UpdateDutyForChattelOrGuard_Prefix 出错: {ex}");
				return true; // 出错时继续执行原方法
			}
		}
	}
}