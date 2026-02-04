using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.UI
{
	public class FloatMenuOptionProvider_ToddlerSelfBath : FloatMenuOptionProvider
	{
		protected override bool Drafted => true;

		protected override bool Undrafted => true;

		protected override bool Multiselect => false;

		protected override bool RequiresManipulation => false;

		public override bool SelectedPawnValid(Pawn pawn, FloatMenuContext context)
		{
			return base.SelectedPawnValid(pawn, context) && ToddlersCompatUtility.IsToddler(pawn);
		}

		public override bool TargetThingValid(Thing thing, FloatMenuContext context)
		{
			return base.TargetThingValid(thing, context) && ToddlerSelfBathUtility.IsBathFixture(thing);
		}

		protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
		{
			Pawn pawn = context?.FirstSelectedPawn;
			if (pawn == null || clickedThing == null || pawn.Map == null || clickedThing.Map != pawn.Map)
			{
				return null;
			}

			string label = "Wash".Translate() + " " + clickedThing.LabelShort;
			if (!ToddlersCompatUtility.CanSelfCare(pawn))
			{
				return Disabled(label, "RimTalk_ToddlersExpansion_ToddlerSelfBathTooSmall".Translate());
			}

			if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
				string reason = "CannotUseReason".Translate("IncapableOfCapacity".Translate(PawnCapacityDefOf.Manipulation.label, pawn.Named("PAWN")));
				return Disabled(label, reason);
			}

			if (ToddlerSelfBathUtility.GetHygieneNeed(pawn) == null)
			{
				return null;
			}

			if (!ToddlerSelfBathUtility.TryCreateSelfBathJobForTarget(pawn, clickedThing, out Job job, ignoreAllowedArea: true))
			{
				return Disabled(label, "CannotUseNoPath".Translate());
			}

			return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, delegate
			{
				pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			}), pawn, clickedThing);
		}

		private static FloatMenuOption Disabled(string label, string reason)
		{
			if (string.IsNullOrEmpty(reason))
			{
				return new FloatMenuOption(label, null);
			}

			return new FloatMenuOption(label + ": " + reason, null);
		}
	}
}
