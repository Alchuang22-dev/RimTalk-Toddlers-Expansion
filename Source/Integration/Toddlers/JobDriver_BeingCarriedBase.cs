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
				// 停止幼儿的移动
				StopMovement();
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

				// 每tick确保幼儿不会尝试移动
				StopMovement();
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

		/// <summary>
		/// 停止幼儿的移动，确保不会尝试寻路
		/// </summary>
		private void StopMovement()
		{
			if (pawn?.pather == null)
			{
				return;
			}

			// 如果pather正在移动，立即停止
			if (pawn.pather.Moving)
			{
				pawn.pather.StopDead();
			}
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
