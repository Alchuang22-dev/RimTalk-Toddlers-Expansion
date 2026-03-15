using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.BioTech;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimTalk_ToddlersExpansion.Language;
using System;
using System.Collections.Generic;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.RimTalk
{
	public static class ToddlerContextInjector
	{
		private const string LanguagePrefix = "Language: ";
		private const string PlayPrefix = "Play: ";
		private const string BabyStatePrefix = "Baby state: ";
		private const string CryingDefName = "Crying";
		private const string GigglingDefName = "Giggling";

		public static string InjectToddlerLanguageContext(string context, Pawn pawn)
		{
			bool isToddler = pawn != null && ToddlersCompatUtility.IsToddler(pawn);
			bool isBabyOnly = pawn != null && BiotechCompatUtility.IsBaby(pawn) && !isToddler;
			if (pawn == null || (!isToddler && !isBabyOnly))
			{
				return context;
			}

			context = RewriteBabyMentalStateContext(context, pawn);

			string language = GetToddlerLanguageDescriptor(pawn);
			string play = GetToddlerPlayDescriptor(pawn);
			string babyState = GetBabyStateDescriptor(pawn);
			if (string.IsNullOrEmpty(language) && string.IsNullOrEmpty(play) && string.IsNullOrEmpty(babyState))
			{
				return context;
			}

			string appended = "";
			if (!string.IsNullOrEmpty(language))
			{
				appended = LanguagePrefix + language;
			}

			if (!string.IsNullOrEmpty(play))
			{
				string playLine = PlayPrefix + play;
				appended = string.IsNullOrEmpty(appended) ? playLine : appended + "\n" + playLine;
			}

			if (!string.IsNullOrEmpty(babyState))
			{
				string babyStateLine = BabyStatePrefix + babyState;
				appended = string.IsNullOrEmpty(appended) ? babyStateLine : appended + "\n" + babyStateLine;
			}

			if (string.IsNullOrEmpty(context))
			{
				return appended;
			}

			return context + "\n" + appended;
		}

		public static string GetToddlerLanguageDescriptor(Pawn pawn)
		{
			if (BiotechCompatUtility.IsBaby(pawn) && !ToddlersCompatUtility.IsToddler(pawn))
			{
				return LanguageLevelUtility.GetPromptDescriptor(0f);
			}

			if (!ToddlersCompatUtility.IsToddler(pawn))
			{
				return string.Empty;
			}

			if (!LanguageLevelUtility.TryGetLanguageProgress(pawn, out float progress))
			{
				return string.Empty;
			}

			return LanguageLevelUtility.GetPromptDescriptor(progress);
		}

		public static string GetToddlerPlayDescriptor(Pawn pawn)
		{
			if (!ToddlersCompatUtility.IsToddler(pawn) || pawn?.CurJob?.def == null)
			{
				return string.Empty;
			}

			JobDef jobDef = pawn.CurJob.def;
			if (!jobDef.defName.NullOrEmpty()
				&& jobDef.defName.StartsWith("RimTalk_ToddlerSelfPlay", System.StringComparison.Ordinal))
			{
				return "self";
			}

			if (jobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob)
			{
				return "mutual";
			}

			if (jobDef == ToddlersExpansionJobDefOf.RimTalk_FollowNatureRunner)
			{
				return "outing";
			}

			return string.Empty;
		}

		public static string GetBabyStateDescriptor(Pawn pawn)
		{
			if (!IsBabyFitMentalState(pawn))
			{
				return string.Empty;
			}

			return pawn.MentalStateDef.defName == GigglingDefName
				? "giggling (normal infant behavior, not a mental break)"
				: "crying (normal infant behavior, not a mental break)";
		}

		private static string RewriteBabyMentalStateContext(string context, Pawn pawn)
		{
			if (string.IsNullOrEmpty(context) || !IsBabyFitMentalState(pawn))
			{
				return context;
			}

			string[] split = context.Split(new[] { '\n' }, StringSplitOptions.None);
			var rebuilt = new List<string>(split.Length);
			bool replacedMood = false;

			for (int i = 0; i < split.Length; i++)
			{
				string line = split[i]?.TrimEnd('\r') ?? string.Empty;
				if (!replacedMood && line.StartsWith("Mood:", StringComparison.OrdinalIgnoreCase))
				{
					rebuilt.Add(BuildBabyMoodLine(pawn));
					replacedMood = true;
					continue;
				}

				if (line.IndexOf("be dramatic (mental break)", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					continue;
				}

				rebuilt.Add(line.Replace("(in mental break)", string.Empty).Replace("(mental break)", string.Empty).TrimEnd());
			}

			if (!replacedMood)
			{
				rebuilt.Add(BuildBabyMoodLine(pawn));
			}

			return string.Join("\n", rebuilt).Trim();
		}

		private static string BuildBabyMoodLine(Pawn pawn)
		{
			var mood = pawn?.needs?.mood;
			string moodString = mood?.MoodString ?? "distressed";
			int moodPercent = mood != null ? (int)(mood.CurLevelPercentage * 100f) : 0;
			return $"Mood: {moodString} ({moodPercent}%)";
		}

		private static bool IsBabyFitMentalState(Pawn pawn)
		{
			if (!BiotechCompatUtility.IsBaby(pawn) || pawn?.InMentalState != true || pawn.MentalStateDef == null)
			{
				return false;
			}

			string defName = pawn.MentalStateDef.defName;
			return defName == CryingDefName || defName == GigglingDefName;
		}
	}
}
