using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 殖民者/访客去抱起幼儿的JobDriver
	/// 用于商队/访客离开前让成年人抱起幼儿
	/// 由 JobGiver_PickUpUncarriedToddler 分配
	/// </summary>
	public class JobDriver_PickUpToddler : JobDriver
	{
		/// <summary>
		/// 要抱起的幼儿
		/// </summary>
		private Pawn Toddler => TargetA.Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(Toddler, job, 1, -1, null, errorOnFailed);
		}

		public override string GetReport()
		{
			if (Toddler != null)
			{
				return "RimTalk_PickingUpToddler".Translate(Toddler.LabelShort);
			}
			return base.GetReport();
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			// 验证幼儿
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			// 走到幼儿身边
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

			// 抱起幼儿
			Toil pickUpToil = new Toil();
			pickUpToil.initAction = () =>
			{
				Pawn toddler = Toddler;
				if (toddler == null || toddler.Dead || toddler.Destroyed)
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				// 尝试抱起幼儿
				if (ToddlerCarryingUtility.TryMountToddler(pawn, toddler))
				{
					Log.Message($"[RimTalk_ToddlersExpansion][DEBUG] {pawn.LabelShort} 成功抱起 {toddler.LabelShort}");
					EndJobWith(JobCondition.Succeeded);
				}
				else
				{
					Log.Message($"[RimTalk_ToddlersExpansion][DEBUG] {pawn.LabelShort} 抱起 {toddler.LabelShort} 失败");
					EndJobWith(JobCondition.Incompletable);
				}
			};
			pickUpToil.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return pickUpToil;
		}
	}
}
