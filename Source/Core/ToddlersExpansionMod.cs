using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Harmony;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public sealed partial class ToddlersExpansionMod : Mod
	{
		public static ToddlersExpansionSettings Settings;
		private static Vector2 generalScrollPosition = Vector2.zero;
		private static Vector2 outingScrollPosition = Vector2.zero;
		private static Vector2 animationScrollPosition = Vector2.zero;
		private static Vector2 talkScrollPosition = Vector2.zero;

		public ToddlersExpansionMod(ModContentPack content) : base(content)
		{
			Settings = GetSettings<ToddlersExpansionSettings>();

			HarmonyBootstrap.Init();
			LongEventHandler.ExecuteWhenFinished(() =>
			{
				Integration.Toddlers.ToddlerAgeSettingsUtility.ApplyConfiguredToddlerAge(refreshExistingPawns: false);
				ToddlersExpansionDiagnostics.Run();
				RimTalkCompatUtility.TryRegisterToddlerVariables();
				Integration.Toddlers.ToddlerCarryingGameComponent.RegisterGameComponent();
				Integration.Toddlers.MidnightSnackUtility.RegisterGameComponent();
				Integration.Toddlers.LanguageLearningUtility.RegisterGameComponent();
				Integration.Toddlers.ToddlerSelfBathUtility.RegisterGameComponent();
				Integration.Toddlers.HAR.HarNurseryMoodUtility.RegisterGameComponent();
				Integration.YayoAnimation.YayoAnimationSafeFallbackUtility.RegisterGameComponent();
				Integration.Kiiro.KiiroRefugeeBabyGuardUtility.RegisterGameComponent();
			});
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			ToddlersExpansionSettings settings = Settings;
			if (settings == null)
			{
				return;
			}

			settings.SettingsPageIndex = Mathf.Clamp(settings.SettingsPageIndex, 0, 3);

			const float topPadding = 40f;
			Rect tabsRect = new Rect(inRect.x, inRect.y + topPadding, inRect.width, 32f);
			List<TabRecord> tabs = new List<TabRecord>
			{
				new TabRecord("RimTalk_ToddlersExpansion_Settings_Page1".Translate(), () => settings.SettingsPageIndex = 0, settings.SettingsPageIndex == 0),
				new TabRecord("RimTalk_ToddlersExpansion_Settings_Page2".Translate(), () => settings.SettingsPageIndex = 1, settings.SettingsPageIndex == 1),
				new TabRecord("RimTalk_ToddlersExpansion_Settings_Page3".Translate(), () => settings.SettingsPageIndex = 2, settings.SettingsPageIndex == 2),
				new TabRecord("RimTalk_ToddlersExpansion_Settings_Page4".Translate(), () => settings.SettingsPageIndex = 3, settings.SettingsPageIndex == 3)
			};
			TabDrawer.DrawTabs(tabsRect, tabs);

			Rect pageRect = new Rect(inRect.x, tabsRect.yMax + 8f, inRect.width, inRect.height - (tabsRect.yMax - inRect.y) - 8f);
			if (settings.SettingsPageIndex == 0)
			{
				DrawGeneralSettingsPage(pageRect, settings);
			}
			else if (settings.SettingsPageIndex == 1)
			{
				DrawOutingSettingsPage(pageRect, settings);
			}
			else if (settings.SettingsPageIndex == 2)
			{
				DrawAnimationSettingsPage(pageRect);
			}
			else
			{
				DrawTalkSettingsPage(pageRect);
			}
		}

		private void DrawGeneralSettingsPage(Rect inRect, ToddlersExpansionSettings settings)
		{
			const float columnGap = 24f;
			float contentHeight = 2230f;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);
			float columnWidth = (viewRect.width - columnGap) / 2f;
			Rect leftColumnRect = new Rect(0f, 0f, columnWidth, contentHeight);
			Rect rightColumnRect = new Rect(columnWidth + columnGap, 0f, columnWidth, contentHeight);

			Widgets.BeginScrollView(inRect, ref generalScrollPosition, viewRect);
			Listing_Standard leftColumn = new Listing_Standard();
			leftColumn.Begin(leftColumnRect);
			DrawCaravanGenerationSettingsSection(leftColumn, settings);
			DrawBoredomSettingsSection(leftColumn);
			DrawLanguageSettingsSection(leftColumn);
			DrawFeedingSettingsSection(leftColumn);
			DrawBabyAppearanceSettingsSection(leftColumn);
			DrawBabyCrySettingsSection(leftColumn);
			leftColumn.End();

			Listing_Standard rightColumn = new Listing_Standard();
			rightColumn.Begin(rightColumnRect);
			DrawPerformanceSettingsSection(rightColumn);
			DrawBehaviorSettingsSection(rightColumn);
			DrawInteractionSettingsSection(rightColumn);
			DrawMishapEventSettingsSection(rightColumn);
			rightColumn.End();

			Widgets.EndScrollView();
		}

		private void DrawOutingSettingsPage(Rect inRect, ToddlersExpansionSettings settings)
		{
			float contentHeight = 920f;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

			Widgets.BeginScrollView(inRect, ref outingScrollPosition, viewRect);
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(viewRect);

			listingStandard.Label("RimTalk_ToddlersExpansion_Outing_Settings_Header".Translate());
			listingStandard.GapLine();
			listingStandard.Label("RimTalk_ToddlersExpansion_Outing_Settings_Desc".Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_VanillaEdgeRandom".Translate(), ref settings.EnableOutingPoolVanillaEdgeRandom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_GrowingZone".Translate(), ref settings.EnableOutingPoolGrowingZone);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_StockpileZone".Translate(), ref settings.EnableOutingPoolStockpileZone);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_ResearchRoom".Translate(), ref settings.EnableOutingPoolResearchRoom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_TempleRoom".Translate(), ref settings.EnableOutingPoolTempleRoom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_KitchenRoom".Translate(), ref settings.EnableOutingPoolKitchenRoom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_RecreationRoom".Translate(), ref settings.EnableOutingPoolRecreationRoom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_HospitalRoom".Translate(), ref settings.EnableOutingPoolHospitalRoom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_OtherNonBedroomRooms".Translate(), ref settings.EnableOutingPoolOtherNonBedroomRooms);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_ThingWithCompsLandmark".Translate(), ref settings.EnableOutingPoolThingWithCompsLandmark);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_River".Translate(), ref settings.EnableOutingPoolRiver);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_Lake".Translate(), ref settings.EnableOutingPoolLake);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_Snow".Translate(), ref settings.EnableOutingPoolSnow);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_Cave".Translate(), ref settings.EnableOutingPoolCave);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_Sand".Translate(), ref settings.EnableOutingPoolSand);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_AncientRoad".Translate(), ref settings.EnableOutingPoolAncientRoad);

			if (!HasAnyOutingPoolEnabled(settings))
			{
				listingStandard.Gap();
				GUI.color = Color.yellow;
				listingStandard.Label("RimTalk_ToddlersExpansion_OutingPool_AllDisabledWarning".Translate());
				GUI.color = Color.white;
			}

			listingStandard.End();
			Widgets.EndScrollView();
		}

		private void DrawAnimationSettingsPage(Rect inRect)
		{
			float contentHeight = 1100f;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

			Widgets.BeginScrollView(inRect, ref animationScrollPosition, viewRect);
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(viewRect);

			bool yayoLoaded = Integration.YayoAnimation.YayoAnimationCompatUtility.IsYayoAnimationLoaded;
			listingStandard.Label("RimTalk_ToddlersExpansion_Animation_Settings_Header".Translate());
			listingStandard.GapLine();
			listingStandard.Label("RimTalk_ToddlersExpansion_Animation_Settings_Desc".Translate());
			listingStandard.Gap();
			listingStandard.Label((yayoLoaded
				? "RimTalk_ToddlersExpansion_Animation_YayoLoaded"
				: "RimTalk_ToddlersExpansion_Animation_YayoNotLoaded").Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Animation_EnableNewborn".Translate(),
				ref ToddlersExpansionSettings.EnableNewbornPlayAnimations,
				"RimTalk_ToddlersExpansion_Animation_EnableNewborn_Tooltip".Translate());
			listingStandard.GapLine();

			listingStandard.Label("RimTalk_ToddlersExpansion_Animation_NoYayo_Header".Translate());
			listingStandard.Gap();
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Wiggle".Translate(), ref ToddlersExpansionSettings.EnableNativePlayWiggle);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Sway".Translate(), ref ToddlersExpansionSettings.EnableNativePlaySway);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Lay".Translate(), ref ToddlersExpansionSettings.EnableNativePlayLay);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_ProneCrawl".Translate(), ref ToddlersExpansionSettings.EnableNativePlayCrawl);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_ToddlersWobble".Translate(), ref ToddlersExpansionSettings.EnableNativePlayToddlerWobble);
			listingStandard.GapLine();

			listingStandard.Label("RimTalk_ToddlersExpansion_Animation_Yayo_Header".Translate());
			listingStandard.Gap();
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Wiggle".Translate(), ref ToddlersExpansionSettings.EnableNativePlayWiggle);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Sway".Translate(), ref ToddlersExpansionSettings.EnableNativePlaySway);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Lay".Translate(), ref ToddlersExpansionSettings.EnableNativePlayLay);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_ProneCrawl".Translate(), ref ToddlersExpansionSettings.EnableNativePlayCrawl);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_ToddlersWobble".Translate(), ref ToddlersExpansionSettings.EnableNativePlayToddlerWobble);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_PlayToys".Translate(), ref ToddlersExpansionSettings.EnableYayoPlayToys);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Hoopstone".Translate(), ref ToddlersExpansionSettings.EnableYayoPlayHoopstone);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_DartsBoard".Translate(), ref ToddlersExpansionSettings.EnableYayoPlayDartsBoard);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_GoldenCube".Translate(), ref ToddlersExpansionSettings.EnableYayoGoldenCube);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_SocialRelax".Translate(), ref ToddlersExpansionSettings.EnableYayoSocialRelax);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_BabyRoll".Translate(), ref ToddlersExpansionSettings.EnableYayoBabyRoll);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Roll".Translate(), ref ToddlersExpansionSettings.EnableYayoCustomRoll);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Spin".Translate(), ref ToddlersExpansionSettings.EnableYayoCustomSpin);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Hop".Translate(), ref ToddlersExpansionSettings.EnableYayoCustomHop);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_RunLoop".Translate(), ref ToddlersExpansionSettings.EnableYayoCustomRunLoop);

			listingStandard.End();
			Widgets.EndScrollView();
		}

		private static void DrawPerformanceSettingsSection(Listing_Standard listingStandard)
		{
			listingStandard.Label("RimTalk_ToddlersExpansion_Performance_Settings_Header".Translate());
			listingStandard.GapLine();
			listingStandard.Label("RimTalk_ToddlersExpansion_Performance_Settings_Desc".Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Performance_MuteSpamLogs".Translate(),
				ref ToddlersExpansionSettings.MuteSpamDebugLogs,
				"RimTalk_ToddlersExpansion_Performance_MuteSpamLogs_Tooltip".Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Performance_MuteAllLogs".Translate(),
				ref ToddlersExpansionSettings.MuteAllLogs,
				"RimTalk_ToddlersExpansion_Performance_MuteAllLogs_Tooltip".Translate());
			listingStandard.Gap();

			listingStandard.Label(
				"RimTalk_ToddlersExpansion_Performance_MutualPlayCheckInterval".Translate(
					ToddlersExpansionSettings.GetMutualPlayPartnerCheckIntervalTicks()));
			ToddlersExpansionSettings.MutualPlayPartnerCheckIntervalTicks =
				(int)listingStandard.Slider(ToddlersExpansionSettings.MutualPlayPartnerCheckIntervalTicks, 1, 600);
			listingStandard.Gap();

			listingStandard.Label(
				"RimTalk_ToddlersExpansion_Performance_MainLoopCheckInterval".Translate(
					ToddlersExpansionSettings.GetToddlerMainLoopCheckIntervalTicks()));
			ToddlersExpansionSettings.ToddlerMainLoopCheckIntervalTicks =
				(int)listingStandard.Slider(ToddlersExpansionSettings.ToddlerMainLoopCheckIntervalTicks, 1, 120);
			listingStandard.Gap();

			listingStandard.Label(
				"RimTalk_ToddlersExpansion_Performance_BabyCarryCheckInterval".Translate(
					ToddlersExpansionSettings.GetBabyCarryPickupCheckIntervalTicks()));
			ToddlersExpansionSettings.BabyCarryCheckIntervalTicks =
				(int)listingStandard.Slider(ToddlersExpansionSettings.BabyCarryCheckIntervalTicks, 30, 600);
			listingStandard.Gap();
		}

		private void DrawTalkSettingsPage(Rect inRect)
		{
			float contentHeight = 900f;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

			Widgets.BeginScrollView(inRect, ref talkScrollPosition, viewRect);
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(viewRect);

			DrawLlmSettingsSection(listingStandard, Settings);
			listingStandard.GapLine();

			DrawTalkRequestSettingsSection(listingStandard);

			listingStandard.End();
			Widgets.EndScrollView();
		}

		private static void DrawCaravanGenerationSettingsSection(Listing_Standard listingStandard, ToddlersExpansionSettings settings)
		{
			listingStandard.Label("RimTalk_Caravan_Settings_Header".Translate());
			listingStandard.GapLine();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_EnableCaravanToddlerGeneration".Translate(),
				ref settings.EnableCaravanToddlerGeneration,
				"RimTalk_ToddlersExpansion_EnableCaravanToddlerGeneration_Tooltip".Translate());
			listingStandard.Gap();

			if (!settings.EnableCaravanToddlerGeneration)
			{
				listingStandard.GapLine();
				return;
			}

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

			if (settings.MinBatchCount > settings.MaxBatchCount)
			{
				settings.MinBatchCount = settings.MaxBatchCount;
			}

			listingStandard.Label("RimTalk_ToddlersExpansion_ExtraBatchChance".Translate(settings.ExtraBatchChance.ToStringPercent()));
			settings.ExtraBatchChance = listingStandard.Slider(settings.ExtraBatchChance, 0f, 1f);
			listingStandard.Gap();
			listingStandard.GapLine();
		}

		private static void DrawBoredomSettingsSection(Listing_Standard listingStandard)
		{
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
				ToddlersExpansionSettings.boredomMaxCap = listingStandard.Slider(ToddlersExpansionSettings.boredomMaxCap, 0.5f, 1f);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_Boredom_DailyRecoveryRate".Translate(ToddlersExpansionSettings.boredomDailyRecoveryRate.ToStringPercent()));
				ToddlersExpansionSettings.boredomDailyRecoveryRate = listingStandard.Slider(ToddlersExpansionSettings.boredomDailyRecoveryRate, 0.05f, 0.5f);
				listingStandard.Gap();

				listingStandard.CheckboxLabeled("RimTalk_Boredom_AutoDetection".Translate(), ref ToddlersExpansionSettings.enableAutoDetection, "RimTalk_Boredom_AutoDetection_Tooltip".Translate());
				listingStandard.Gap();
			}

			listingStandard.GapLine();
		}

		private static void DrawLanguageSettingsSection(Listing_Standard listingStandard)
		{
			listingStandard.Label("RimTalk_Language_Settings_Header".Translate());
			listingStandard.GapLine();
			listingStandard.Label("RimTalk_ToddlersExpansion_NewbornToToddlerDays_Desc".Translate());
			listingStandard.Gap();
			int oldThresholdDays = ToddlersExpansionSettings.GetNewbornToToddlerDays();
			listingStandard.Label(
				"RimTalk_ToddlersExpansion_NewbornToToddlerDays".Translate(
					oldThresholdDays,
					ToddlersExpansionSettings.GetNewbornToToddlerYears().ToString("0.##")));
			ToddlersExpansionSettings.newbornToToddlerDays =
				(int)listingStandard.Slider(ToddlersExpansionSettings.GetNewbornToToddlerDays(), 1, 179);
			if (ToddlersExpansionSettings.GetNewbornToToddlerDays() != oldThresholdDays)
			{
				Integration.Toddlers.ToddlerAgeSettingsUtility.ApplyConfiguredToddlerAge(refreshExistingPawns: true);
			}
			listingStandard.Gap();
			listingStandard.Label("RimTalk_Language_LearningFactor".Translate(ToddlersExpansionSettings.learningFactor_Talking.ToString("0.##")));
			ToddlersExpansionSettings.learningFactor_Talking = listingStandard.Slider(ToddlersExpansionSettings.learningFactor_Talking, 0.1f, 3f);
			listingStandard.Gap();
			listingStandard.GapLine();
		}

		private static void DrawFeedingSettingsSection(Listing_Standard listingStandard)
		{
			listingStandard.Label("RimTalk_ToddlersExpansion_Feeding_Settings_Header".Translate());
			listingStandard.GapLine();
			listingStandard.Label("RimTalk_ToddlersExpansion_ToddlerEatingSpeed".Translate(ToddlersExpansionSettings.toddlerEatingSpeedFactor.ToString("0.##")));
			ToddlersExpansionSettings.toddlerEatingSpeedFactor = listingStandard.Slider(ToddlersExpansionSettings.toddlerEatingSpeedFactor, 0.1f, 10f);
			listingStandard.Gap();
			listingStandard.GapLine();
		}

		private static void DrawBabyAppearanceSettingsSection(Listing_Standard listingStandard)
		{
			listingStandard.Label("RimTalk_ToddlersExpansion_BabyAppearance_Settings_Header".Translate());
			listingStandard.GapLine();
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_EnableUnder3HairRendering".Translate(),
				ref ToddlersExpansionSettings.enableUnder3HairRendering,
				"RimTalk_ToddlersExpansion_EnableUnder3HairRendering_Tooltip".Translate());
			listingStandard.Gap();
			listingStandard.GapLine();
		}

		private static void DrawBabyCrySettingsSection(Listing_Standard listingStandard)
		{
			listingStandard.Label("RimTalk_ToddlersExpansion_BabyCry_Settings_Header".Translate());
			listingStandard.GapLine();

			bool muteSocialImpact = ToddlersExpansionSettings.babyCryAffectsMoodOnly;
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_BabyCry_NoSocial".Translate(),
				ref muteSocialImpact,
				"RimTalk_ToddlersExpansion_BabyCry_NoSocial_Tooltip".Translate());
			ToddlersExpansionSettings.babyCryAffectsMoodOnly = muteSocialImpact;
			listingStandard.Gap();

			bool muteMoodImpact = !ToddlersExpansionSettings.babyCryAffectsMood;
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_BabyCry_NoMood".Translate(),
				ref muteMoodImpact,
				"RimTalk_ToddlersExpansion_BabyCry_NoMood_Tooltip".Translate());
			ToddlersExpansionSettings.babyCryAffectsMood = !muteMoodImpact;
			listingStandard.Gap();
		}

		private static void DrawBehaviorSettingsSection(Listing_Standard listingStandard)
		{
			listingStandard.Label("RimTalk_ToddlersExpansion_Behavior_Settings_Header".Translate());
			listingStandard.GapLine();
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_EnableHostileToddlerColonistBehavior".Translate(),
				ref ToddlersExpansionSettings.enableHostileToddlerColonistBehavior,
				"RimTalk_ToddlersExpansion_EnableHostileToddlerColonistBehavior_Tooltip".Translate());
			listingStandard.Gap();
			listingStandard.GapLine();
		}

		private static void DrawInteractionSettingsSection(Listing_Standard listingStandard)
		{
			listingStandard.Label("RimTalk_ToddlersExpansion_Interaction_Settings_Header".Translate());
			listingStandard.GapLine();
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Interaction_EnablePrisonerCarry".Translate(),
				ref ToddlersExpansionSettings.EnablePrisonerBabyCarryInteractions,
				"RimTalk_ToddlersExpansion_Interaction_EnablePrisonerCarry_Tooltip".Translate());
			listingStandard.Gap();
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Interaction_EnableChildCarry".Translate(),
				ref ToddlersExpansionSettings.EnableChildBabyCarryInteractions,
				"RimTalk_ToddlersExpansion_Interaction_EnableChildCarry_Tooltip".Translate());
			listingStandard.Gap();
		}


		private static void DrawMishapEventSettingsSection(Listing_Standard listingStandard)
		{
			listingStandard.Label("RimTalk_ToddlersExpansion_Mishap_Settings_Header".Translate());
			listingStandard.GapLine();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Mishap_EnableTumble".Translate(),
				ref ToddlersExpansionSettings.enableToddlerTumble,
				"RimTalk_ToddlersExpansion_Mishap_EnableTumble_Tooltip".Translate());
			listingStandard.Gap();

			if (ToddlersExpansionSettings.enableToddlerTumble)
			{
				listingStandard.Label("RimTalk_ToddlersExpansion_Mishap_TumbleChance".Translate(
					ToddlersExpansionSettings.toddlerTumbleChanceFactor.ToString("0.##")));
				ToddlersExpansionSettings.toddlerTumbleChanceFactor =
					listingStandard.Slider(ToddlersExpansionSettings.toddlerTumbleChanceFactor, 0f, 3f);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_Mishap_TumbleDamageMax".Translate(
					ToddlersExpansionSettings.toddlerTumbleDamageMax));
				ToddlersExpansionSettings.toddlerTumbleDamageMax =
					(int)listingStandard.Slider(ToddlersExpansionSettings.toddlerTumbleDamageMax, 1, 15);
				listingStandard.Gap();
			}

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Mishap_EnableScuffle".Translate(),
				ref ToddlersExpansionSettings.enableToddlerScuffle,
				"RimTalk_ToddlersExpansion_Mishap_EnableScuffle_Tooltip".Translate());
			listingStandard.Gap();

			if (ToddlersExpansionSettings.enableToddlerScuffle)
			{
				listingStandard.Label("RimTalk_ToddlersExpansion_Mishap_ScuffleChance".Translate(
					ToddlersExpansionSettings.toddlerScuffleChanceFactor.ToString("0.##")));
				ToddlersExpansionSettings.toddlerScuffleChanceFactor =
					listingStandard.Slider(ToddlersExpansionSettings.toddlerScuffleChanceFactor, 0f, 3f);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_Mishap_ScuffleDamageMax".Translate(
					ToddlersExpansionSettings.toddlerScuffleDamageMax));
				ToddlersExpansionSettings.toddlerScuffleDamageMax =
					(int)listingStandard.Slider(ToddlersExpansionSettings.toddlerScuffleDamageMax, 1, 15);
				listingStandard.Gap();
			}

			listingStandard.GapLine();
		}
		private static void DrawTalkRequestSettingsSection(Listing_Standard listingStandard)
		{
			listingStandard.Label("RimTalk_ToddlersExpansion_Talk_Settings_Header".Translate());
			listingStandard.GapLine();
			listingStandard.Label("RimTalk_ToddlersExpansion_Talk_Settings_Desc".Translate());
			listingStandard.Gap();
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Talk_EnableSelfPlay".Translate(),
				ref ToddlersExpansionSettings.EnableRimTalkSelfPlayEventTalkRequests,
				"RimTalk_ToddlersExpansion_Talk_EnableSelfPlay_Tooltip".Translate());
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Talk_EnableMutualPlay".Translate(),
				ref ToddlersExpansionSettings.EnableRimTalkMutualPlayEventTalkRequests,
				"RimTalk_ToddlersExpansion_Talk_EnableMutualPlay_Tooltip".Translate());
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Talk_EnableWatchPlay".Translate(),
				ref ToddlersExpansionSettings.EnableRimTalkWatchPlayEventTalkRequests,
				"RimTalk_ToddlersExpansion_Talk_EnableWatchPlay_Tooltip".Translate());
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Talk_EnableCarriedPlay".Translate(),
				ref ToddlersExpansionSettings.EnableRimTalkCarriedPlayEventTalkRequests,
				"RimTalk_ToddlersExpansion_Talk_EnableCarriedPlay_Tooltip".Translate());
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Talk_EnableStruggle".Translate(),
				ref ToddlersExpansionSettings.EnableRimTalkStruggleEventTalkRequests,
				"RimTalk_ToddlersExpansion_Talk_EnableStruggle_Tooltip".Translate());
		}

		private static bool HasAnyOutingPoolEnabled(ToddlersExpansionSettings settings)
		{
			return settings.EnableOutingPoolVanillaEdgeRandom
				|| settings.EnableOutingPoolGrowingZone
				|| settings.EnableOutingPoolStockpileZone
				|| settings.EnableOutingPoolResearchRoom
				|| settings.EnableOutingPoolTempleRoom
				|| settings.EnableOutingPoolKitchenRoom
				|| settings.EnableOutingPoolRecreationRoom
				|| settings.EnableOutingPoolHospitalRoom
				|| settings.EnableOutingPoolOtherNonBedroomRooms
				|| settings.EnableOutingPoolThingWithCompsLandmark
				|| settings.EnableOutingPoolRiver
				|| settings.EnableOutingPoolLake
				|| settings.EnableOutingPoolSnow
				|| settings.EnableOutingPoolCave
				|| settings.EnableOutingPoolSand
				|| settings.EnableOutingPoolAncientRoad;
		}

		public override string SettingsCategory() => "RimTalk_ToddlersExpansion_Settings_Title".Translate();
	}
}
