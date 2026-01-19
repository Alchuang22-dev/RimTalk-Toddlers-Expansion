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

		static ToddlersExpansionJobDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionJobDefOf));
		}
	}

	[DefOf]
	public static class ToddlersExpansionJoyGiverDefOf
	{
		public static JoyGiverDef RimTalk_WatchToddlerPlayJoy;

		static ToddlersExpansionJoyGiverDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionJoyGiverDefOf));
		}
	}

	[DefOf]
	public static class ToddlersExpansionHediffDefOf
	{
		public static HediffDef RimTalk_ToddlerLanguageLearning;

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

		static ToddlersExpansionThoughtDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionThoughtDefOf));
		}
	}
}
