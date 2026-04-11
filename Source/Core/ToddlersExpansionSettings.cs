using System;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public sealed class ToddlersExpansionSettings : ModSettings
	{
		public int SettingsPageIndex = 0;
		public bool UseStandaloneLlmApi = false;
		public ToddlersExpansionStandaloneApiConfig StandaloneApi = new ToddlersExpansionStandaloneApiConfig();

		// Caravan / visitor generation settings
		public bool EnableCaravanToddlerGeneration = true;
		public int MaxToddlersPerGroup = 3;
		public int MaxChildrenPerGroup = 2;
		public float ToddlerGenerationChance = 0.7f;
		public float ChildGenerationChance = 0.6f;
		public int MinBatchCount = 1;
		public int MaxBatchCount = 3;
		public float ExtraBatchChance = 0.3f;

		// Boredom settings
		public static bool enableBoredomSystem = true;
		public static float boredomIncreasePerActivity = 0.05f;
		public static float boredomMaxCap = 0.70f;
		public static float boredomDailyRecoveryRate = 0.07f;
		public static bool enableAutoDetection = true;

		// Language learning settings
		public static float learningFactor_Talking = 0.8f;
		public static int newbornToToddlerDays = 60;

		// Toddler eating speed settings
		public static float toddlerEatingSpeedFactor = 1f;

		// Behavior settings
		public static bool enableHostileToddlerColonistBehavior = true;
		public static bool enableUnder3HairRendering = false;
		public static bool babyCryAffectsMoodOnly = true;
		public static bool babyCryAffectsMood = true;
		public static bool MuteSpamDebugLogs = true;
		public static bool MuteAllLogs = false;
		public static bool EnablePrisonerBabyCarryInteractions = false;
		public static bool EnableChildBabyCarryInteractions = false;
		public static int MutualPlayPartnerCheckIntervalTicks = 1;
		public static int ToddlerMainLoopCheckIntervalTicks = 1;
		public static int BabyCarryCheckIntervalTicks = 120;

		// Nature running / children outing destination pools
		public bool EnableOutingPoolVanillaEdgeRandom = true;
		public bool EnableOutingPoolGrowingZone = true;
		public bool EnableOutingPoolStockpileZone = true;
		public bool EnableOutingPoolResearchRoom = true;
		public bool EnableOutingPoolTempleRoom = true;
		public bool EnableOutingPoolKitchenRoom = true;
		public bool EnableOutingPoolRecreationRoom = true;
		public bool EnableOutingPoolHospitalRoom = true;
		public bool EnableOutingPoolOtherNonBedroomRooms = true;
		public bool EnableOutingPoolThingWithCompsLandmark = true;
		public bool EnableOutingPoolRiver = true;
		public bool EnableOutingPoolLake = true;
		public bool EnableOutingPoolSnow = true;
		public bool EnableOutingPoolCave = true;
		public bool EnableOutingPoolSand = true;
		public bool EnableOutingPoolAncientRoad = true;

		// Toddler mishap event settings
		public static bool enableToddlerTumble = true;
		public static float toddlerTumbleChanceFactor = 1f;
		public static int toddlerTumbleDamageMax = 5;
		public static bool enableToddlerScuffle = true;
		public static float toddlerScuffleChanceFactor = 1f;
		public static int toddlerScuffleDamageMax = 2;

		// Play animation pool settings
		public static bool EnableNewbornPlayAnimations = true;

		// RimTalk event talk settings
		public static bool EnableRimTalkSelfPlayEventTalkRequests = true;
		public static bool EnableRimTalkMutualPlayEventTalkRequests = true;
		public static bool EnableRimTalkWatchPlayEventTalkRequests = true;
		public static bool EnableRimTalkCarriedPlayEventTalkRequests = true;
		public static bool EnableRimTalkStruggleEventTalkRequests = true;

		public static bool EnableNativePlayWiggle = true;
		public static bool EnableNativePlaySway = true;
		public static bool EnableNativePlayLay = true;
		public static bool EnableNativePlayCrawl = true;
		public static bool EnableNativePlayToddlerWobble = true;

		public static bool EnableYayoPlayToys = true;
		public static bool EnableYayoPlayHoopstone = true;
		public static bool EnableYayoPlayDartsBoard = true;
		public static bool EnableYayoGoldenCube = true;
		public static bool EnableYayoSocialRelax = true;
		public static bool EnableYayoBabyRoll = true;
		public static bool EnableYayoCustomRoll = true;
		public static bool EnableYayoCustomSpin = true;
		public static bool EnableYayoCustomHop = true;
		public static bool EnableYayoCustomRunLoop = true;

		public static bool ShouldEmitVerboseDebugLogs => Prefs.DevMode && !MuteAllLogs && !MuteSpamDebugLogs;

		public static bool ShouldSuppressModLogMessage(string text)
		{
			if (!MuteAllLogs || string.IsNullOrEmpty(text))
			{
				return false;
			}

			return text.Contains("[RimTalk_ToddlersExpansion]")
				|| text.Contains("[RimTalk Toddlers Expansion]")
				|| text.Contains("[RimTalk Toddlers]")
				|| text.Contains("[RimTalk Boredom]");
		}

		public static int GetMutualPlayPartnerCheckIntervalTicks()
		{
			return ClampInterval(MutualPlayPartnerCheckIntervalTicks, 1, 600);
		}

		public static int GetToddlerMainLoopCheckIntervalTicks()
		{
			return ClampInterval(ToddlerMainLoopCheckIntervalTicks, 1, 120);
		}

		public static int GetBabyCarryPickupCheckIntervalTicks()
		{
			return ClampInterval(BabyCarryCheckIntervalTicks, 30, 600);
		}

		public static int GetBabyCarryDesireCheckIntervalTicks()
		{
			return GetBabyCarryPickupCheckIntervalTicks() * 5;
		}

		public static int GetNewbornToToddlerDays()
		{
			return Math.Max(1, Math.Min(newbornToToddlerDays, 179));
		}

		public static float GetNewbornToToddlerYears()
		{
			return GetNewbornToToddlerDays() / 60f;
		}

		private static int ClampInterval(int value, int min, int max)
		{
			return Math.Max(min, Math.Min(max, value));
		}

		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref SettingsPageIndex, "SettingsPageIndex", 0);
			Scribe_Values.Look(ref UseStandaloneLlmApi, "UseStandaloneLlmApi", false);
			Scribe_Deep.Look(ref StandaloneApi, "StandaloneApi");

			Scribe_Values.Look(ref EnableCaravanToddlerGeneration, "EnableCaravanToddlerGeneration", true);
			Scribe_Values.Look(ref MaxToddlersPerGroup, "MaxToddlersPerGroup", 3);
			Scribe_Values.Look(ref MaxChildrenPerGroup, "MaxChildrenPerGroup", 2);
			Scribe_Values.Look(ref ToddlerGenerationChance, "ToddlerGenerationChance", 0.7f);
			Scribe_Values.Look(ref ChildGenerationChance, "ChildGenerationChance", 0.6f);
			Scribe_Values.Look(ref MinBatchCount, "MinBatchCount", 1);
			Scribe_Values.Look(ref MaxBatchCount, "MaxBatchCount", 3);
			Scribe_Values.Look(ref ExtraBatchChance, "ExtraBatchChance", 0.3f);

			Scribe_Values.Look(ref enableBoredomSystem, "enableBoredomSystem", true);
			Scribe_Values.Look(ref boredomIncreasePerActivity, "boredomIncreasePerActivity", 0.05f);
			Scribe_Values.Look(ref boredomMaxCap, "boredomMaxCap", 0.70f);
			Scribe_Values.Look(ref boredomDailyRecoveryRate, "boredomDailyRecoveryRate", 0.07f);
			Scribe_Values.Look(ref enableAutoDetection, "enableAutoDetection", true);

			Scribe_Values.Look(ref learningFactor_Talking, "learningFactor_Talking", 0.8f);
			Scribe_Values.Look(ref newbornToToddlerDays, "newbornToToddlerDays", 60);
			Scribe_Values.Look(ref toddlerEatingSpeedFactor, "toddlerEatingSpeedFactor", 1f);

			Scribe_Values.Look(ref enableHostileToddlerColonistBehavior, "enableHostileToddlerColonistBehavior", true);
			Scribe_Values.Look(ref enableUnder3HairRendering, "enableUnder3HairRendering", false);
			Scribe_Values.Look(ref babyCryAffectsMoodOnly, "babyCryAffectsMoodOnly", true);
			Scribe_Values.Look(ref babyCryAffectsMood, "babyCryAffectsMood", true);
			Scribe_Values.Look(ref MuteSpamDebugLogs, "MuteSpamDebugLogs", true);
			Scribe_Values.Look(ref MuteAllLogs, "MuteAllLogs", false);
			Scribe_Values.Look(ref EnablePrisonerBabyCarryInteractions, "EnablePrisonerBabyCarryInteractions", false);
			Scribe_Values.Look(ref EnableChildBabyCarryInteractions, "EnableChildBabyCarryInteractions", false);
			Scribe_Values.Look(ref MutualPlayPartnerCheckIntervalTicks, "MutualPlayPartnerCheckIntervalTicks", 1);
			Scribe_Values.Look(ref ToddlerMainLoopCheckIntervalTicks, "ToddlerMainLoopCheckIntervalTicks", 1);
			Scribe_Values.Look(ref BabyCarryCheckIntervalTicks, "BabyCarryCheckIntervalTicks", 120);

			Scribe_Values.Look(ref enableToddlerTumble, "enableToddlerTumble", true);
			Scribe_Values.Look(ref toddlerTumbleChanceFactor, "toddlerTumbleChanceFactor", 1f);
			Scribe_Values.Look(ref toddlerTumbleDamageMax, "toddlerTumbleDamageMax", 5);
			Scribe_Values.Look(ref enableToddlerScuffle, "enableToddlerScuffle", true);
			Scribe_Values.Look(ref toddlerScuffleChanceFactor, "toddlerScuffleChanceFactor", 1f);
			Scribe_Values.Look(ref toddlerScuffleDamageMax, "toddlerScuffleDamageMax", 2);

			Scribe_Values.Look(ref EnableOutingPoolVanillaEdgeRandom, "EnableOutingPoolVanillaEdgeRandom", true);
			Scribe_Values.Look(ref EnableOutingPoolGrowingZone, "EnableOutingPoolGrowingZone", true);
			Scribe_Values.Look(ref EnableOutingPoolStockpileZone, "EnableOutingPoolStockpileZone", true);
			Scribe_Values.Look(ref EnableOutingPoolResearchRoom, "EnableOutingPoolResearchRoom", true);
			Scribe_Values.Look(ref EnableOutingPoolTempleRoom, "EnableOutingPoolTempleRoom", true);
			Scribe_Values.Look(ref EnableOutingPoolKitchenRoom, "EnableOutingPoolKitchenRoom", true);
			Scribe_Values.Look(ref EnableOutingPoolRecreationRoom, "EnableOutingPoolRecreationRoom", true);
			Scribe_Values.Look(ref EnableOutingPoolHospitalRoom, "EnableOutingPoolHospitalRoom", true);
			Scribe_Values.Look(ref EnableOutingPoolOtherNonBedroomRooms, "EnableOutingPoolOtherNonBedroomRooms", true);
			Scribe_Values.Look(ref EnableOutingPoolThingWithCompsLandmark, "EnableOutingPoolThingWithCompsLandmark", true);
			Scribe_Values.Look(ref EnableOutingPoolRiver, "EnableOutingPoolRiver", true);
			Scribe_Values.Look(ref EnableOutingPoolLake, "EnableOutingPoolLake", true);
			Scribe_Values.Look(ref EnableOutingPoolSnow, "EnableOutingPoolSnow", true);
			Scribe_Values.Look(ref EnableOutingPoolCave, "EnableOutingPoolCave", true);
			Scribe_Values.Look(ref EnableOutingPoolSand, "EnableOutingPoolSand", true);
			Scribe_Values.Look(ref EnableOutingPoolAncientRoad, "EnableOutingPoolAncientRoad", true);

			Scribe_Values.Look(ref EnableNewbornPlayAnimations, "EnableNewbornPlayAnimations", true);
			Scribe_Values.Look(ref EnableRimTalkSelfPlayEventTalkRequests, "EnableRimTalkSelfPlayEventTalkRequests", true);
			Scribe_Values.Look(ref EnableRimTalkMutualPlayEventTalkRequests, "EnableRimTalkMutualPlayEventTalkRequests", true);
			Scribe_Values.Look(ref EnableRimTalkWatchPlayEventTalkRequests, "EnableRimTalkWatchPlayEventTalkRequests", true);
			Scribe_Values.Look(ref EnableRimTalkCarriedPlayEventTalkRequests, "EnableRimTalkCarriedPlayEventTalkRequests", true);
			Scribe_Values.Look(ref EnableRimTalkStruggleEventTalkRequests, "EnableRimTalkStruggleEventTalkRequests", true);

			Scribe_Values.Look(ref EnableNativePlayWiggle, "EnableNativePlayWiggle", true);
			Scribe_Values.Look(ref EnableNativePlaySway, "EnableNativePlaySway", true);
			Scribe_Values.Look(ref EnableNativePlayLay, "EnableNativePlayLay", true);
			Scribe_Values.Look(ref EnableNativePlayCrawl, "EnableNativePlayCrawl", true);
			Scribe_Values.Look(ref EnableNativePlayToddlerWobble, "EnableNativePlayToddlerWobble", true);

			Scribe_Values.Look(ref EnableYayoPlayToys, "EnableYayoPlayToys", true);
			Scribe_Values.Look(ref EnableYayoPlayHoopstone, "EnableYayoPlayHoopstone", true);
			Scribe_Values.Look(ref EnableYayoPlayDartsBoard, "EnableYayoPlayDartsBoard", true);
			Scribe_Values.Look(ref EnableYayoGoldenCube, "EnableYayoGoldenCube", true);
			Scribe_Values.Look(ref EnableYayoSocialRelax, "EnableYayoSocialRelax", true);
			Scribe_Values.Look(ref EnableYayoBabyRoll, "EnableYayoBabyRoll", true);
			Scribe_Values.Look(ref EnableYayoCustomRoll, "EnableYayoCustomRoll", true);
			Scribe_Values.Look(ref EnableYayoCustomSpin, "EnableYayoCustomSpin", true);
			Scribe_Values.Look(ref EnableYayoCustomHop, "EnableYayoCustomHop", true);
			Scribe_Values.Look(ref EnableYayoCustomRunLoop, "EnableYayoCustomRunLoop", true);

			if (StandaloneApi == null)
			{
				StandaloneApi = new ToddlersExpansionStandaloneApiConfig();
			}
		}
	}
}
