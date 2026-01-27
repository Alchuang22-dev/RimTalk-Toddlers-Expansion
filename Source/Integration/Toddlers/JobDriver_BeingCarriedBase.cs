using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public abstract class JobDriver_BeingCarriedBase : JobDriver
	{
		private const int EffectInterval = 60;

		protected virtual string ReportKey => "RimTalk_BeingCarriedBy";

		protected virtual void OnStart()
		{
		}

		protected virtual void TickEffects(int ticks)
		{
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		public override string GetReport()
		{
			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (carrier != null)
			{
				return ReportKey.Translate(carrier.LabelShort);
			}

			return base.GetReport();
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			Toil waitToil = new Toil();
			waitToil.initAction = () =>
			{
				SyncRotation();
				OnStart();
			};
			waitToil.tickAction = () =>
			{
				if (!ToddlerCarryingUtility.IsBeingCarried(pawn))
				{
					EndJobWith(JobCondition.Succeeded);
					return;
				}

				SyncRotation();

				if (pawn.IsHashIntervalTick(EffectInterval))
				{
					TickEffects(EffectInterval);
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

		private void SyncRotation()
		{
			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (carrier != null)
			{
				pawn.Rotation = carrier.Rotation;
			}
		}
	}
}
