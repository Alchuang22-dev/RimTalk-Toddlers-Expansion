using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	[DefOf]
	public static class ToddlersExpansionJobDefOf
	{
		public static JobDef RimTalk_ToddlerSelfPlayJob;
		public static JobDef RimTalk_ToddlerMutualPlayJob;
		public static JobDef RimTalk_WatchToddlerPlayJob;
		public static JobDef RimTalk_ToddlerPlayAtToy;
		public static JobDef RimTalk_MidnightSnack;

		static ToddlersExpansionJobDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionJobDefOf));
		}
	}

	[DefOf]
	public static class ToddlersExpansionJoyGiverDefOf
	{
		public static JoyGiverDef RimTalk_WatchToddlerPlayJoy;
		public static JoyGiverDef RimTalk_ToddlerToyPlayJoy;

		static ToddlersExpansionJoyGiverDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionJoyGiverDefOf));
		}
	}

	[DefOf]
	public static class ToddlersExpansionHediffDefOf
	{
		public static HediffDef RimTalk_ToddlerLanguageLearning;
		public static HediffDef RimTalk_ToddlerToothDecay;
		public static HediffDef RimTalk_MissingTooth;
		public static HediffDef RimTalk_MidnightSnackCooldown;

		static ToddlersExpansionHediffDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionHediffDefOf));
		}
	}

	[DefOf]
	public static class ToddlersExpansionThoughtDefOf
	{
		public static ThoughtDef RimTalk_MyBabyNearby;
		public static ThoughtDef RimTalk_TalkedToBaby;
		public static ThoughtDef RimTalk_ToddlerSleepAlone;
		public static ThoughtDef RimTalk_ToddlerSleepWithOthers;
		public static ThoughtDef RimTalk_ToddlerSleepWithParents;
		public static ThoughtDef RimTalk_MidnightSnackSuccess_Child;
		public static ThoughtDef RimTalk_MidnightSnackSuccess_Baby;
		public static ThoughtDef RimTalk_MidnightSnackSuccess_Toddler;
		public static ThoughtDef RimTalk_VisitedDentist_Child;
		public static ThoughtDef RimTalk_VisitedDentist_Baby;
		public static ThoughtDef RimTalk_VisitedDentist_Toddler;

		static ToddlersExpansionThoughtDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionThoughtDefOf));
		}
	}

	[DefOf]
	public static class ToddlersExpansionAnimationDefOf
	{
		public static AnimationDef RimTalk_ToddlerPlay_Wiggle;
		public static AnimationDef RimTalk_ToddlerPlay_Sway;
		public static AnimationDef RimTalk_ToddlerPlay_Lay;
		public static AnimationDef RimTalk_ToddlerPlay_Crawl;

		static ToddlersExpansionAnimationDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionAnimationDefOf));
		}
	}
}
