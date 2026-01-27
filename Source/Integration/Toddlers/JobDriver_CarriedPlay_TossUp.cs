using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 飞高高 - 成年人抱着幼儿上下抛的JobDriver
	/// </summary>
	public class JobDriver_CarriedPlay_TossUp : JobDriver
	{
		/// <summary>
		/// 要玩耍的幼儿
		/// </summary>
		private Pawn Toddler => TargetA.Pawn;

		/// <summary>
		/// 动画持续时间（ticks）
		/// </summary>
		private const int AnimationDuration = 180;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			// 不需要预留，幼儿已经被抱着
			return true;
		}

		public override string GetReport()
		{
			if (Toddler != null)
			{
				return "RimTalk_TossingUpToddler".Translate(Toddler.LabelShort);
			}
			return base.GetReport();
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			// 验证幼儿仍然被抱着
			this.FailOn(() => !ToddlerCarryingUtility.IsCarryingToddler(pawn));
			this.FailOn(() => Toddler == null || Toddler.Dead || Toddler.Destroyed);

			// 播放飞高高动画
			Toil playToil = new Toil();
			playToil.initAction = () =>
			{
				// 停止移动，站在原地
				pawn.pather.StopDead();
				// 开始动画
				CarriedPlayAnimationTracker.StartTossUpAnimation(pawn, Toddler);
			};
			playToil.tickAction = () =>
			{
				// 更新动画
				CarriedPlayAnimationTracker.UpdateAnimation(pawn, Toddler);
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
				CarriedPlayUtility.ApplyTossUpEffects(pawn, Toddler);
			};
			effectToil.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return effectToil;
		}
	}
}
