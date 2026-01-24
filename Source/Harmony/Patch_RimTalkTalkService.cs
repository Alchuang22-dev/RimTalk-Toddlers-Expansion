using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_RimTalkTalkService
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			if (!RimTalkCompatUtility.IsRimTalkActive)
			{
				return;
			}

			Type talkServiceType = AccessTools.TypeByName("RimTalk.Service.TalkService");
			if (talkServiceType == null)
			{
				return;
			}

			MethodInfo target = AccessTools.Method(talkServiceType, "GenerateTalk");
			if (target == null)
			{
				return;
			}

			MethodInfo postfix = AccessTools.Method(typeof(Patch_RimTalkTalkService), nameof(GenerateTalk_Postfix));
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
		}

		private static void GenerateTalk_Postfix(object __0, bool __result)
		{
			if (!__result || __0 == null)
			{
				return;
			}

			if (!RimTalkCompatUtility.TryGetTalkRequestInfo(__0, out Pawn initiator, out Pawn recipient, out string talkTypeName))
			{
				if (Prefs.DevMode)
				{
					Log.Message("[RimTalk_ToddlersExpansion] TalkService.GenerateTalk: failed to read talk request info.");
				}
				return;
			}

			if (!RimTalkCompatUtility.IsUserTalkType(talkTypeName))
			{
				if (Prefs.DevMode)
				{
					Log.Message($"[RimTalk_ToddlersExpansion] TalkService.GenerateTalk: skip non-user talk type ({talkTypeName ?? "null"}).");
				}
				return;
			}

			if (Prefs.DevMode)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] TalkService.GenerateTalk: type={talkTypeName}, initiator={initiator?.LabelShort ?? "null"}, recipient={recipient?.LabelShort ?? "null"}.");
			}

			if (Prefs.DevMode)
			{
				Log.Message("[RimTalk_ToddlersExpansion] TalkService.GenerateTalk: applying talked-to effect to talkRequest.Initiator for user talk.");
			}

			ToddlerTalkRecipientEffects.TryApply(initiator, recipient);
		}
	}
}
