using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class CompProperties_ToddlerToy : CompProperties
	{
		public bool groundToy = true;
		public bool allowBabies = true;
		public bool allowToddlers = true;
		public bool allowChildren = true;
		public float joyGainPerTick = 0.0002f;
		public JoyKindDef joyKind;
		public int useDurationTicks = 2000;

		public CompProperties_ToddlerToy()
		{
			compClass = typeof(CompToddlerToy);
		}
	}

	public sealed class CompToddlerToy : ThingComp
	{
		public CompProperties_ToddlerToy Props => (CompProperties_ToddlerToy)props;

		public bool GroundToy => Props?.groundToy ?? true;

		public float JoyGainPerTick => Props?.joyGainPerTick ?? 0.0002f;

		public int UseDurationTicks => Props?.useDurationTicks ?? 2000;

		public JoyKindDef JoyKind => Props?.joyKind ?? JoyKindDefOf.Meditative;

		public bool Allows(Pawn pawn)
		{
			if (pawn == null || Props == null)
			{
				return false;
			}

			if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby())
			{
				return Props.allowBabies;
			}

			if (ToddlersCompatUtility.IsToddler(pawn))
			{
				return Props.allowToddlers;
			}

			return pawn.DevelopmentalStage == DevelopmentalStage.Child && Props.allowChildren;
		}
	}
}
