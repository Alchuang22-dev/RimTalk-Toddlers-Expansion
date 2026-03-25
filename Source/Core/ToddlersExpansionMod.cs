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
				ToddlersExpansionDiagnostics.Run();
				RimTalkCompatUtility.TryRegisterToddlerVariables();
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
			float contentHeight = 1500f;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

			Widgets.BeginScrollView(inRect, ref generalScrollPosition, viewRect);
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(viewRect);

			DrawLlmSettingsSection(listingStandard, settings);
			listingStandard.GapLine();

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

			listingStandard.Label("RimTalk_ToddlersExpansion_Feeding_Settings_Header".Translate());
			listingStandard.GapLine();

			listingStandard.Label("RimTalk_ToddlersExpansion_ToddlerEatingSpeed".Translate(ToddlersExpansionSettings.toddlerEatingSpeedFactor.ToString("0.##")));
			ToddlersExpansionSettings.toddlerEatingSpeedFactor = listingStandard.Slider(ToddlersExpansionSettings.toddlerEatingSpeedFactor, 0.1f, 10f);
			listingStandard.Gap();

			listingStandard.GapLine();

			listingStandard.Label("RimTalk_ToddlersExpansion_Behavior_Settings_Header".Translate());
			listingStandard.GapLine();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_EnableHostileToddlerColonistBehavior".Translate(),
				ref ToddlersExpansionSettings.enableHostileToddlerColonistBehavior,
				"RimTalk_ToddlersExpansion_EnableHostileToddlerColonistBehavior_Tooltip".Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_EnableUnder3HairRendering".Translate(),
				ref ToddlersExpansionSettings.enableUnder3HairRendering,
				"RimTalk_ToddlersExpansion_EnableUnder3HairRendering_Tooltip".Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_BabyCryMoodOnly".Translate(),
				ref ToddlersExpansionSettings.babyCryAffectsMoodOnly,
				"RimTalk_ToddlersExpansion_BabyCryMoodOnly_Tooltip".Translate());
			listingStandard.Gap();

			listingStandard.GapLine();

			listingStandard.Label("RimTalk_Caravan_Settings_Header".Translate());
			listingStandard.GapLine();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_EnableCaravanToddlerGeneration".Translate(),
				ref settings.EnableCaravanToddlerGeneration,
				"RimTalk_ToddlersExpansion_EnableCaravanToddlerGeneration_Tooltip".Translate());
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
			float contentHeight = 980f;
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

		private void DrawTalkSettingsPage(Rect inRect)
		{
			float contentHeight = 320f;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

			Widgets.BeginScrollView(inRect, ref talkScrollPosition, viewRect);
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(viewRect);

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

			listingStandard.End();
			Widgets.EndScrollView();
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
