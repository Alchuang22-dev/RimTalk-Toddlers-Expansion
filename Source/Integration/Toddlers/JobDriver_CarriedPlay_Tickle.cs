using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 逗弄幼儿 - 成年人逗幼儿笑的JobDriver
	/// 不使用动画，直接触发幼儿的咯咯笑精神状态并触发对话
	/// </summary>
	public class JobDriver_CarriedPlay_Tickle : JobDriver
	{
		/// <summary>
		/// 要玩耍的幼儿
		/// </summary>
		private Pawn Toddler => TargetA.Pawn;

		/// <summary>
		/// 缓存的幼儿引用（用于确保效果应用时引用有效）
		/// </summary>
		private Pawn cachedToddler;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			// 不需要预留，幼儿已经被抱着
			return true;
		}

		public override string GetReport()
		{
			if (Toddler != null)
			{
				return "RimTalk_TicklingToddler".Translate(Toddler.LabelShort);
			}
			return base.GetReport();
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			// 缓存幼儿引用
			cachedToddler = Toddler;
			
			// 验证幼儿仍然被抱着
			this.FailOn(() => !ToddlerCarryingUtility.IsCarryingToddler(pawn));
			this.FailOn(() => cachedToddler == null || cachedToddler.Dead || cachedToddler.Destroyed);

			// 直接应用效果（无动画）
			Toil effectToil = new Toil();
			effectToil.initAction = () =>
			{
				// 停止移动
				pawn.pather.StopDead();
				
				if (cachedToddler != null && !cachedToddler.Dead && !cachedToddler.Destroyed)
				{
					// 触发幼儿的咯咯笑精神状态
					TryStartGiggling(cachedToddler);
					
					// 应用其他效果（心情、对话等）
					CarriedPlayUtility.ApplyTickleEffects(pawn, cachedToddler);
				}
			};
			effectToil.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return effectToil;
		}

		/// <summary>
		/// 尝试让幼儿进入咯咯笑精神状态
		/// </summary>
		private void TryStartGiggling(Pawn toddler)
		{
			if (toddler?.mindState?.mentalStateHandler == null)
			{
				return;
			}

			// 使用原版的 Giggling 精神状态
			MentalStateDef gigglingDef = DefDatabase<MentalStateDef>.GetNamedSilentFail("Giggling");
			if (gigglingDef == null)
			{
				if (Prefs.DevMode)
				{
					Log.Warning("[RimTalk_ToddlersExpansion] Could not find Giggling MentalStateDef");
				}
				return;
			}

			// 尝试进入咯咯笑状态
			if (toddler.mindState.mentalStateHandler.TryStartMentalState(gigglingDef, null, false, false, false, null, false, false))
			{
				if (Prefs.DevMode)
				{
					Log.Message($"[RimTalk_ToddlersExpansion] {toddler.Name} started giggling after being tickled!");
				}
			}
		}
	}
}