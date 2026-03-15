using RimWorld;
using Verse;

namespace RatBabyMod
{
	[DefOf]
	public static class RatBabyDefOf
	{
		public static QuestScriptDef RatBaby_RatkinOrphanQuest;

		static RatBabyDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(RatBabyDefOf));
		}
	}
}
