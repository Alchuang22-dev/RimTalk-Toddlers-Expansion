using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class JobDriver_WatchToddlerPlay : JobDriver
	{
		private const TargetIndex ToddlerInd = TargetIndex.A;
		private const TargetIndex WatchSpotInd = TargetIndex.B;
		private const int MaxWatchDistance = 6;

		private Pawn Toddler => TargetA.Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (TargetB.IsValid)
			{
				return pawn.Reserve(TargetB, job, 1, -1, null, errorOnFailed);
			}

			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(ToddlerInd);
			this.FailOn(() => Toddler == null || Toddler.Downed || !Toddler.Spawned);
			this.FailOn(() => !ToddlersCompatUtility.IsEngagedInToddlerPlay(Toddler));

			if (TargetB.IsValid)
			{
				yield return Toils_Goto.GotoCell(WatchSpotInd, PathEndMode.OnCell);
			}

			Toil watch = ToilMaker.MakeToil("WatchToddlerPlay");
			watch.tickIntervalAction = delta =>
			{
				if (Toddler == null || !Toddler.Spawned)
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				if (!pawn.Position.InHorDistOf(Toddler.Position, MaxWatchDistance) || !ToddlersCompatUtility.IsEngagedInToddlerPlay(Toddler))
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				pawn.rotationTracker.FaceCell(Toddler.Position);
				pawn.GainComfortFromCellIfPossible(delta);
				JoyUtility.JoyTickCheckEnd(pawn, delta, joySource: null);
				SocialNeedTuning_Toddlers.ApplyWatchPlayTickEffects(pawn, Toddler, delta);
			};
			watch.handlingFacing = true;
			watch.defaultCompleteMode = ToilCompleteMode.Delay;
			watch.defaultDuration = job.def.joyDuration;

			AddFinishAction(condition =>
			{
				if (condition == JobCondition.Succeeded)
				{
					ToddlerPlayDialogueEvents.OnAdultWatchToddlerPlay(pawn, Toddler, job, Map);
				}
			});

			yield return watch;
		}
	}
}
