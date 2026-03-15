using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.YayoAnimation;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlersLocomotionAnimation
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			try
			{
				Type animationUtilityType = AccessTools.TypeByName("Toddlers.AnimationUtility");
				MethodInfo target = AccessTools.Method(animationUtilityType, "SetLocomotionAnimation", new[] { typeof(Pawn), typeof(AnimationDef) });
				if (target == null)
				{
					return;
				}

				harmony.Patch(target, prefix: new HarmonyMethod(typeof(Patch_ToddlersLocomotionAnimation), nameof(SetLocomotionAnimation_Prefix)));
			}
			catch (Exception ex)
			{
				Log.Warning($"[RimTalk_ToddlersExpansion] Failed to patch Toddlers locomotion animation compatibility: {ex.Message}");
			}
		}

		private static bool SetLocomotionAnimation_Prefix(Pawn pawn, AnimationDef animation)
		{
			return !YayoAnimationCompatUtility.ShouldAllowManagedPlayAnimation(pawn);
		}
	}
}
