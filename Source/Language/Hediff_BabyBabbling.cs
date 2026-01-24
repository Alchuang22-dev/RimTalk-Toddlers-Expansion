using RimTalk_ToddlersExpansion.Integration.BioTech;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Language
{
	public sealed class Hediff_BabyBabbling : HediffWithComps
	{
		public override bool ShouldRemove => pawn == null
			|| ToddlersCompatUtility.IsToddler(pawn)
			|| !BiotechCompatUtility.IsBaby(pawn);

		public override string SeverityLabel => null;
	}
}
