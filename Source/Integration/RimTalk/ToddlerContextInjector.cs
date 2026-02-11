using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.BioTech;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimTalk_ToddlersExpansion.Language;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.RimTalk
{
	public static class ToddlerContextInjector
	{
		private const string LanguagePrefix = "Language: ";
		private const string PlayPrefix = "Play: ";

		public static string InjectToddlerLanguageContext(string context, Pawn pawn)
		{
			bool isToddler = pawn != null && ToddlersCompatUtility.IsToddler(pawn);
			bool isBabyOnly = pawn != null && BiotechCompatUtility.IsBaby(pawn) && !isToddler;
			if (pawn == null || (!isToddler && !isBabyOnly))
			{
				return context;
			}

			string language = GetToddlerLanguageDescriptor(pawn);
			string play = GetToddlerPlayDescriptor(pawn);
			if (string.IsNullOrEmpty(language) && string.IsNullOrEmpty(play))
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
	}
}
