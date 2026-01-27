using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.YayoAnimation
{
	/// <summary>
	/// Yayo's Animation 兼容性工具类。
	/// 用于在抱着幼儿玩耍时禁用/重置 Yayo 对幼儿的动画效果，避免冲突。
	/// </summary>
	public static class YayoAnimationCompatUtility
	{
		private static bool _initialized = false;
		private static bool _yayoAnimationLoaded = false;
		
		// 需要禁用 Yayo 动画的 pawn 集合（在转圈动画期间）
		private static readonly HashSet<Pawn> _suppressedPawns = new HashSet<Pawn>();
		
		// Yayo's Animation 的相关类型和方法
		private static Type _animationCoreType = null;
		private static MethodInfo _checkAniMethod = null;
		
		// PawnDataUtility.GetData 方法
		private static MethodInfo _getDataMethod = null;
		
		// PawnDrawData 类型和相关字段/方法
		private static Type _pawnDrawDataType = null;
		private static MethodInfo _resetMethod = null;
		private static FieldInfo _fixedRotField = null;

		/// <summary>
		/// 初始化兼容层
		/// </summary>
		public static void Initialize()
		{
			if (_initialized)
			{
				return;
			}
			_initialized = true;

			try
			{
				// 查找 Yayo's Animation 程序集
				Assembly yayoAssembly = null;
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					if (assembly.GetName().Name == "yayoAni")
					{
						yayoAssembly = assembly;
						break;
					}
				}

				if (yayoAssembly == null)
				{
					Log.Message("[RimTalk_ToddlersExpansion] Yayo's Animation not found, skipping compatibility setup");
					return;
				}

				// 获取 AnimationCore 类型
				_animationCoreType = yayoAssembly.GetType("YayoAnimation.AnimationCore");
				if (_animationCoreType == null)
				{
					Log.Warning("[RimTalk_ToddlersExpansion] Could not find YayoAnimation.AnimationCore");
					return;
				}

				// 获取 CheckAni 方法
				_checkAniMethod = AccessTools.Method(_animationCoreType, "CheckAni");
				if (_checkAniMethod == null)
				{
					Log.Warning("[RimTalk_ToddlersExpansion] Could not find AnimationCore.CheckAni method");
					return;
				}

				// 获取 PawnDataUtility 类型
				Type pawnDataUtilityType = yayoAssembly.GetType("YayoAnimation.Data.PawnDataUtility");
				if (pawnDataUtilityType != null)
				{
					_getDataMethod = AccessTools.Method(pawnDataUtilityType, "GetData", new Type[] { typeof(Pawn) });
				}

				// 获取 PawnDrawData 类型
				_pawnDrawDataType = yayoAssembly.GetType("YayoAnimation.Data.PawnDrawData");
				if (_pawnDrawDataType != null)
				{
					_resetMethod = AccessTools.Method(_pawnDrawDataType, "Reset");
					_fixedRotField = AccessTools.Field(_pawnDrawDataType, "fixedRot");
				}

				_yayoAnimationLoaded = true;
				Log.Message("[RimTalk_ToddlersExpansion] Yayo's Animation compatibility initialized successfully");
			}
			catch (Exception ex)
			{
				Log.Error($"[RimTalk_ToddlersExpansion] Error initializing Yayo's Animation compatibility: {ex}");
			}
		}

		/// <summary>
		/// 应用 Harmony 补丁来处理 Yayo 动画兼容性
		/// </summary>
		public static void ApplyPatches(HarmonyLib.Harmony harmony)
		{
			if (!IsYayoAnimationLoaded || _checkAniMethod == null)
			{
				return;
			}

			try
			{
				// Patch AnimationCore.CheckAni 的 Prefix
				harmony.Patch(
					_checkAniMethod,
					prefix: new HarmonyMethod(typeof(YayoAnimationCompatUtility), nameof(CheckAni_Prefix))
				);
				Log.Message("[RimTalk_ToddlersExpansion] Applied Yayo's Animation CheckAni patch");
			}
			catch (Exception ex)
			{
				Log.Error($"[RimTalk_ToddlersExpansion] Error patching Yayo's Animation: {ex}");
			}
		}

		/// <summary>
		/// CheckAni 的 Prefix 补丁 - 处理被抱幼儿的朝向同步和动画抑制
		/// </summary>
		private static bool CheckAni_Prefix(Pawn pawn, Rot4 rot, object pdd)
		{
			// 检查这个pawn是否被抱着
			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (carrier != null)
			{
				// 被抱着的幼儿：重置动画并设置朝向为载体朝向
				try
				{
					if (pdd != null)
					{
						// 重置 Yayo 的动画数据
						if (_resetMethod != null)
						{
							_resetMethod.Invoke(pdd, null);
						}
						
						// 设置 fixedRot 为载体的朝向
						if (_fixedRotField != null)
						{
							// fixedRot 是 Rot4? 类型
							_fixedRotField.SetValue(pdd, carrier.Rotation);
						}
					}
				}
				catch
				{
					// 忽略错误
				}
				return false; // 跳过原方法
			}
			
			// 如果这个 pawn 在我们的抑制列表中（用于其他动画抑制场景）
			if (_suppressedPawns.Contains(pawn))
			{
				// 重置 Yayo 的动画数据
				try
				{
					if (_resetMethod != null && pdd != null)
					{
						_resetMethod.Invoke(pdd, null);
					}
				}
				catch
				{
					// 忽略错误
				}
				return false; // 跳过原方法
			}
			
			return true; // 继续执行原方法
		}

		/// <summary>
		/// 检查 Yayo's Animation 是否已加载
		/// </summary>
		public static bool IsYayoAnimationLoaded
		{
			get
			{
				if (!_initialized)
				{
					Initialize();
				}
				return _yayoAnimationLoaded;
			}
		}

		/// <summary>
		/// 开始抑制指定 pawn 的 Yayo 动画
		/// </summary>
		public static void StartSuppression(Pawn pawn)
		{
			if (pawn != null)
			{
				_suppressedPawns.Add(pawn);
			}
		}

		/// <summary>
		/// 停止抑制指定 pawn 的 Yayo 动画
		/// </summary>
		public static void StopSuppression(Pawn pawn)
		{
			if (pawn != null)
			{
				_suppressedPawns.Remove(pawn);
			}
		}

		/// <summary>
		/// 检查是否正在抑制指定 pawn 的 Yayo 动画
		/// </summary>
		public static bool IsSuppressed(Pawn pawn)
		{
			return pawn != null && _suppressedPawns.Contains(pawn);
		}

		/// <summary>
		/// 清除所有抑制
		/// </summary>
		public static void ClearAllSuppressions()
		{
			_suppressedPawns.Clear();
		}

		/// <summary>
		/// 重置 pawn 的 Yayo 动画数据（使其不受 Yayo 动画影响）
		/// </summary>
		/// <param name="pawn">要重置的 pawn</param>
		[Obsolete("Use StartSuppression instead")]
		public static void ResetPawnAnimation(Pawn pawn)
		{
			// 这个方法不再使用，保留以兼容旧代码
		}

		/// <summary>
		/// 设置 pawn 的固定朝向（用于让幼儿跟随载体朝向）
		/// </summary>
		/// <param name="pawn">要设置的 pawn</param>
		/// <param name="rotation">固定朝向</param>
		[Obsolete("Use StartSuppression instead")]
		public static void SetFixedRotation(Pawn pawn, Rot4 rotation)
		{
			// 这个方法不再使用，改用 StartSuppression
			StartSuppression(pawn);
		}

		/// <summary>
		/// 禁止 Yayo 对指定 pawn 的动画更新（设置 nextUpdateTick 为未来）
		/// </summary>
		/// <param name="pawn">要禁止的 pawn</param>
		/// <param name="durationTicks">禁止持续时间（ticks）</param>
		[Obsolete("Use StartSuppression instead")]
		public static void SuppressAnimation(Pawn pawn, int durationTicks = 60)
		{
			// 这个方法不再使用，改用 StartSuppression
			StartSuppression(pawn);
		}
	}
}