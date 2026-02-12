using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Harmony
{
	/// <summary>
	/// Refactor of Toddlers' IgnoreToddlerMentalStates patch:
	/// treat baby Crying/Giggling as non-blocking in selected systems.
	/// </summary>
	public static class Patch_IgnoreToddlerMentalStates
	{
		private const string CryingDefName = "Crying";
		private const string GigglingDefName = "Giggling";

		private static readonly MethodInfo PawnInMentalStateGetter =
			AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.InMentalState));

		private static readonly MethodInfo ShouldTreatAsBlockingMentalStateMethod =
			AccessTools.Method(typeof(Patch_IgnoreToddlerMentalStates), nameof(ShouldTreatAsBlockingMentalState));

		private static readonly AccessTools.FieldRef<MentalStateHandler, Pawn> MentalStateHandlerPawnField =
			AccessTools.FieldRefAccess<MentalStateHandler, Pawn>("pawn");

		public static void Init(HarmonyLib.Harmony harmony)
		{
			// Keep original Toddlers coverage for caravan/ritual/gathering/shuttle paths.
			PatchInMentalStateCalls(harmony, AccessTools.Method(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.AllSendablePawns)));
			PatchInMentalStateCalls(harmony, AccessTools.Method(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.ForceCaravanDepart)));
			PatchInMentalStateCalls(harmony, AccessTools.Method(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.GetForceDepartWarningMessage)));
			PatchInMentalStateCalls(harmony, AccessTools.Method(typeof(ForbidUtility), nameof(ForbidUtility.CaresAboutForbidden)));
			PatchInMentalStateCalls(harmony, AccessTools.Method(typeof(Trigger_MentalState), nameof(Trigger_MentalState.ActivateOn)));
			PatchInMentalStateCalls(harmony, AccessTools.Method(typeof(Trigger_NoMentalState), nameof(Trigger_NoMentalState.ActivateOn)));
			PatchInMentalStateCalls(harmony, AccessTools.Method(typeof(GatheringsUtility), nameof(GatheringsUtility.PawnCanStartOrContinueGathering)));
			PatchInMentalStateCalls(
				harmony,
				AccessTools.Method(
					typeof(RitualRoleAssignments),
					nameof(RitualRoleAssignments.PawnNotAssignableReason),
					new[] { typeof(Pawn), typeof(RitualRole), typeof(Precept_Ritual), typeof(RitualRoleAssignments), typeof(TargetInfo), typeof(bool).MakeByRefType() }));
			PatchInMentalStateCalls(harmony, AccessTools.Method(typeof(CompShuttle), "PawnIsHealthyEnoughForShuttle", new[] { typeof(Pawn) }));

			// Extra coverage for long-running lord/constant jobs.
			PatchInMentalStateCalls(harmony, AccessTools.Method(typeof(ThinkNode_ConditionalCanDoLordJobNow), "Satisfied", new[] { typeof(Pawn) }));
			PatchInMentalStateCalls(harmony, AccessTools.Method(typeof(ThinkNode_ConditionalCanDoConstantThinkTreeJobNow), "Satisfied", new[] { typeof(Pawn) }));

			// Prevent lord jobs (rituals etc.) from treating baby Crying/Giggling as a hard mental-state event.
			MethodInfo notifyInMentalState = AccessTools.Method(typeof(Lord), nameof(Lord.Notify_InMentalState));
			if (notifyInMentalState != null)
			{
				harmony.Patch(
					notifyInMentalState,
					prefix: new HarmonyMethod(typeof(Patch_IgnoreToddlerMentalStates), nameof(Lord_Notify_InMentalState_Prefix)));
			}

			// Prevent container drop side effects when baby fits trigger inside shuttle/vehicle transport holders.
			MethodInfo tryStartMentalState = AccessTools.Method(
				typeof(MentalStateHandler),
				nameof(MentalStateHandler.TryStartMentalState),
				new[]
				{
					typeof(MentalStateDef),
					typeof(string),
					typeof(bool),
					typeof(bool),
					typeof(bool),
					typeof(Pawn),
					typeof(bool),
					typeof(bool),
					typeof(bool)
				});
			if (tryStartMentalState != null)
			{
				harmony.Patch(
					tryStartMentalState,
					prefix: new HarmonyMethod(typeof(Patch_IgnoreToddlerMentalStates), nameof(MentalStateHandler_TryStartMentalState_Prefix)));
			}
		}

		private static void PatchInMentalStateCalls(HarmonyLib.Harmony harmony, MethodBase target)
		{
			if (target == null)
			{
				return;
			}

			harmony.Patch(
				target,
				transpiler: new HarmonyMethod(typeof(Patch_IgnoreToddlerMentalStates), nameof(ReplaceInMentalStateCallTranspiler)));
		}

		private static IEnumerable<CodeInstruction> ReplaceInMentalStateCallTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.Calls(PawnInMentalStateGetter))
				{
					var replacement = new CodeInstruction(OpCodes.Call, ShouldTreatAsBlockingMentalStateMethod);
					replacement.labels.AddRange(instruction.labels);
					replacement.blocks.AddRange(instruction.blocks);
					yield return replacement;
				}
				else
				{
					yield return instruction;
				}
			}
		}

		// Replacement for direct Pawn.InMentalState checks in targeted vanilla methods.
		private static bool ShouldTreatAsBlockingMentalState(Pawn pawn)
		{
			if (pawn?.InMentalState != true)
			{
				return false;
			}

			return !IsIgnoredBabyFitMentalState(pawn, pawn.MentalStateDef);
		}

		private static bool Lord_Notify_InMentalState_Prefix(Pawn pawn, MentalStateDef def)
		{
			return !IsIgnoredBabyFitMentalState(pawn, def);
		}

		private static bool MentalStateHandler_TryStartMentalState_Prefix(
			MentalStateDef stateDef,
			MentalStateHandler __instance,
			ref bool __result)
		{
			Pawn pawn = MentalStateHandlerPawnField(__instance);
			if (!IsIgnoredBabyFitMentalState(pawn, stateDef))
			{
				return true;
			}

			// Vanilla TryStartMentalState drops pawns from CompTransporter containers.
			// Rejecting this non-blocking state here avoids babies being ejected from shuttles/vehicles.
			if (!IsTransportContainer(pawn?.ParentHolder))
			{
				return true;
			}

			__result = false;
			return false;
		}

		private static bool IsIgnoredBabyFitMentalState(Pawn pawn, MentalStateDef stateDef)
		{
			if (pawn == null || stateDef == null)
			{
				return false;
			}

			DevelopmentalStage stage = pawn.DevelopmentalStage;
			if (stage != DevelopmentalStage.Baby && stage != DevelopmentalStage.Newborn)
			{
				return false;
			}

			string defName = stateDef.defName;
			return defName == CryingDefName || defName == GigglingDefName;
		}

		private static bool IsTransportContainer(IThingHolder holder)
		{
			if (holder == null)
			{
				return false;
			}

			if (holder is CompTransporter)
			{
				return true;
			}

			string name = holder.GetType().FullName ?? holder.GetType().Name;
			return name.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Transport", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Shuttle", StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
