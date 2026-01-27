using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 抱着幼儿玩耍时的动画追踪器。
	/// 追踪当前正在播放的动画类型和进度。
	/// </summary>
	public static class CarriedPlayAnimationTracker
	{
		/// <summary>
		/// 动画类型枚举
		/// </summary>
		public enum CarriedPlayAnimationType
		{
			None,
			TossUp,
			SpinAround
		}

		/// <summary>
		/// 动画数据
		/// </summary>
		private class AnimationData
		{
			public CarriedPlayAnimationType Type;
			public int StartTick;
			public Pawn Carrier;
			public Pawn Toddler;
		}

		/// <summary>
		/// 当前活动的动画
		/// </summary>
		private static readonly Dictionary<Pawn, AnimationData> ActiveAnimations = new Dictionary<Pawn, AnimationData>();

		/// <summary>
		/// 开始飞高高动画
		/// </summary>
		public static void StartTossUpAnimation(Pawn carrier, Pawn toddler)
		{
			StartAnimation(carrier, toddler, CarriedPlayAnimationType.TossUp);
		}

		/// <summary>
		/// 开始转圈动画
		/// </summary>
		public static void StartSpinAroundAnimation(Pawn carrier, Pawn toddler)
		{
			StartAnimation(carrier, toddler, CarriedPlayAnimationType.SpinAround);
		}

		/// <summary>
		/// 开始动画
		/// </summary>
		private static void StartAnimation(Pawn carrier, Pawn toddler, CarriedPlayAnimationType type)
		{
			if (carrier == null || toddler == null)
			{
				return;
			}

			ActiveAnimations[carrier] = new AnimationData
			{
				Type = type,
				StartTick = Find.TickManager.TicksGame,
				Carrier = carrier,
				Toddler = toddler
			};
		}

		/// <summary>
		/// 更新动画
		/// </summary>
		public static void UpdateAnimation(Pawn carrier, Pawn toddler)
		{
			// 动画更新逻辑在GetAnimationOffset中处理
		}

		/// <summary>
		/// 停止动画
		/// </summary>
		public static void StopAnimation(Pawn carrier, Pawn toddler)
		{
			if (carrier != null)
			{
				ActiveAnimations.Remove(carrier);
			}
		}

		/// <summary>
		/// 获取当前动画类型
		/// </summary>
		public static CarriedPlayAnimationType GetCurrentAnimationType(Pawn carrier)
		{
			if (carrier == null || !ActiveAnimations.TryGetValue(carrier, out AnimationData data))
			{
				return CarriedPlayAnimationType.None;
			}

			return data.Type;
		}

		/// <summary>
		/// 检查是否有活动动画
		/// </summary>
		public static bool HasActiveAnimation(Pawn carrier)
		{
			return carrier != null && ActiveAnimations.ContainsKey(carrier);
		}

		/// <summary>
		/// 获取动画偏移量（用于渲染）
		/// </summary>
		/// <param name="carrier">载体</param>
		/// <param name="toddler">幼儿</param>
		/// <returns>额外的渲染偏移</returns>
		public static Vector3 GetAnimationOffset(Pawn carrier, Pawn toddler)
		{
			if (carrier == null || !ActiveAnimations.TryGetValue(carrier, out AnimationData data))
			{
				return Vector3.zero;
			}

			int elapsed = Find.TickManager.TicksGame - data.StartTick;
			float progress = elapsed / 60f; // 转换为秒

			switch (data.Type)
			{
				case CarriedPlayAnimationType.TossUp:
					return GetTossUpOffset(progress);
				case CarriedPlayAnimationType.SpinAround:
					return Vector3.zero; // 转圈通过改变朝向实现，不需要偏移
				default:
					return Vector3.zero;
			}
		}

		/// <summary>
		/// 获取动画旋转角度（用于转圈动画）
		/// </summary>
		/// <param name="carrier">载体</param>
		/// <returns>旋转角度</returns>
		public static float GetAnimationRotation(Pawn carrier)
		{
			if (carrier == null || !ActiveAnimations.TryGetValue(carrier, out AnimationData data))
			{
				return 0f;
			}

			if (data.Type != CarriedPlayAnimationType.SpinAround)
			{
				return 0f;
			}

			int elapsed = Find.TickManager.TicksGame - data.StartTick;
			float progress = elapsed / 60f; // 转换为秒

			// 每秒旋转360度，持续3秒
			return progress * 360f;
		}

		/// <summary>
		/// 飞高高动画偏移
		/// </summary>
		private static Vector3 GetTossUpOffset(float progress)
		{
			// 使用正弦波模拟上下抛动作
			// 每0.5秒一个周期，上下移动0.3个单位
			float height = Mathf.Sin(progress * Mathf.PI * 4f) * 0.3f;
			return new Vector3(0f, 0f, height);
		}

		/// <summary>
		/// 清除所有动画
		/// </summary>
		public static void ClearAll()
		{
			ActiveAnimations.Clear();
		}

		/// <summary>
		/// 清除指定pawn的动画
		/// </summary>
		public static void ClearPawn(Pawn pawn)
		{
			if (pawn != null)
			{
				ActiveAnimations.Remove(pawn);
			}
		}
	}
}
