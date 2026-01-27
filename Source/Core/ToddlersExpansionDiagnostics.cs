using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	internal static class ToddlersExpansionDiagnostics
	{
		private static bool _ran;

		public static void Run()
		{
			if (_ran)
			{
				return;
			}

			_ran = true;

			int missingDefs = 0;
			int missingTextures = 0;

			missingDefs += CheckDef<JobDef>("RimTalk_ToddlerSelfPlayJob");
			missingDefs += CheckDef<JobDef>("RimTalk_ToddlerMutualPlayJob");
			missingDefs += CheckDef<JobDef>("RimTalk_ToddlerMutualPlayPartnerJob");
			missingDefs += CheckDef<JobDef>("RimTalk_WatchToddlerPlayJob");
			missingDefs += CheckDef<JobDef>("RimTalk_ToddlerPlayAtToy");
			missingDefs += CheckDef<JoyGiverDef>("RimTalk_WatchToddlerPlayJoy");
			missingDefs += CheckDef<JoyGiverDef>("RimTalk_ToddlerToyPlayJoy");
			missingDefs += CheckDef<ThingDef>("RimTalk_ToyBlockPile");
			missingDefs += CheckDef<ThingDef>("RimTalk_ToyRockingHorse");
			missingDefs += CheckDef<ThingDef>("RimTalk_ToyPuzzleTable");
			missingDefs += CheckDef<HediffDef>("RimTalk_ToddlerLanguageLearning");
			missingDefs += CheckDef<HediffDef>("RimTalk_BabyBabbling");
			missingDefs += CheckDef<ThoughtDef>("RimTalk_MyBabyNearby");
			missingDefs += CheckDef<JobDef>("RimTalk_BeingCarried_Observe");
			missingDefs += CheckDef<JobDef>("RimTalk_BeingCarried_Sleep");
			missingDefs += CheckDef<JobDef>("RimTalk_BeingCarried_Idle");
			missingDefs += CheckDef<JobDef>("RimTalk_BeingCarried_Struggle");
			missingDefs += CheckDef<MentalStateDef>("RimTalk_WantToBeHeld");
			missingDefs += CheckDef<ThoughtDef>("RimTalk_TalkedToBaby");
			missingDefs += CheckDef<ThoughtDef>("RimTalk_ToddlerSleepAlone");
			missingDefs += CheckDef<ThoughtDef>("RimTalk_ToddlerSleepWithOthers");
			missingDefs += CheckDef<ThoughtDef>("RimTalk_ToddlerSleepInNursery");
			missingDefs += CheckDef<ThoughtDef>("RimTalk_ToddlerSleepWithParents");
			missingDefs += CheckDef<AnimationDef>("RimTalk_ToddlerPlay_Wiggle");
			missingDefs += CheckDef<AnimationDef>("RimTalk_ToddlerPlay_Sway");
			missingDefs += CheckDef<AnimationDef>("RimTalk_ToddlerPlay_Lay");
			missingDefs += CheckDef<AnimationDef>("RimTalk_ToddlerPlay_Crawl");

			missingTextures += CheckMultiTexture("Things/Building/ToddlerToys/toy_block_pile");
			missingTextures += CheckMultiTexture("Things/Building/ToddlerToys/toy_rocking_horse");
			missingTextures += CheckMultiTexture("Things/Building/ToddlerToys/toy_puzzle_table");

			if (missingDefs == 0 && missingTextures == 0)
			{
				Log.Message("[RimTalk_ToddlersExpansion] Diagnostics: defs and textures OK.");
			}
			else
			{
				Log.Warning($"[RimTalk_ToddlersExpansion] Diagnostics: missingDefs={missingDefs}, missingTextures={missingTextures}.");
			}
		}

		private static int CheckDef<T>(string defName) where T : Def
		{
			if (DefDatabase<T>.GetNamedSilentFail(defName) == null)
			{
				Log.Warning($"[RimTalk_ToddlersExpansion] Missing {typeof(T).Name} def: {defName}");
				return 1;
			}

			return 0;
		}

		private static int CheckMultiTexture(string path)
		{
			if (ContentFinder<Texture2D>.Get(path + "_north", false) == null
				|| ContentFinder<Texture2D>.Get(path + "_east", false) == null
				|| ContentFinder<Texture2D>.Get(path + "_south", false) == null
				|| ContentFinder<Texture2D>.Get(path + "_west", false) == null)
			{
				Log.Warning($"[RimTalk_ToddlersExpansion] Missing multi-texture set: {path}_north/east/south/west");
				return 1;
			}

			return 0;
		}
	}
}
