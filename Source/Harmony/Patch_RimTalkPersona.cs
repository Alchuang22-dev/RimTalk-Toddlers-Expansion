using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_RimTalkPersona
	{
		private const string PersonaHediffDefName = "RimTalk_PersonaData";
		private const float RestrictedTalkWeight = 0.2f;

		private static HediffDef _personaHediffDef;
		private static FieldInfo _personalityField;
		private static FieldInfo _talkInitiationWeightField;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			if (!RimTalkCompatUtility.IsRimTalkActive)
			{
				return;
			}

			Type personaType = AccessTools.TypeByName("RimTalk.Data.Hediff_Persona");
			if (personaType == null)
			{
				return;
			}

			MethodInfo target = AccessTools.Method(personaType, "GetOrAddNew", new[] { typeof(Pawn) });
			if (target == null)
			{
				return;
			}

			_personalityField = AccessTools.Field(personaType, "Personality");
			_talkInitiationWeightField = AccessTools.Field(personaType, "TalkInitiationWeight");

			MethodInfo prefix = AccessTools.Method(typeof(Patch_RimTalkPersona), nameof(GetOrAddNew_Prefix));
			MethodInfo postfix = AccessTools.Method(typeof(Patch_RimTalkPersona), nameof(GetOrAddNew_Postfix));
			harmony.Patch(target, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
		}

		private static void GetOrAddNew_Prefix(Pawn pawn, out bool __state)
		{
			__state = HasExistingPersona(pawn);
		}

		private static void GetOrAddNew_Postfix(Pawn pawn, object __result, bool __state)
		{
			if (__state || pawn == null || __result == null)
			{
				return;
			}

			if (!ToddlerSoulUtility.TryGetSoulForPawn(pawn, out string personality, out float chattiness))
			{
				return;
			}

			_personalityField?.SetValue(__result, personality);
			if (_talkInitiationWeightField != null)
			{
				float talkWeight = ShouldUseRestrictedTalkWeight(pawn) ? RestrictedTalkWeight : chattiness;
				_talkInitiationWeightField.SetValue(__result, talkWeight);
			}

			if (Prefs.DevMode)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] Assigned young pawn soul to {pawn.LabelShort}: {personality}");
			}
		}

		private static bool HasExistingPersona(Pawn pawn)
		{
			_personaHediffDef ??= DefDatabase<HediffDef>.GetNamedSilentFail(PersonaHediffDefName);
			return pawn?.health?.hediffSet != null
				&& _personaHediffDef != null
				&& pawn.health.hediffSet.GetFirstHediffOfDef(_personaHediffDef) != null;
		}

		private static bool ShouldUseRestrictedTalkWeight(Pawn pawn)
		{
			return pawn.IsSlave || pawn.IsPrisoner || IsVisitor(pawn) || IsEnemy(pawn);
		}

		private static bool IsVisitor(Pawn pawn)
		{
			return pawn?.Faction != null
				&& Faction.OfPlayer != null
				&& pawn.Faction != Faction.OfPlayer
				&& !pawn.HostileTo(Faction.OfPlayer)
				&& !pawn.IsPrisoner;
		}

		private static bool IsEnemy(Pawn pawn)
		{
			return pawn != null
				&& Faction.OfPlayer != null
				&& pawn.HostileTo(Faction.OfPlayer)
				&& !pawn.IsPrisoner;
		}
	}
}
