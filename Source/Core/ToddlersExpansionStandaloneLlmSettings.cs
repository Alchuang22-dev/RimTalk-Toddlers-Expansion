using System.Collections.Generic;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public enum ToddlersExpansionStandaloneLlmProvider
	{
		OpenAI,
		DeepSeek,
		Grok,
		GLM,
		OpenRouter,
		AlibabaIntl,
		AlibabaCN,
		Local,
		Custom
	}

	public sealed class ToddlersExpansionStandaloneApiConfig : IExposable
	{
		public ToddlersExpansionStandaloneLlmProvider Provider = ToddlersExpansionStandaloneLlmProvider.OpenAI;
		public string ApiKey = "";
		public string Model = "";
		public string BaseUrl = "";

		public bool RequiresApiKey =>
			Provider != ToddlersExpansionStandaloneLlmProvider.Local &&
			Provider != ToddlersExpansionStandaloneLlmProvider.Custom;

		public bool RequiresCustomBaseUrl =>
			Provider == ToddlersExpansionStandaloneLlmProvider.Local ||
			Provider == ToddlersExpansionStandaloneLlmProvider.Custom;

		public string GetResolvedBaseUrl()
		{
			if (RequiresCustomBaseUrl)
			{
				return BaseUrl?.Trim() ?? string.Empty;
			}

			return Provider.GetEndpointUrl();
		}

		public bool IsValid()
		{
			if (string.IsNullOrWhiteSpace(Model))
			{
				return false;
			}

			if (string.IsNullOrWhiteSpace(GetResolvedBaseUrl()))
			{
				return false;
			}

			return !RequiresApiKey || !string.IsNullOrWhiteSpace(ApiKey);
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref Provider, "provider", ToddlersExpansionStandaloneLlmProvider.OpenAI);
			Scribe_Values.Look(ref ApiKey, "apiKey", "");
			Scribe_Values.Look(ref Model, "model", "");
			Scribe_Values.Look(ref BaseUrl, "baseUrl", "");
		}
	}

	public static class ToddlersExpansionStandaloneLlmProviderUtility
	{
		private static readonly Dictionary<ToddlersExpansionStandaloneLlmProvider, string> EndpointByProvider =
			new Dictionary<ToddlersExpansionStandaloneLlmProvider, string>
			{
				{ ToddlersExpansionStandaloneLlmProvider.OpenAI, "https://api.openai.com/v1/chat/completions" },
				{ ToddlersExpansionStandaloneLlmProvider.DeepSeek, "https://api.deepseek.com/v1/chat/completions" },
				{ ToddlersExpansionStandaloneLlmProvider.Grok, "https://api.x.ai/v1/chat/completions" },
				{ ToddlersExpansionStandaloneLlmProvider.GLM, "https://api.z.ai/api/paas/v4/chat/completions" },
				{ ToddlersExpansionStandaloneLlmProvider.OpenRouter, "https://openrouter.ai/api/v1/chat/completions" },
				{ ToddlersExpansionStandaloneLlmProvider.AlibabaIntl, "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions" },
				{ ToddlersExpansionStandaloneLlmProvider.AlibabaCN, "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions" }
			};

		public static string GetLabel(this ToddlersExpansionStandaloneLlmProvider provider)
		{
			switch (provider)
			{
				case ToddlersExpansionStandaloneLlmProvider.AlibabaIntl:
					return "Alibaba (Intl)";
				case ToddlersExpansionStandaloneLlmProvider.AlibabaCN:
					return "Alibaba (CN)";
				case ToddlersExpansionStandaloneLlmProvider.Local:
					return "OpenAI-Compatible (Local)";
				case ToddlersExpansionStandaloneLlmProvider.Custom:
					return "Custom Endpoint";
				default:
					return provider.ToString();
			}
		}

		public static string GetEndpointUrl(this ToddlersExpansionStandaloneLlmProvider provider)
		{
			return EndpointByProvider.TryGetValue(provider, out string endpoint) ? endpoint : string.Empty;
		}

		public static Dictionary<string, string> GetExtraHeaders(this ToddlersExpansionStandaloneLlmProvider provider)
		{
			if (provider != ToddlersExpansionStandaloneLlmProvider.OpenRouter)
			{
				return null;
			}

			return new Dictionary<string, string>
			{
				{ "HTTP-Referer", "https://github.com/jlibrary/RimTalk-Toddlers-Expansion" },
				{ "X-Title", "RimTalk Toddlers Expansion" }
			};
		}
	}
}
