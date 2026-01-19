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
			MethodInfo target = infoLevelType != null
				? AccessTools.Method(promptServiceType, "CreatePawnContext", new[] { typeof(Pawn), infoLevelType })
				: AccessTools.Method(promptServiceType, "CreatePawnContext", new[] { typeof(Pawn) });

			if (target == null)
			{
				return;
			}

			MethodInfo postfix = AccessTools.Method(typeof(Patch_RimTalkContextBuilder), nameof(CreatePawnContext_Postfix));
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
		}

		private static void CreatePawnContext_Postfix(Pawn pawn, ref string __result)
		{
			if (pawn == null)
			{
				return;
			}

			__result = ToddlerContextInjector.InjectToddlerLanguageContext(__result, pawn);
		}
	}
}
