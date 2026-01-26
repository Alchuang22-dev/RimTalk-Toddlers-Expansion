using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 殖民者去抱起幼儿的JobDriver
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
			this.FailOnDowned(TargetIndex.A);

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
					// 成功，结束Job
					EndJobWith(JobCondition.Succeeded);
				}
				else
				{
					// 失败
					EndJobWith(JobCondition.Incompletable);
				}
			};
			pickUpToil.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return pickUpToil;
		}
	}
}