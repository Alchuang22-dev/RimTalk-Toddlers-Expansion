using RimTalk_ToddlersExpansion.Harmony;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public sealed class ToddlersExpansionMod : Mod
	{
		public static ToddlersExpansionSettings Settings;
		private static Vector2 scrollPosition = Vector2.zero;

		public ToddlersExpansionMod(ModContentPack content) : base(content)
		{
			Settings = GetSettings<ToddlersExpansionSettings>();

			HarmonyBootstrap.Init();
			LongEventHandler.ExecuteWhenFinished(() =>
			{
				ToddlersExpansionDiagnostics.Run();
				RimTalkCompatUtility.TryRegisterToddlerVariables();
				Integration.Toddlers.MidnightSnackUtility.RegisterGameComponent();
				Integration.Toddlers.LanguageLearningUtility.RegisterGameComponent();
				Integration.Toddlers.ToddlerSelfBathUtility.RegisterGameComponent();
				Integration.Toddlers.HAR.HarNurseryMoodUtility.RegisterGameComponent();
			});
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			var settings = Settings;

			// 计算内容高度 - 足够容纳所有设置项
			float contentHeight = 1200f;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

			Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

			var listingStandard = new Listing_Standard();
			listingStandard.Begin(viewRect);

			// 标题
			Text.Font = GameFont.Medium;
			listingStandard.Label("RimTalk_ToddlersExpansion_Settings_Title".Translate());
			Text.Font = GameFont.Small;
			listingStandard.Gap();

			// ========== 无聊机制设置 ==========
			listingStandard.Label("RimTalk_Boredom_Settings_Header".Translate());
			listingStandard.GapLine();

			listingStandard.CheckboxLabeled("RimTalk_Boredom_Enable".Translate(), ref ToddlersExpansionSettings.enableBoredomSystem, "RimTalk_Boredom_Enable_Tooltip".Translate());
			listingStandard.Gap();

			if (ToddlersExpansionSettings.enableBoredomSystem)
			{
				listingStandard.Label("RimTalk_Boredom_IncreasePerActivity".Translate(ToddlersExpansionSettings.boredomIncreasePerActivity.ToStringPercent()));
				ToddlersExpansionSettings.boredomIncreasePerActivity = listingStandard.Slider(ToddlersExpansionSettings.boredomIncreasePerActivity, 0.01f, 0.2f);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_Boredom_MaxCap".Translate(ToddlersExpansionSettings.boredomMaxCap.ToStringPercent()));
				ToddlersExpansionSettings.boredomMaxCap = listingStandard.Slider(ToddlersExpansionSettings.boredomMaxCap, 0.5f, 1.0f);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_Boredom_DailyRecoveryRate".Translate(ToddlersExpansionSettings.boredomDailyRecoveryRate.ToStringPercent()));
				ToddlersExpansionSettings.boredomDailyRecoveryRate = listingStandard.Slider(ToddlersExpansionSettings.boredomDailyRecoveryRate, 0.05f, 0.5f);
				listingStandard.Gap();

				listingStandard.CheckboxLabeled("RimTalk_Boredom_AutoDetection".Translate(), ref ToddlersExpansionSettings.enableAutoDetection, "RimTalk_Boredom_AutoDetection_Tooltip".Translate());
				listingStandard.Gap();
			}

			listingStandard.GapLine();

			// ========== 语言学习设置 ==========
			listingStandard.Label("RimTalk_Language_Settings_Header".Translate());
			listingStandard.GapLine();

			listingStandard.Label("RimTalk_Language_LearningFactor".Translate(ToddlersExpansionSettings.learningFactor_Talking.ToStringPercent()));
			ToddlersExpansionSettings.learningFactor_Talking = listingStandard.Slider(ToddlersExpansionSettings.learningFactor_Talking, 0.01f, 1f);
			listingStandard.Gap();

			listingStandard.GapLine();

			// ========== Feeding Settings ==========
			listingStandard.Label("RimTalk_ToddlersExpansion_Feeding_Settings_Header".Translate());
			listingStandard.GapLine();

			listingStandard.Label("RimTalk_ToddlersExpansion_ToddlerEatingSpeed".Translate(ToddlersExpansionSettings.toddlerEatingSpeedFactor.ToString("0.##")));
			ToddlersExpansionSettings.toddlerEatingSpeedFactor = listingStandard.Slider(ToddlersExpansionSettings.toddlerEatingSpeedFactor, 0.1f, 10f);
			listingStandard.Gap();

			listingStandard.GapLine();

			
			listingStandard.Label("RimTalk_ToddlersExpansion_Behavior_Settings_Header".Translate());
			listingStandard.GapLine();

			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_EnableHostileToddlerColonistBehavior".Translate(),
				ref ToddlersExpansionSettings.enableHostileToddlerColonistBehavior,
				"RimTalk_ToddlersExpansion_EnableHostileToddlerColonistBehavior_Tooltip".Translate());
			listingStandard.Gap();
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_EnableUnder3HairRendering".Translate(),
				ref ToddlersExpansionSettings.enableUnder3HairRendering,
				"RimTalk_ToddlersExpansion_EnableUnder3HairRendering_Tooltip".Translate());
			listingStandard.Gap();

			listingStandard.GapLine();
