using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.RimTalk
{
	public enum ToddlerPlayReportKind
	{
		SelfPlay,
		MutualPlay
	}

	public static class ToddlerPlayReportUtility
	{
		private const int RequestCooldownTicks = 12000;
		private const int ContextMaxChars = 180;

		private const string OutputRules = "Output only the activity phrase, 1-4 words, no punctuation, no quotes.";

		private static readonly object LockObj = new object();
		private static readonly Dictionary<int, int> PendingJobTicks = new Dictionary<int, int>(64);
		private static readonly Dictionary<int, string> PendingReports = new Dictionary<int, string>(64);
		private static readonly Dictionary<int, int> LastRequestTickByPawn = new Dictionary<int, int>(64);

		public static void EnsureReportRequested(Job job, Pawn toddler, Pawn partner, ToddlerPlayReportKind kind)
		{
			if (job == null || toddler == null || !RimTalkCompatUtility.IsRimTalkActive)
			{
				return;
			}

			if (!string.IsNullOrWhiteSpace(job.reportStringOverride))
			{
				return;
			}

			int jobId = job.loadID;
			if (jobId < 0 || !TryRegisterPending(jobId, toddler))
			{
				return;
			}

			string nearby = RimTalkCompatUtility.TryGetNearbyContextText(toddler, out string contextText)
				? contextText
				: null;
			string trimmed = TrimContext(nearby);
			string partnerLabel = partner?.LabelShort ?? "RimTalk_ToddlersExpansion_PlayReport_AnotherToddler".Translate();
			string prefix = BuildPrefix(kind, partnerLabel);

			string systemPrompt = BuildSystemPrompt(kind);
			string userPrompt = BuildUserPrompt(kind, trimmed, partnerLabel);

			bool queued = RimTalkCompatUtility.TryRequestShortText(systemPrompt, userPrompt, response =>
			{
				string sanitized = SanitizeResponse(response);
				if (string.IsNullOrEmpty(sanitized))
				{
					ClearPending(jobId);
					return;
				}

				string report = string.IsNullOrEmpty(prefix) ? sanitized : prefix + sanitized;
				lock (LockObj)
				{
					if (!PendingJobTicks.ContainsKey(jobId))
					{
						return;
					}

					PendingJobTicks.Remove(jobId);
					PendingReports[jobId] = report;
				}
			});

			if (!queued)
			{
				ClearPending(jobId);
			}
		}

		public static void TryApplyPendingReport(Job job)
		{
			if (job == null)
			{
				return;
			}

			int jobId = job.loadID;
			if (jobId < 0)
			{
				return;
			}

			string report;
			lock (LockObj)
			{
				if (!PendingReports.TryGetValue(jobId, out report))
				{
					return;
				}

				PendingReports.Remove(jobId);
			}

			if (!string.IsNullOrWhiteSpace(report))
			{
				job.reportStringOverride = report;
			}
		}

		public static void CancelJob(Job job)
		{
			if (job == null)
			{
				return;
			}

			ClearPending(job.loadID);
		}

		private static bool TryRegisterPending(int jobId, Pawn toddler)
		{
			if (jobId < 0)
			{
				return false;
			}

			int now = Find.TickManager.TicksGame;
			lock (LockObj)
			{
				if (PendingJobTicks.ContainsKey(jobId) || PendingReports.ContainsKey(jobId))
				{
					return false;
				}

				int pawnId = toddler.thingIDNumber;
				if (LastRequestTickByPawn.TryGetValue(pawnId, out int lastTick) && now - lastTick < RequestCooldownTicks)
				{
					return false;
				}

				LastRequestTickByPawn[pawnId] = now;
				PendingJobTicks[jobId] = now;
			}

			return true;
		}

		private static void ClearPending(int jobId)
		{
			if (jobId < 0)
			{
				return;
			}

			lock (LockObj)
			{
				PendingJobTicks.Remove(jobId);
				PendingReports.Remove(jobId);
			}
		}

		private static string BuildPrefix(ToddlerPlayReportKind kind, string partnerLabel)
		{
			return kind == ToddlerPlayReportKind.SelfPlay
				? "RimTalk_ToddlersExpansion_PlayReport_SelfPrefix".Translate()
				: "RimTalk_ToddlersExpansion_PlayReport_MutualPrefix".Translate(partnerLabel.Named("PARTNER"));
		}

		private static string BuildSystemPrompt(ToddlerPlayReportKind kind)
		{
			string lang = GetActiveLanguageName();
			string activity = kind == ToddlerPlayReportKind.SelfPlay
				? "Return a short toddler self-play activity."
				: "Return a short toddler shared play activity.";
			string langRule = string.IsNullOrWhiteSpace(lang)
				? "Use the game's language."
				: $"Use the game's language: {lang}.";
			return $"{activity} {langRule} {OutputRules}";
		}

		private static string BuildUserPrompt(ToddlerPlayReportKind kind, string nearby, string partnerLabel)
		{
			string context = string.IsNullOrWhiteSpace(nearby) ? "none" : nearby;
			if (kind == ToddlerPlayReportKind.SelfPlay)
			{
				return $"Nearby: {context}. Toddler is self-playing. Suggest a specific play activity.";
			}

			return $"Nearby: {context}. Toddler is playing with {partnerLabel}. Suggest a specific shared play activity.";
		}

		private static string GetActiveLanguageName()
		{
			return LanguageDatabase.activeLanguage?.info?.friendlyNameNative ?? "English";
		}

		private static string TrimContext(string context)
		{
			if (string.IsNullOrWhiteSpace(context))
			{
				return string.Empty;
			}

			string normalized = context.Replace('\r', ' ').Replace('\n', ';').Trim();
			if (normalized.Length > ContextMaxChars)
			{
				normalized = normalized.Substring(0, ContextMaxChars).Trim();
			}

			return normalized;
		}

		private static string SanitizeResponse(string response)
		{
			if (string.IsNullOrWhiteSpace(response))
			{
				return string.Empty;
			}

			string line = response.Trim();
			int breakAt = line.IndexOfAny(new[] { '\r', '\n' });
			if (breakAt >= 0)
			{
				line = line.Substring(0, breakAt);
			}

			line = line.Trim().Trim('"', '\'', '`');
			if (line.StartsWith("-", StringComparison.Ordinal))
			{
				line = line.TrimStart('-', ' ', '\t');
			}

			line = line.TrimEnd('.', '!', '?', ';', ':').Trim();
			if (line.Length > 60)
			{
				line = line.Substring(0, 60).Trim();
			}
			return line;
		}
	}
}
