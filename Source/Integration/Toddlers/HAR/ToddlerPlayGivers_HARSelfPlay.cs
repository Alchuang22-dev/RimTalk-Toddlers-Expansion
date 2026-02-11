using RimWorld;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Toddlers;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers.HAR
{
	public abstract class ToddlerPlayGiver_HarSelfPlayBase : ToddlerPlayGiver
	{
		private const float PlayNeedThreshold = 0.92f;
		private const int SearchRadius = 6;

		protected abstract HarRaceWhitelistUtility.MiliraAlignedRaceGroup TargetGroup { get; }

		public override bool CanDo(Pawn pawn)
		{
			if (!IsEligiblePawn(pawn))
			{
				return false;
			}

			Need_Play play = pawn.needs?.play;
			if (play != null && play.CurLevelPercentage >= PlayNeedThreshold)
			{
				return false;
			}

			return TryFindPlaySpot(pawn, out _);
		}

		public override Job TryGiveJob(Pawn pawn)
		{
			if (!IsEligiblePawn(pawn))
			{
				return null;
			}

			if (!TryFindPlaySpot(pawn, out IntVec3 spot))
			{
				return null;
			}

			Job job = JobMaker.MakeJob(def.jobDef, spot);
			job.ignoreJoyTimeAssignment = true;
			job.expiryInterval = 2000;
			return job;
		}

		private bool IsEligiblePawn(Pawn pawn)
		{
			if (pawn?.Map == null || pawn.needs?.play == null || !ToddlersCompatUtility.IsEligibleForSelfPlay(pawn))
			{
				return false;
			}

			if (pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
			{
				return false;
			}

			if (PawnUtility.WillSoonHaveBasicNeed(pawn, 0f))
			{
				return false;
			}

			return HarRaceWhitelistUtility.GetMiliraAlignedRaceGroup(pawn) == TargetGroup;
		}

		private static bool TryFindPlaySpot(Pawn pawn, out IntVec3 spot)
		{
			Map map = pawn.Map;
			IntVec3 root = pawn.Position;
			return CellFinder.TryFindRandomCellNear(root, map, SearchRadius, cell =>
			{
				if (!cell.Standable(map) || cell.IsForbidden(pawn))
				{
					return false;
				}

				return pawn.CanReserveSittableOrSpot(cell);
			}, out spot);
		}
	}

	public sealed class ToddlerPlayGiver_HarSelfPlay_Ratkin : ToddlerPlayGiver_HarSelfPlayBase
	{
		protected override HarRaceWhitelistUtility.MiliraAlignedRaceGroup TargetGroup => HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Ratkin;
	}

	public sealed class ToddlerPlayGiver_HarSelfPlay_Kiiro : ToddlerPlayGiver_HarSelfPlayBase
	{
		protected override HarRaceWhitelistUtility.MiliraAlignedRaceGroup TargetGroup => HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Kiiro;
	}

	public sealed class ToddlerPlayGiver_HarSelfPlay_MoeLotl : ToddlerPlayGiver_HarSelfPlayBase
	{
		protected override HarRaceWhitelistUtility.MiliraAlignedRaceGroup TargetGroup => HarRaceWhitelistUtility.MiliraAlignedRaceGroup.MoeLotl;
	}

	public sealed class ToddlerPlayGiver_HarSelfPlay_Milira : ToddlerPlayGiver_HarSelfPlayBase
	{
		protected override HarRaceWhitelistUtility.MiliraAlignedRaceGroup TargetGroup => HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Milira;
	}

	public sealed class ToddlerPlayGiver_HarSelfPlay_Bunny : ToddlerPlayGiver_HarSelfPlayBase
	{
		protected override HarRaceWhitelistUtility.MiliraAlignedRaceGroup TargetGroup => HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Bunny;
	}

	public sealed class ToddlerPlayGiver_HarSelfPlay_Cinder : ToddlerPlayGiver_HarSelfPlayBase
	{
		protected override HarRaceWhitelistUtility.MiliraAlignedRaceGroup TargetGroup => HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Cinder;
	}
}
