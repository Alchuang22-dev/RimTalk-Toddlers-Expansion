using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 管理幼儿玩耍时的视觉效果，包括玩具盒动画和咯咯笑动画
	/// </summary>
	public static class ToddlerPlayEffectUtility
	{
		private const int ToysCount = 5;
		private const float ToyDistanceFactor = 0.5f;
		private static readonly FloatRange ToyRandomAngleOffset = new FloatRange(-5f, 5f);

		private static Mote[] _toyMotes;
		private static int _lastGiggleTick;
		private const int GiggleIntervalMin = 250; // 最小咯咯笑间隔
		private const int GiggleIntervalMax = 750; // 最大咯咯笑间隔
		private const float GiggleChance = 0.3f; // 咯咯笑触发概率

		/// <summary>
		/// 初始化玩具Motes数组
		/// </summary>
		public static void InitializeToyMotes()
		{
			if (_toyMotes == null || _toyMotes.Length != ToysCount)
			{
				_toyMotes = new Mote[ToysCount];
			}
		}

		/// <summary>
		/// 维持玩具盒动画效果（围绕幼儿漂浮的彩色玩具）
		/// </summary>
		/// <param name="pawn">正在玩耍的幼儿</param>
		/// <param name="map">当前地图</param>
		public static void MaintainToyBoxEffect(Pawn pawn, Map map)
		{
			if (pawn == null || map == null || pawn.Map != map)
			{
				return;
			}

			InitializeToyMotes();

			// 如果是第一次或Motes已消失，重新创建
			if (_toyMotes[0] == null || _toyMotes[0].Destroyed)
			{
				CreateToyMotes(pawn, map);
			}

			// 维持所有玩具Motes
			for (int i = 0; i < _toyMotes.Length; i++)
			{
				if (_toyMotes[i] != null && !_toyMotes[i].Destroyed)
				{
					_toyMotes[i].Maintain();
				}
			}
		}

		/// <summary>
		/// 创建玩具Motes
		/// </summary>
		private static void CreateToyMotes(Pawn pawn, Map map)
		{
			Vector3 centerPos = pawn.TrueCenter();
			Vector3 baseDirection = IntVec3.North.ToVector3();
			float angleStep = 72f; // 360度/5个玩具 = 72度

			for (int i = 0; i < ToysCount; i++)
			{
				float angle = angleStep * i + ToyRandomAngleOffset.RandomInRange;
				Vector3 offset = baseDirection.RotatedBy(angle) * ToyDistanceFactor;
				Vector3 position = centerPos + offset;

				_toyMotes[i] = MoteMaker.MakeStaticMote(
					position,
					map,
					ThingDefOf.Mote_Toy,
					1f,
					false
				);
			}
		}

		/// <summary>
		/// 触发咯咯笑动画效果（彩色曲线飘动）
		/// </summary>
		/// <param name="pawn">正在玩耍的幼儿</param>
		/// <returns>是否成功触发了效果</returns>
		public static bool TryTriggerGigglingEffect(Pawn pawn)
		{
			if (pawn == null || pawn.Map == null)
			{
				return false;
			}

			// 检查冷却时间
			int currentTick = Find.TickManager.TicksGame;
			if (currentTick - _lastGiggleTick < GiggleIntervalMin)
			{
				return false;
			}

			// 随机决定是否触发
			if (Rand.Range(0f, 1f) > GiggleChance) // 根据概率决定是否触发
			{
				return false;
			}

			// 创建咯咯笑效果
			try
			{
				FleckDef fleckDef = DefDatabase<FleckDef>.GetNamed("FleckBabyGiggling", false);
				if (fleckDef != null)
				{
					FleckMaker.Static(pawn.Position, pawn.Map, fleckDef);
					_lastGiggleTick = currentTick;
					return true;
				}
				else
				{
					Log.WarningOnce("[RimTalk] FleckBabyGiggling definition not found", 123456);
					return false;
				}
			}
			catch (Exception ex)
			{
				Log.WarningOnce($"[RimTalk] Failed to create giggling effect: {ex.Message}", 123457);
				return false;
			}
		}

		/// <summary>
		/// 尝试同时触发两种效果
		/// </summary>
		public static void ApplyPlayEffects(Pawn pawn, Map map)
		{
			MaintainToyBoxEffect(pawn, map);
			TryTriggerGigglingEffect(pawn);
		}

		/// <summary>
		/// 清理效果资源
		/// </summary>
		public static void ClearEffects()
		{
			_toyMotes = null;
		}
	}
}
