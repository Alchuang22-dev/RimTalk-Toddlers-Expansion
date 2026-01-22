using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public sealed class ToddlersExpansionSettings : ModSettings
	{
		// 商队/过路者生成设置
		public bool EnableCaravanToddlerGeneration = true;
		public int MaxToddlersPerGroup = 3;
		public int MaxChildrenPerGroup = 2;
		public float ToddlerGenerationChance = 0.7f;
		public float ChildGenerationChance = 0.6f;
		public int MinBatchCount = 1;
		public int MaxBatchCount = 3;
		public float ExtraBatchChance = 0.3f;

		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref EnableCaravanToddlerGeneration, "EnableCaravanToddlerGeneration", true);
			Scribe_Values.Look(ref MaxToddlersPerGroup, "MaxToddlersPerGroup", 3);
			Scribe_Values.Look(ref MaxChildrenPerGroup, "MaxChildrenPerGroup", 2);
			Scribe_Values.Look(ref ToddlerGenerationChance, "ToddlerGenerationChance", 0.7f);
			Scribe_Values.Look(ref ChildGenerationChance, "ChildGenerationChance", 0.6f);
			Scribe_Values.Look(ref MinBatchCount, "MinBatchCount", 1);
			Scribe_Values.Look(ref MaxBatchCount, "MaxBatchCount", 3);
			Scribe_Values.Look(ref ExtraBatchChance, "ExtraBatchChance", 0.3f);
		}
	}
}
