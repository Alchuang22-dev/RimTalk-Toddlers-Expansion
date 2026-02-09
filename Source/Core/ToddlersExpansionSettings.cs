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

		// 厌倦系统设置
		public static bool enableBoredomSystem = true;
		public static float boredomIncreasePerActivity = 0.05f; // 每次活动增加5%
		public static float boredomMaxCap = 0.70f; // 厌倦度封顶70%
		public static float boredomDailyRecoveryRate = 0.07f; // 每天恢复7%
		public static bool enableAutoDetection = true;

		// 语言学习设置
		public static float learningFactor_Talking = 0.8f; // 与其他学习保持一致

		// Toddler eating speed settings
		public static float toddlerEatingSpeedFactor = 1f; // 1 = Toddlers default (slow)

		// AI 逻辑
		public static bool enableHostileToddlerColonistBehavior = true;
		public static bool enableUnder3HairRendering = false;

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

			// 厌倦系统设置
			Scribe_Values.Look(ref enableBoredomSystem, "enableBoredomSystem", true);
			Scribe_Values.Look(ref boredomIncreasePerActivity, "boredomIncreasePerActivity", 0.05f);
			Scribe_Values.Look(ref boredomMaxCap, "boredomMaxCap", 0.70f);
			Scribe_Values.Look(ref boredomDailyRecoveryRate, "boredomDailyRecoveryRate", 0.07f);
			Scribe_Values.Look(ref enableAutoDetection, "enableAutoDetection", true);

			// 语言学习设置
			Scribe_Values.Look(ref learningFactor_Talking, "learningFactor_Talking", 0.8f);
			Scribe_Values.Look(ref toddlerEatingSpeedFactor, "toddlerEatingSpeedFactor", 1f);

			// AI 行为修正
			Scribe_Values.Look(ref enableHostileToddlerColonistBehavior, "enableHostileToddlerColonistBehavior", true);
			Scribe_Values.Look(ref enableUnder3HairRendering, "enableUnder3HairRendering", false);
		}
	}
}
