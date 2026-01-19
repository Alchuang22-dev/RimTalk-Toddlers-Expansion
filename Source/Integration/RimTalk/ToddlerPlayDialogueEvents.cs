using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimTalk_ToddlersExpansion.Language;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.RimTalk
{
	public static class ToddlerPlayDialogueEvents
	{
		private const string TalkType = "Event";

		public static void OnToddlerSelfPlayCompleted(Pawn toddler, Job job, Map map)
		{
			if (!RimTalkCompatUtility.IsRimTalkActive || toddler == null)
			{
				return;
			}

			string prompt = BuildPrompt("self-play", toddler, null, null);
			RimTalkCompatUtility.TryQueueTalk(toddler, null, prompt, TalkType);
		}

		public static void OnToddlerMutualPlayCompleted(Pawn toddlerA, Pawn toddlerB, Job job, Map map)
		{
			if (!RimTalkCompatUtility.IsRimTalkActive || toddlerA == null || toddlerB == null)
			{
				return;
			}

			string prompt = BuildPrompt("mutual play", toddlerA, toddlerB, null);
			RimTalkCompatUtility.TryQueueTalk(toddlerA, toddlerB, prompt, TalkType);
		}

		public static void OnAdultWatchToddlerPlay(Pawn adult, Pawn toddler, Job job, Map map)
		{
			if (!RimTalkCompatUtility.IsRimTalkActive || adult == null || toddler == null)
			{
				return;
			}

			string prompt = BuildPrompt("watching toddler play", toddler, adult, null);
			RimTalkCompatUtility.TryQueueTalk(adult, toddler, prompt, TalkType);
		}

		private static string BuildPrompt(string activity, Pawn toddler, Pawn other, Pawn watcher)
		{
			string language = LanguageLevelUtility.TryGetLanguageProgress(toddler, out float progress)
				? LanguageLevelUtility.GetPromptDescriptor(progress)
				: "";
			string play = ToddlersCompatUtility.IsCurrentlyPlaying(toddler) ? "playing" : "";

			string prompt = $"Toddler event: {activity}.";
			if (!string.IsNullOrEmpty(play))
			{
				prompt += $" Play={play}.";
			}

			if (!string.IsNullOrEmpty(language))
			{
				prompt += $" Speech={language}.";
			}

			if (other != null)
			{
				prompt += $" Other={other.LabelShort}.";
			}

			return prompt;
		}
	}
}
