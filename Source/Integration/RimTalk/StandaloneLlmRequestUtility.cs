using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using RimTalk_ToddlersExpansion.Core;
using UnityEngine.Networking;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.RimTalk
{
	public static class StandaloneLlmRequestUtility
	{
		private static bool _loggedFirstRequest;
		private static bool _loggedUnavailable;

		public static bool HasValidConfiguration =>
			ToddlersExpansionMod.Settings?.StandaloneApi != null &&
			ToddlersExpansionMod.Settings.StandaloneApi.IsValid();

		public static bool TryRequestShortText(string systemPrompt, string userPrompt, Action<string> onCompleted)
		{
			if (string.IsNullOrWhiteSpace(userPrompt) || onCompleted == null)
			{
				return false;
			}

			ToddlersExpansionStandaloneApiConfig config = ToddlersExpansionMod.Settings?.StandaloneApi;
			if (config == null || !config.IsValid())
			{
				LogUnavailableOnce();
				return false;
			}

			StandaloneRequestConfig snapshot = new StandaloneRequestConfig(
				config.Provider,
				config.GetResolvedBaseUrl(),
				config.ApiKey,
				config.Model);

			if (!_loggedFirstRequest)
			{
				_loggedFirstRequest = true;
				Log.Message($"[RimTalk_ToddlersExpansion] First standalone short-text request queued (provider={snapshot.Provider}, model={snapshot.Model}).");
			}

			Task.Run(async delegate
			{
				try
				{
					string response = await RequestShortTextInternalAsync(snapshot, systemPrompt, userPrompt).ConfigureAwait(false);
					if (!string.IsNullOrWhiteSpace(response))
					{
						onCompleted(response);
					}
				}
				catch (Exception ex)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Standalone short-text request failed: {ex.Message}");
				}
			});

			return true;
		}

		private static async Task<string> RequestShortTextInternalAsync(
			StandaloneRequestConfig config,
			string systemPrompt,
			string userPrompt)
		{
			string endpointUrl = FormatEndpointUrl(config.BaseUrl);
			string requestJson = BuildRequestJson(config.Model, systemPrompt, userPrompt);

			using UnityWebRequest webRequest = new UnityWebRequest(endpointUrl, "POST");
			webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson));
			webRequest.downloadHandler = new DownloadHandlerBuffer();
			webRequest.SetRequestHeader("Content-Type", "application/json");

			if (!string.IsNullOrWhiteSpace(config.ApiKey))
			{
				webRequest.SetRequestHeader("Authorization", $"Bearer {config.ApiKey}");
			}

			Dictionary<string, string> extraHeaders = config.Provider.GetExtraHeaders();
			if (extraHeaders != null)
			{
				foreach (KeyValuePair<string, string> header in extraHeaders)
				{
					webRequest.SetRequestHeader(header.Key, header.Value);
				}
			}

			UnityWebRequestAsyncOperation asyncOp = webRequest.SendWebRequest();
			float elapsedSeconds = 0f;
			const float timeoutSeconds = 60f;

			while (!asyncOp.isDone)
			{
				if (Current.Game == null)
				{
					return null;
				}

				await Task.Delay(100).ConfigureAwait(false);
				elapsedSeconds += 0.1f;
				if (elapsedSeconds > timeoutSeconds)
				{
					webRequest.Abort();
					throw new TimeoutException($"Request timed out after {timeoutSeconds:0} seconds.");
				}
			}

			if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
				webRequest.result == UnityWebRequest.Result.ProtocolError)
			{
				string responseText = webRequest.downloadHandler?.text ?? string.Empty;
				throw new InvalidOperationException(
					$"HTTP {(int)webRequest.responseCode} {webRequest.error}. {responseText}".Trim());
			}

			StandaloneOpenAiResponse response =
				StandaloneJsonUtility.DeserializeFromJson<StandaloneOpenAiResponse>(webRequest.downloadHandler.text);
			return response?.Choices?[0]?.Message?.Content;
		}

		private static string BuildRequestJson(string model, string systemPrompt, string userPrompt)
		{
			StandaloneOpenAiRequest request = new StandaloneOpenAiRequest
			{
				Model = model,
				Temperature = 0.7,
				MaxTokens = 32,
				Messages = new List<StandaloneOpenAiMessage>()
			};

			if (!string.IsNullOrWhiteSpace(systemPrompt))
			{
				request.Messages.Add(new StandaloneOpenAiMessage
				{
					Role = "system",
					Content = systemPrompt
				});
			}

			request.Messages.Add(new StandaloneOpenAiMessage
			{
				Role = "user",
				Content = userPrompt
			});

			return StandaloneJsonUtility.SerializeToJson(request);
		}

		private static string FormatEndpointUrl(string baseUrl)
		{
			const string defaultPath = "/v1/chat/completions";
			if (string.IsNullOrWhiteSpace(baseUrl))
			{
				return string.Empty;
			}

			string trimmed = baseUrl.Trim().TrimEnd('/');
			Uri uri = new Uri(trimmed);
			return uri.AbsolutePath == "/" || string.IsNullOrEmpty(uri.AbsolutePath.Trim('/'))
				? trimmed + defaultPath
				: trimmed;
		}

		private static void LogUnavailableOnce()
		{
			if (_loggedUnavailable)
			{
				return;
			}

			_loggedUnavailable = true;
			Log.Warning("[RimTalk_ToddlersExpansion] Standalone short-text request disabled: invalid standalone API configuration.");
		}

		private readonly struct StandaloneRequestConfig
		{
			public readonly ToddlersExpansionStandaloneLlmProvider Provider;
			public readonly string BaseUrl;
			public readonly string ApiKey;
			public readonly string Model;

			public StandaloneRequestConfig(
				ToddlersExpansionStandaloneLlmProvider provider,
				string baseUrl,
				string apiKey,
				string model)
			{
				Provider = provider;
				BaseUrl = baseUrl ?? string.Empty;
				ApiKey = apiKey ?? string.Empty;
				Model = model ?? string.Empty;
			}
		}
	}

	internal static class StandaloneJsonUtility
	{
		public static string SerializeToJson<T>(T obj)
		{
			using MemoryStream stream = new MemoryStream();
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
			serializer.WriteObject(stream, obj);
			return Encoding.UTF8.GetString(stream.ToArray());
		}

		public static T DeserializeFromJson<T>(string json)
		{
			using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty));
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
			return (T)serializer.ReadObject(stream);
		}
	}

	[DataContract]
	internal sealed class StandaloneOpenAiRequest
	{
		[DataMember(Name = "model")]
		public string Model { get; set; }

		[DataMember(Name = "messages")]
		public List<StandaloneOpenAiMessage> Messages { get; set; }

		[DataMember(Name = "temperature", EmitDefaultValue = false)]
		public double? Temperature { get; set; }

		[DataMember(Name = "max_tokens", EmitDefaultValue = false)]
		public int? MaxTokens { get; set; }
	}

	[DataContract]
	internal sealed class StandaloneOpenAiMessage
	{
		[DataMember(Name = "role")]
		public string Role { get; set; }

		[DataMember(Name = "content")]
		public string Content { get; set; }
	}

	[DataContract]
	internal sealed class StandaloneOpenAiResponse
	{
		[DataMember(Name = "choices")]
		public List<StandaloneOpenAiChoice> Choices { get; set; }
	}

	[DataContract]
	internal sealed class StandaloneOpenAiChoice
	{
		[DataMember(Name = "message")]
		public StandaloneOpenAiMessage Message { get; set; }
	}
}
