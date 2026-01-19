using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.BioTech
{
	public sealed class ThoughtWorker_MyBabyNearby : ThoughtWorker
	{
		protected override ThoughtState CurrentStateInternal(Pawn p)
		{
			if (!BiotechCompatUtility.IsBiotechActive)
			{
				return ThoughtState.Inactive;
			}

			return BedroomThoughtsPatchHelper.GetMyBabyNearbyThought(p);
		}
	}
}
