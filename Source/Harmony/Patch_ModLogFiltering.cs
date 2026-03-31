using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ModLogFiltering
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			PatchStringMethod(harmony, nameof(Log.Message), typeof(Patch_ModLogFiltering), nameof(AllowStringLog_Prefix));
			PatchStringMethod(harmony, nameof(Log.Warning), typeof(Patch_ModLogFiltering), nameof(AllowStringLog_Prefix));
			PatchStringMethod(harmony, nameof(Log.Error), typeof(Patch_ModLogFiltering), nameof(AllowStringLog_Prefix));

			MethodInfo warningOnce = AccessTools.Method(typeof(Log), nameof(Log.WarningOnce), new[] { typeof(string), typeof(int) });
			if (warningOnce != null)
			{
				harmony.Patch(warningOnce, prefix: new HarmonyMethod(typeof(Patch_ModLogFiltering), nameof(AllowStringOnceLog_Prefix)));
			}

			MethodInfo errorOnce = AccessTools.Method(typeof(Log), nameof(Log.ErrorOnce), new[] { typeof(string), typeof(int) });
			if (errorOnce != null)
			{
				harmony.Patch(errorOnce, prefix: new HarmonyMethod(typeof(Patch_ModLogFiltering), nameof(AllowStringOnceLog_Prefix)));
			}
		}

		private static void PatchStringMethod(HarmonyLib.Harmony harmony, string methodName, System.Type patchType, string prefixName)
		{
			MethodInfo target = AccessTools.Method(typeof(Log), methodName, new[] { typeof(string) });
			if (target != null)
			{
				harmony.Patch(target, prefix: new HarmonyMethod(patchType, prefixName));
			}
		}

		private static bool AllowStringLog_Prefix(string text)
		{
			return !ToddlersExpansionSettings.ShouldSuppressModLogMessage(text);
		}

		private static bool AllowStringOnceLog_Prefix(string text, int key)
		{
			return !ToddlersExpansionSettings.ShouldSuppressModLogMessage(text);
		}
	}
}
