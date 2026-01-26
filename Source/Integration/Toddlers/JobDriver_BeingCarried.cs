using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 幼儿被抱着时的JobDriver。
	/// 这是一个特殊的Job，幼儿在被背着时会持续执行。
	/// </summary>
	public class JobDriver_BeingCarried : JobDriver
	{
		/// <summary>
		/// 获取载体（背着幼儿的成年人）
		/// </summary>
		private Pawn Carrier => TargetA.Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			// 不需要预留任何东西
			return true;
		}

		public override string GetReport()
		{
			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (carrier != null)
			{
				return "RimTalk_BeingCarriedBy".Translate(carrier.LabelShort);
			}

			return base.GetReport();
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			// 创建一个持续等待的Toil
			Toil waitToil = new Toil();
			waitToil.initAction = () =>
			{
				// 设置pawn朝向载体
				Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
				if (carrier != null)
				{
					pawn.Rotation = carrier.Rotation;
				}
			};
			waitToil.tickAction = () =>
			{
				// 每tick检查是否还在被背着
				if (!ToddlerCarryingUtility.IsBeingCarried(pawn))
				{
					// 不再被背着，结束Job
					EndJobWith(JobCondition.Succeeded);
					return;
				}

				// 同步朝向
				Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
				if (carrier != null)
				{
					pawn.Rotation = carrier.Rotation;
				}
			};
			waitToil.defaultCompleteMode = ToilCompleteMode.Never;
			waitToil.handlingFacing = true;

			yield return waitToil;
		}

		public override bool IsContinuation(Job j)
		{
			return j.def == job.def;
		}
	}
}