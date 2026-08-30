using System;
using System.Collections.Generic;
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

			PatchBuildMessagesParticipantCapture(harmony);
		}

		private static void PatchBuildMessagesParticipantCapture(HarmonyLib.Harmony harmony)
		{
			Type promptManagerType = AccessTools.TypeByName("RimTalk.Prompt.PromptManager");
			if (promptManagerType == null)
			{
				return;
			}

			MethodInfo buildMessages = AccessTools.Method(promptManagerType, "BuildMessages");
			if (buildMessages == null)
			{
				return;
			}

			MethodInfo prefix = AccessTools.Method(typeof(Patch_RimTalkTalkService), nameof(BuildMessages_Prefix));
			harmony.Patch(buildMessages, prefix: new HarmonyMethod(prefix));
		}

		private static void BuildMessages_Prefix(object __0, object __1)
		{
			if (RimTalkCompatUtility.TryGetTalkRequestInfo(
				__0,
				out Pawn _,
				out Pawn _,
				out string talkTypeName)
				&& RimTalkCompatUtility.IsAnnouncementTalkType(talkTypeName))
			{
				RimTalkCompatUtility.TryCaptureTalkRequestParticipants(__0, __1);
			}
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

			if (RimTalkCompatUtility.IsAnnouncementTalkType(talkTypeName))
			{
				ApplyAnnouncementListenerEffects(__0, initiator, recipient);
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

		private static void ApplyAnnouncementListenerEffects(object talkRequest, Pawn initiator, Pawn recipient)
		{
			if (!RimTalkCompatUtility.TryGetAnnouncementListeners(
				talkRequest,
				initiator,
				recipient,
				out Pawn speaker,
				out List<Pawn> listeners))
			{
				if (Prefs.DevMode)
				{
					Log.Message("[RimTalk_ToddlersExpansion] Announcement generated with no eligible listeners for toddler effects.");
				}
				return;
			}

			int toddlerListenerCount = 0;
			foreach (Pawn listener in listeners)
			{
				if (!ToddlersCompatUtility.IsToddlerOrBaby(listener))
				{
					continue;
				}

				ToddlerTalkRecipientEffects.TryApply(listener, speaker);
				toddlerListenerCount++;
			}

			if (Prefs.DevMode)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] Announcement listener effects processed: speaker={speaker.LabelShort}, listeners={listeners.Count}, toddlerListeners={toddlerListenerCount}.");
			}
		}
	}
}
