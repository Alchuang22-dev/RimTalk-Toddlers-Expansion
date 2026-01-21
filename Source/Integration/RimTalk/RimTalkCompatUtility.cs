using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.RimTalk
{
	public static class RimTalkCompatUtility
	{
		private const string ModId = "cj.rimtalk.toddlers";
		private const string PromptApiTypeName = "RimTalk.API.RimTalkPromptAPI";
		private const string TalkRequestTypeName = "RimTalk.Data.TalkRequest";
		private const string TalkTypeTypeName = "RimTalk.Source.Data.TalkType";
		private const string TalkServiceTypeName = "RimTalk.Service.TalkService";
		private const string ContextHelperTypeName = "RimTalk.Util.ContextHelper";
		private const string AiClientFactoryTypeName = "RimTalk.Client.AIClientFactory";
		private const string AiClientTypeName = "RimTalk.Client.IAIClient";
		private const string RoleTypeName = "RimTalk.Data.Role";
		private const string PayloadTypeName = "RimTalk.Client.Payload";

		private static bool _initialized;
		private static bool _isActive;
		private static bool _warned;
		private static bool _hooksRegistered;
		private static bool _loggedInit;
		private static bool _loggedRegister;
		private static bool _loggedShortTextUnavailable;
		private static bool _loggedShortTextRequest;
		private static bool _loggedClientSuccess;
		private static bool _loggedClientFailure;

		private static MethodInfo _registerPawnVariable;
		private static MethodInfo _generateTalk;
		private static ConstructorInfo _talkRequestCtor;
		private static Type _talkRequestType;
		private static PropertyInfo _talkRequestInitiator;
		private static PropertyInfo _talkRequestRecipient;
		private static PropertyInfo _talkRequestTalkType;
		private static Type _talkTypeType;
		private static MethodInfo _collectNearbyContextText;
		private static MethodInfo _getAiClientAsync;
		private static MethodInfo _getChatCompletionAsync;
		private static Type _roleType;
		private static Type _payloadType;
		private static Type _messageTupleType;
		private static Type _messageListType;
		private static Type _actionPayloadType;
		private static object _roleSystem;
		private static object _roleUser;

		public static bool IsRimTalkActive
		{
			get
			{
				EnsureInitialized();
				return _isActive;
			}
		}

		public static void TryRegisterToddlerVariables()
		{
			EnsureInitialized();
			if (!_isActive || _hooksRegistered || _registerPawnVariable == null)
			{
				return;
			}

			try
			{
				_registerPawnVariable.Invoke(null, new object[]
				{
					ModId,
					"toddler_language",
					(Func<Pawn, string>)ToddlerContextInjector.GetToddlerLanguageDescriptor,
					"RimTalk_ToddlersExpansion_ToddlerLanguageDescriptor".Translate(),
					100
				});

				_registerPawnVariable.Invoke(null, new object[]
				{
					ModId,
					"toddler_play",
					(Func<Pawn, string>)ToddlerContextInjector.GetToddlerPlayDescriptor,
					"RimTalk_ToddlersExpansion_ToddlerPlayContext".Translate(),
					110
				});

				_hooksRegistered = true;
				if (!_loggedRegister)
				{
					_loggedRegister = true;
					Log.Message("[RimTalk_ToddlersExpansion] Registered RimTalk toddler context variables.");
				}
			}
			catch (Exception ex)
			{
				WarnOnce("RegisterToddlerVariables", ex);
			}
		}

		public static bool TryQueueTalk(Pawn initiator, Pawn recipient, string prompt, string talkTypeName)
		{
			if (initiator == null || string.IsNullOrWhiteSpace(prompt))
			{
				return false;
			}

			EnsureInitialized();
			if (!_isActive || _generateTalk == null || _talkRequestCtor == null || _talkTypeType == null)
			{
				return false;
			}

			try
			{
				object talkType = Enum.Parse(_talkTypeType, talkTypeName ?? "Other", true);
				object request = _talkRequestCtor.Invoke(new object[] { prompt, initiator, recipient, talkType });
				object result = _generateTalk.Invoke(null, new[] { request });
				return result is bool ok && ok;
			}
			catch (Exception ex)
			{
				WarnOnce("TryQueueTalk", ex);
				return false;
			}
		}

		public static bool TryGetTalkRequestInfo(object talkRequest, out Pawn initiator, out Pawn recipient, out string talkTypeName)
		{
			initiator = null;
			recipient = null;
			talkTypeName = null;
			if (talkRequest == null)
			{
				return false;
			}

			EnsureInitialized();
			if (!_isActive)
			{
				return false;
			}

			try
			{
				if (_talkRequestType != null && !_talkRequestType.IsInstanceOfType(talkRequest))
				{
					return false;
				}

				_talkRequestInitiator ??= AccessTools.Property(talkRequest.GetType(), "Initiator");
				_talkRequestRecipient ??= AccessTools.Property(talkRequest.GetType(), "Recipient");
				_talkRequestTalkType ??= AccessTools.Property(talkRequest.GetType(), "TalkType");

				initiator = _talkRequestInitiator?.GetValue(talkRequest) as Pawn;
				recipient = _talkRequestRecipient?.GetValue(talkRequest) as Pawn;
				object talkType = _talkRequestTalkType?.GetValue(talkRequest);
				talkTypeName = talkType?.ToString();
				return true;
			}
			catch (Exception ex)
			{
				WarnOnce("TalkRequestInfo", ex);
				return false;
			}
		}

		public static bool IsUserTalkType(string talkTypeName)
		{
			return string.Equals(talkTypeName, "User", StringComparison.OrdinalIgnoreCase);
		}

		public static bool TryGetNearbyContextText(Pawn pawn, out string contextText)
		{
			contextText = null;
			if (pawn == null)
			{
				return false;
			}

			EnsureInitialized();
			if (!_isActive || _collectNearbyContextText == null)
			{
				return false;
			}

			try
			{
				contextText = _collectNearbyContextText.Invoke(null, new object[] { pawn, 5, 12, 18, 200, 120 }) as string;
				return !string.IsNullOrWhiteSpace(contextText);
			}
			catch (Exception ex)
			{
				WarnOnce("CollectNearbyContextText", ex);
				return false;
			}
		}

		public static bool TryRequestShortText(string systemPrompt, string userPrompt, Action<string> onCompleted)
		{
			if (string.IsNullOrWhiteSpace(userPrompt) || onCompleted == null)
			{
				return false;
			}

			EnsureInitialized();
			if (!_isActive)
			{
				LogShortTextUnavailableOnce("RimTalk not active.");
				return false;
			}
			if (_getAiClientAsync == null || _getChatCompletionAsync == null || _messageListType == null)
			{
				LogShortTextUnavailableOnce("LLM API hooks missing.");
				return false;
			}

			if (!_loggedShortTextRequest)
			{
				_loggedShortTextRequest = true;
				Log.Message($"[RimTalk_ToddlersExpansion] First short-text request queued (systemPrompt={systemPrompt?.Length ?? 0}, userPrompt={userPrompt.Length}).");
			}

			Task.Run(async () =>
			{
				try
				{
					string response = await RequestShortTextInternalAsync(systemPrompt, userPrompt).ConfigureAwait(false);
					if (!string.IsNullOrWhiteSpace(response))
					{
						onCompleted(response);
					}
				}
				catch (Exception ex)
				{
					WarnOnce("RequestShortText", ex);
				}
			});

			return true;
		}

		private static void EnsureInitialized()
		{
			if (_initialized)
			{
				return;
			}

			_initialized = true;
			try
			{
				Type promptApiType = AccessTools.TypeByName(PromptApiTypeName);
				if (promptApiType == null)
				{
					_isActive = false;
					if (!_loggedInit)
					{
						_loggedInit = true;
						Log.Message("[RimTalk_ToddlersExpansion] RimTalk not detected; integration disabled.");
					}
					return;
				}

				_isActive = true;
				_registerPawnVariable = AccessTools.Method(promptApiType, "RegisterPawnVariable",
					new[] { typeof(string), typeof(string), typeof(Func<Pawn, string>), typeof(string), typeof(int) });

				_talkRequestType = AccessTools.TypeByName(TalkRequestTypeName);
				_talkTypeType = AccessTools.TypeByName(TalkTypeTypeName);
				if (_talkRequestType != null && _talkTypeType != null)
				{
					_talkRequestCtor = _talkRequestType.GetConstructor(new[] { typeof(string), typeof(Pawn), typeof(Pawn), _talkTypeType });
					_talkRequestInitiator = AccessTools.Property(_talkRequestType, "Initiator");
					_talkRequestRecipient = AccessTools.Property(_talkRequestType, "Recipient");
					_talkRequestTalkType = AccessTools.Property(_talkRequestType, "TalkType");
				}

				Type talkServiceType = AccessTools.TypeByName(TalkServiceTypeName);
				if (talkServiceType != null && _talkRequestType != null)
				{
					_generateTalk = AccessTools.Method(talkServiceType, "GenerateTalk", new[] { _talkRequestType });
				}

				Type contextHelperType = AccessTools.TypeByName(ContextHelperTypeName);
				if (contextHelperType != null)
				{
					_collectNearbyContextText = AccessTools.Method(contextHelperType, "CollectNearbyContextText",
						new[] { typeof(Pawn), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) });
				}

				Type aiClientFactoryType = AccessTools.TypeByName(AiClientFactoryTypeName);
				if (aiClientFactoryType != null)
				{
					_getAiClientAsync = AccessTools.Method(aiClientFactoryType, "GetAIClientAsync");
				}

				Type aiClientType = AccessTools.TypeByName(AiClientTypeName);
				_roleType = AccessTools.TypeByName(RoleTypeName);
				_payloadType = AccessTools.TypeByName(PayloadTypeName);
				if (aiClientType != null && _roleType != null && _payloadType != null)
				{
					_messageTupleType = typeof(ValueTuple<,>).MakeGenericType(_roleType, typeof(string));
					_messageListType = typeof(System.Collections.Generic.List<>).MakeGenericType(_messageTupleType);
					_actionPayloadType = typeof(Action<>).MakeGenericType(_payloadType);
					_getChatCompletionAsync = AccessTools.Method(aiClientType, "GetChatCompletionAsync",
						new[] { _messageListType, _messageListType, _actionPayloadType });
					_roleSystem = Enum.Parse(_roleType, "System");
					_roleUser = Enum.Parse(_roleType, "User");
				}

				if (!_loggedInit)
				{
					_loggedInit = true;
					if (_getAiClientAsync != null && _getChatCompletionAsync != null && _messageListType != null)
					{
						Log.Message("[RimTalk_ToddlersExpansion] RimTalk detected; LLM short-text pipeline ready.");
					}
					else
					{
						Log.Warning("[RimTalk_ToddlersExpansion] RimTalk detected, but LLM short-text pipeline unavailable.");
					}
				}
			}
			catch (Exception ex)
			{
				_isActive = false;
				WarnOnce("Initialize", ex);
			}
		}

		private static async Task<string> RequestShortTextInternalAsync(string systemPrompt, string userPrompt)
		{
			object client = await GetAiClientAsync().ConfigureAwait(false);
			if (client == null || _messageListType == null || _messageTupleType == null)
			{
				return null;
			}

			object prefixMessages = CreateMessageList(systemPrompt, _roleSystem);
			object messages = CreateMessageList(userPrompt, _roleUser);
			object taskObj = _getChatCompletionAsync?.Invoke(client, new[] { prefixMessages, messages, null });
			if (taskObj is not Task task)
			{
				return null;
			}

			await task.ConfigureAwait(false);
			object payload = task.GetType().GetProperty("Result")?.GetValue(task);
			if (payload == null)
			{
				return null;
			}

			return payload.GetType().GetProperty("Response")?.GetValue(payload) as string;
		}

		private static async Task<object> GetAiClientAsync()
		{
			object taskObj = _getAiClientAsync?.Invoke(null, null);
			if (taskObj is not Task task)
			{
				return null;
			}

			await task.ConfigureAwait(false);
			object result = task.GetType().GetProperty("Result")?.GetValue(task);
			if (result == null)
			{
				if (!_loggedClientFailure)
				{
					_loggedClientFailure = true;
					Log.Warning("[RimTalk_ToddlersExpansion] RimTalk AI client unavailable.");
				}
			}
			else if (!_loggedClientSuccess)
			{
				_loggedClientSuccess = true;
				Log.Message("[RimTalk_ToddlersExpansion] RimTalk AI client acquired.");
			}

			return result;
		}

		private static object CreateMessageList(string message, object role)
		{
			object list = Activator.CreateInstance(_messageListType);
			if (string.IsNullOrWhiteSpace(message))
			{
				return list;
			}

			object tuple = Activator.CreateInstance(_messageTupleType, role, message);
			((IList)list).Add(tuple);
			return list;
		}

		private static void WarnOnce(string context, Exception ex)
		{
			if (_warned || !Prefs.DevMode)
			{
				return;
			}

			_warned = true;
			Log.Warning($"[RimTalk_ToddlersExpansion] RimTalk compat {context} failed: {ex.Message}");
		}

		private static void LogShortTextUnavailableOnce(string reason)
		{
			if (_loggedShortTextUnavailable)
			{
				return;
			}

			_loggedShortTextUnavailable = true;
			Log.Warning($"[RimTalk_ToddlersExpansion] Short-text LLM request disabled: {reason}");
		}
	}
}
