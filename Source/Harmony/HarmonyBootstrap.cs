using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.YayoAnimation;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class HarmonyBootstrap
	{
		private static bool _initialized;

		public static void Init()
		{
			if (_initialized)
			{
				return;
			}

			_initialized = true;
			var harmony = new HarmonyLib.Harmony("cj.rimtalk.toddlers");
			Patch_RimTalkContextBuilder.Init(harmony);
			Patch_RimTalkTalkService.Init(harmony);
			Patch_ToddlersWashBaby.Init(harmony);
			Patch_ToddlersWashBabyBathRules.Init(harmony);
			Patch_ToddlersPlayInCribReservation.Init(harmony);
			Patch_BiotechSharedBedroomThoughts.Init(harmony);
			Patch_PawnGroupMakerUtility.Init(harmony);
			Patch_FloatMenu_ToddlerToyPlay.Init(harmony);
			Patch_TravelingLord.Init(harmony);
			// Patch 商队离开时的 duty 分配，确保被背着的幼儿不会获得不合适的 duty
			Patch_ExitMapDuty.Init(harmony);
			
			Patch_ToddlerPrisonerThinkTree.Init(harmony);
			Patch_LearningGiver_NatureRunning.Init(harmony);
			Patch_ToddlerJobLogging.Init(harmony);
			// 幼儿洗澡渲染补丁（脱衣服、浴缸中位置调整）
			Patch_ToddlerBathRendering.Init(harmony);
			
			// 幼儿背负系统补丁
			Patch_ToddlerCarrying.Init(harmony);
			
			// 幼儿无聊机制补丁
			Patch_ToddlerBoredom.ApplyPatches(harmony);
			Patch_ToddlerEatingSpeed.Init(harmony);
			Patch_ToddlerEatingSafety.Init(harmony);
			
			// Yayo's Animation 兼容性初始化和补丁
			YayoAnimationCompatUtility.Initialize();
			YayoAnimationCompatUtility.ApplyPatches(harmony);
			
			Log.Message("[RimTalk Toddlers Expansion] All patches applied");
		}
	}
}
