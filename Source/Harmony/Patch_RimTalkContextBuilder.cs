using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_RimTalkContextBuilder
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			if (!RimTalkCompatUtility.IsRimTalkActive)
			{
				return;
			}

			Type promptServiceType = AccessTools.TypeByName("RimTalk.Service.PromptService");
			if (promptServiceType == null)
			{
				return;
			}

			Type infoLevelType = promptServiceType.GetNestedType("InfoLevel", BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo pawnContextTarget = infoLevelType != null
				? AccessTools.Method(promptServiceType, "CreatePawnContext", new[] { typeof(Pawn), infoLevelType })
				: AccessTools.Method(promptServiceType, "CreatePawnContext", new[] { typeof(Pawn) });

			if (pawnContextTarget != null)
			{
				MethodInfo postfix = AccessTools.Method(typeof(Patch_RimTalkContextBuilder), nameof(CreatePawnContext_Postfix));
				harmony.Patch(pawnContextTarget, postfix: new HarmonyMethod(postfix));
			}

			MethodInfo listenerContextTarget = AccessTools.Method(promptServiceType, "CreateMinimalListenerContext", new[] { typeof(Pawn) });
			if (listenerContextTarget != null)
			{
				MethodInfo listenerPostfix = AccessTools.Method(typeof(Patch_RimTalkContextBuilder), nameof(CreateMinimalListenerContext_Postfix));
				harmony.Patch(listenerContextTarget, postfix: new HarmonyMethod(listenerPostfix));
			}
		}

		private static void CreatePawnContext_Postfix(Pawn pawn, ref string __result)
		{
			if (pawn == null)
			{
				return;
			}

			__result = ToddlerContextInjector.InjectToddlerLanguageContext(__result, pawn);
		}

		private static void CreateMinimalListenerContext_Postfix(Pawn pawn, ref string __result)
		{
			if (pawn == null)
			{
				return;
			}

			__result = ToddlerContextInjector.InjectToddlerListenerContext(__result, pawn);
		}
	}
}
