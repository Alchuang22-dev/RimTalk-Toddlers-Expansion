using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	/// <summary>
	/// Recover RimTalk text decoration when another mod's prefix hits the reported array mismatch.
	/// </summary>
	public static class Patch_RimTalkDecorateTextCompatibility
	{
		private const string PawnUtilTypeName = "RimTalk.Util.PawnUtil";
		private const string ContextHelperTypeName = "RimTalk.Util.ContextHelper";
		private static MethodInfo _getDecoratedName;
		private static bool _loggedRecovery;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type pawnUtilType = AccessTools.TypeByName(PawnUtilTypeName);
			if (pawnUtilType == null)
			{
				return;
			}

			Type pawnSetType = typeof(HashSet<Pawn>);
			MethodInfo[] methods = pawnUtilType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0; i < methods.Length; i++)
			{
				MethodInfo method = methods[i];
				if (method.Name != "DecorateText" || method.ReturnType != typeof(string))
				{
					continue;
				}

				ParameterInfo[] parameters = method.GetParameters();
				if ((parameters.Length != 2 && parameters.Length != 3)
					|| parameters[0].ParameterType != typeof(string)
					|| parameters[1].ParameterType != pawnSetType
					|| (parameters.Length == 3 && parameters[2].ParameterType != typeof(Pawn)))
				{
					continue;
				}

				_getDecoratedName = ResolveGetDecoratedName();
				harmony.Patch(method, finalizer: new HarmonyMethod(typeof(Patch_RimTalkDecorateTextCompatibility), nameof(DecorateText_Finalizer)));
			}
		}

		private static Exception DecorateText_Finalizer(object[] __args, ref string __result, Exception __exception)
		{
			if (!(__exception is ArrayTypeMismatchException))
			{
				return __exception;
			}

			try
			{
				string text = __args != null && __args.Length > 0 ? __args[0] as string : null;
				HashSet<Pawn> relevantPawns = __args != null && __args.Length > 1 ? __args[1] as HashSet<Pawn> : null;
				Pawn selfPawn = __args != null && __args.Length > 2 ? __args[2] as Pawn : null;
				__result = DecorateTextWithoutLinq(text, relevantPawns, selfPawn);

				if (!_loggedRecovery)
				{
					_loggedRecovery = true;
					Log.Warning("[RimTalk_ToddlersExpansion] Recovered RimTalk text decoration after an ArrayTypeMismatchException.");
				}

				return null;
			}
			catch (Exception fallbackException)
			{
				Log.Error($"[RimTalk_ToddlersExpansion] RimTalk text-decoration fallback failed: {fallbackException}");
				return __exception;
			}
		}

		private static string DecorateTextWithoutLinq(string text, HashSet<Pawn> relevantPawns, Pawn selfPawn)
		{
			if (string.IsNullOrEmpty(text) || relevantPawns == null || relevantPawns.Count == 0)
			{
				return text;
			}

			List<KeyValuePair<string, string>> replacements = new List<KeyValuePair<string, string>>(relevantPawns.Count);
			foreach (Pawn pawn in relevantPawns)
			{
				if (pawn == null)
				{
					continue;
				}

				string label = pawn.LabelShort;
				if (string.IsNullOrEmpty(label))
				{
					continue;
				}

				string decoratedName = pawn == selfPawn
					? Translator.Translate("RimTalk.PawnUtil.Self").ToString()
					: GetDecoratedName(pawn, label);
				if (!string.IsNullOrEmpty(decoratedName))
				{
					replacements.Add(new KeyValuePair<string, string>(label, decoratedName));
				}
			}

			replacements.Sort((left, right) => right.Key.Length.CompareTo(left.Key.Length));
			for (int i = 0; i < replacements.Count; i++)
			{
				KeyValuePair<string, string> replacement = replacements[i];
				if (text.Contains(replacement.Key))
				{
					text = text.Replace(replacement.Key, replacement.Value);
				}
			}

			return text;
		}

		private static string GetDecoratedName(Pawn pawn, string fallback)
		{
			if (_getDecoratedName == null)
			{
				return fallback;
			}

			try
			{
				return _getDecoratedName.Invoke(null, new object[] { pawn }) as string ?? fallback;
			}
			catch
			{
				return fallback;
			}
		}

		private static MethodInfo ResolveGetDecoratedName()
		{
			Type contextHelperType = AccessTools.TypeByName(ContextHelperTypeName);
			return contextHelperType == null
				? null
				: AccessTools.Method(contextHelperType, "GetDecoratedName", new[] { typeof(Pawn) });
		}
	}
}