// ========== 商队/过路者设置 ==========
			listingStandard.Label("RimTalk_Caravan_Settings_Header".Translate());
			listingStandard.GapLine();

			// 启用过路者/商队幼儿生成
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_EnableCaravanToddlerGeneration".Translate(), ref settings.EnableCaravanToddlerGeneration, "RimTalk_ToddlersExpansion_EnableCaravanToddlerGeneration_Tooltip".Translate());
			listingStandard.Gap();

			if (settings.EnableCaravanToddlerGeneration)
			{
				listingStandard.Label("RimTalk_ToddlersExpansion_MaxToddlersPerGroup".Translate(settings.MaxToddlersPerGroup));
				settings.MaxToddlersPerGroup = (int)listingStandard.Slider(settings.MaxToddlersPerGroup, 0, 5);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_MaxChildrenPerGroup".Translate(settings.MaxChildrenPerGroup));
				settings.MaxChildrenPerGroup = (int)listingStandard.Slider(settings.MaxChildrenPerGroup, 0, 5);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_ToddlerGenerationChance".Translate(settings.ToddlerGenerationChance.ToStringPercent()));
				settings.ToddlerGenerationChance = listingStandard.Slider(settings.ToddlerGenerationChance, 0f, 1f);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_ChildGenerationChance".Translate(settings.ChildGenerationChance.ToStringPercent()));
				settings.ChildGenerationChance = listingStandard.Slider(settings.ChildGenerationChance, 0f, 1f);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_MinBatchCount".Translate(settings.MinBatchCount));
				settings.MinBatchCount = (int)listingStandard.Slider(settings.MinBatchCount, 1, 5);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_MaxBatchCount".Translate(settings.MaxBatchCount));
				settings.MaxBatchCount = (int)listingStandard.Slider(settings.MaxBatchCount, 1, 5);
				listingStandard.Gap();

				// 确保最小值不大于最大值
				if (settings.MinBatchCount > settings.MaxBatchCount)
				{
					settings.MinBatchCount = settings.MaxBatchCount;
				}

				listingStandard.Label("RimTalk_ToddlersExpansion_ExtraBatchChance".Translate(settings.ExtraBatchChance.ToStringPercent()));
				settings.ExtraBatchChance = listingStandard.Slider(settings.ExtraBatchChance, 0f, 1f);
				listingStandard.Gap();
			}

			listingStandard.End();
			Widgets.EndScrollView();
		}

		public override string SettingsCategory() => "RimTalk Toddlers Expansion";
	}
}
