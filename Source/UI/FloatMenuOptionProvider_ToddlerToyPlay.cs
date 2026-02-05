using RimTalk_ToddlersExpansion.Harmony;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.UI
{
	public class FloatMenuOptionProvider_ToddlerToyPlay : FloatMenuOptionProvider
	{
		protected override bool Drafted => true;
		protected override bool Undrafted => true;
		protected override bool Multiselect => false;
		protected override bool RequiresManipulation => false;

		public override bool SelectedPawnValid(Pawn pawn, FloatMenuContext context)
		{
			if (!base.SelectedPawnValid(pawn, context))
			{
				return false;
			}

			if (pawn?.Map == null)
			{
				return false;
			}

			if (!ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			if (pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
			{
				return false;
			}

			return true;
		}

		public override bool TargetThingValid(Thing thing, FloatMenuContext context)
		{
			return base.TargetThingValid(thing, context) && thing is Building;
		}

		protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
		{
			Pawn pawn = context?.FirstSelectedPawn;
			if (pawn == null || clickedThing == null)
			{
				return null;
			}

			if (!Patch_FloatMenu_ToddlerToyPlay.TryCreateToyPlayOptionForBuilding(pawn, clickedThing, out FloatMenuOption option, out string reason))
			{
				Patch_FloatMenu_ToddlerToyPlay.DebugLog($"provider skip: pawn={pawn.LabelShort} thing={clickedThing.LabelShort} def={clickedThing.def?.defName ?? "null"} reason={reason}");
				return null;
			}

			Patch_FloatMenu_ToddlerToyPlay.DebugLog($"provider added: pawn={pawn.LabelShort} thing={clickedThing.LabelShort} def={clickedThing.def?.defName ?? "null"} source={reason}");
			return option;
		}
	}
}
