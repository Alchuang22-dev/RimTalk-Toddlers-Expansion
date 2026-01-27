using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 转圈 - 成年人抱着幼儿转圈的JobDriver
	/// </summary>
	public class JobDriver_CarriedPlay_SpinAround : JobDriver
	{
		/// <summary>
		/// 要玩耍的幼儿
		/// </summary>
		private Pawn Toddler => TargetA.Pawn;

		/// <summary>
		/// 动画持续时间（ticks）- 转圈需要更长时间
		/// </summary>
		private const int AnimationDuration = 240;

		/// <summary>
		/// 每隔多少tick切换一次朝向（越小转得越快）
		/// </summary>
		private const int TicksPerRotation = 15;

		/// <summary>
		/// 朝向序列：南->东->北->西->南（顺时针转圈）
		/// </summary>
		private static readonly Rot4[] RotationSequence = { Rot4.South, Rot4.East, Rot4.North, Rot4.West };

		/// <summary>
		/// 当前朝向索引
		/// </summary>
		private int currentRotationIndex = 0;

		/// <summary>
		/// 上次切换朝向的tick
		/// </summary>
		private int lastRotationTick = 0;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			// 不需要预留，幼儿已经被抱着
			return true;
		}

		public override string GetReport()
		{
			if (Toddler != null)
			{
				return "RimTalk_SpinningWithToddler".Translate(Toddler.LabelShort);
			}
			return base.GetReport();
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			// 验证幼儿仍然被抱着
			this.FailOn(() => !ToddlerCarryingUtility.IsCarryingToddler(pawn));
			this.FailOn(() => Toddler == null || Toddler.Dead || Toddler.Destroyed);

			// 播放转圈动画
			Toil playToil = new Toil();
			playToil.initAction = () =>
			{
				// 停止移动，站在原地
				pawn.pather.StopDead();
				// 开始动画
				CarriedPlayAnimationTracker.StartSpinAroundAnimation(pawn, Toddler);
				// 初始化朝向
				currentRotationIndex = 0;
				lastRotationTick = Find.TickManager.TicksGame;
				pawn.Rotation = RotationSequence[currentRotationIndex];
			};
			playToil.tickAction = () =>
			{
				// 更新动画
				CarriedPlayAnimationTracker.UpdateAnimation(pawn, Toddler);
				
				// 检查是否需要切换朝向
				int currentTick = Find.TickManager.TicksGame;
				if (currentTick - lastRotationTick >= TicksPerRotation)
				{
					// 切换到下一个朝向（顺时针）
					currentRotationIndex = (currentRotationIndex + 1) % RotationSequence.Length;
					pawn.Rotation = RotationSequence[currentRotationIndex];
					lastRotationTick = currentTick;
				}
			};
			playToil.defaultCompleteMode = ToilCompleteMode.Delay;
			playToil.defaultDuration = AnimationDuration;
			playToil.handlingFacing = true;
			playToil.AddFinishAction(() =>
			{
				// 结束动画
				CarriedPlayAnimationTracker.StopAnimation(pawn, Toddler);
			});
			yield return playToil;

			// 应用效果
			Toil effectToil = new Toil();
			effectToil.initAction = () =>
			{
				CarriedPlayUtility.ApplySpinAroundEffects(pawn, Toddler);
			};
			effectToil.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return effectToil;
		}
	}
}
