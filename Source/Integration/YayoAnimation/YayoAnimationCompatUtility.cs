using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.YayoAnimation
{
	/// <summary>
	/// Compatibility layer for Yayo's Animation.
	/// </summary>
	public static class YayoAnimationCompatUtility
	{
		private static bool _initialized;
		private static bool _yayoAnimationLoaded;
		private static bool _loggedBabyPlayYayoAnimation;
		private static readonly Dictionary<int, int> _lastLoggedBabyPlayJobByPawn = new Dictionary<int, int>(32);
		private static readonly string[] BabyPlayYayoProfiles =
		{
			"PlayToys",
			"Play_Hoopstone",
			"Play_DartsBoard",
			"GoldenCubePlay",
			"ExtinguishSelf",
			"SocialRelax"
		};

		private static readonly HashSet<Pawn> _suppressedPawns = new HashSet<Pawn>();

		private static Type _animationCoreType;
		private static MethodInfo _checkAniMethod;
		private static MethodInfo _aniStandingMethod;

		private static Type _pawnDrawDataType;
		private static MethodInfo _resetMethod;
		private static FieldInfo _fixedRotField;
		private static FieldInfo _angleOffsetField;
		private static FieldInfo _posOffsetField;

		public static void Initialize()
		{
			if (_initialized)
			{
				return;
			}

			_initialized = true;

			try
			{
				Assembly yayoAssembly = null;
				foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					if (assembly.GetName().Name == "yayoAni")
					{
						yayoAssembly = assembly;
						break;
					}
				}

				if (yayoAssembly == null)
				{
					Log.Message("[RimTalk_ToddlersExpansion] Yayo's Animation not found, skipping compatibility setup");
					return;
				}

				_animationCoreType = yayoAssembly.GetType("YayoAnimation.AnimationCore");
				if (_animationCoreType == null)
				{
					Log.Warning("[RimTalk_ToddlersExpansion] Could not find YayoAnimation.AnimationCore");
					return;
				}

				_checkAniMethod = AccessTools.Method(_animationCoreType, "CheckAni");
				if (_checkAniMethod == null)
				{
					Log.Warning("[RimTalk_ToddlersExpansion] Could not find AnimationCore.CheckAni method");
					return;
				}

				_pawnDrawDataType = yayoAssembly.GetType("YayoAnimation.Data.PawnDrawData");
				if (_pawnDrawDataType != null)
				{
					_resetMethod = AccessTools.Method(_pawnDrawDataType, "Reset");
					_fixedRotField = AccessTools.Field(_pawnDrawDataType, "fixedRot");
					_angleOffsetField = AccessTools.Field(_pawnDrawDataType, "angleOffset");
					_posOffsetField = AccessTools.Field(_pawnDrawDataType, "posOffset");
					_aniStandingMethod = AccessTools.Method(
						_animationCoreType,
						"AniStanding",
						new[] { typeof(Pawn), typeof(Rot4).MakeByRefType(), _pawnDrawDataType, typeof(string) });
				}

				_yayoAnimationLoaded = true;
				Log.Message("[RimTalk_ToddlersExpansion] Yayo's Animation compatibility initialized successfully");
			}
			catch (Exception ex)
			{
				Log.Error($"[RimTalk_ToddlersExpansion] Error initializing Yayo's Animation compatibility: {ex}");
			}
		}

		public static void ApplyPatches(HarmonyLib.Harmony harmony)
		{
			if (!IsYayoAnimationLoaded || _checkAniMethod == null)
			{
				return;
			}

			try
			{
				harmony.Patch(
					_checkAniMethod,
					prefix: new HarmonyMethod(typeof(YayoAnimationCompatUtility), nameof(CheckAni_Prefix)));
				Log.Message("[RimTalk_ToddlersExpansion] Applied Yayo's Animation CheckAni patch");
			}
			catch (Exception ex)
			{
				Log.Error($"[RimTalk_ToddlersExpansion] Error patching Yayo's Animation: {ex}");
			}
		}

		private static bool CheckAni_Prefix(Pawn pawn, Rot4 rot, object pdd)
		{
			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (carrier != null)
			{
				try
				{
					if (pdd != null)
					{
						if (_resetMethod != null)
						{
							_resetMethod.Invoke(pdd, null);
						}

						if (_fixedRotField != null)
						{
							_fixedRotField.SetValue(pdd, carrier.Rotation);
						}
					}
				}
				catch
				{
				}

				return true;
			}

			if (_suppressedPawns.Contains(pawn))
			{
				try
				{
					if (_resetMethod != null && pdd != null)
					{
						_resetMethod.Invoke(pdd, null);
					}
				}
				catch
				{
				}

				return false;
			}

			if (IsSmallPawnPlayJob(pawn) && TryApplySmallPawnPlayAnimationFromYayo(pawn, rot, pdd))
			{
				return false;
			}

			return true;
		}

		public static bool IsYayoAnimationLoaded
		{
			get
			{
				if (!_initialized)
				{
					Initialize();
				}

				return _yayoAnimationLoaded;
			}
		}

		public static void StartSuppression(Pawn pawn)
		{
			if (pawn != null)
			{
				_suppressedPawns.Add(pawn);
			}
		}

		public static void StopSuppression(Pawn pawn)
		{
			if (pawn != null)
			{
				_suppressedPawns.Remove(pawn);
			}
		}

		public static bool IsSuppressed(Pawn pawn)
		{
			return pawn != null && _suppressedPawns.Contains(pawn);
		}

		public static void ClearAllSuppressions()
		{
			_suppressedPawns.Clear();
		}

		[Obsolete("Use StartSuppression instead")]
		public static void ResetPawnAnimation(Pawn pawn)
		{
		}

		[Obsolete("Use StartSuppression instead")]
		public static void SetFixedRotation(Pawn pawn, Rot4 rotation)
		{
			StartSuppression(pawn);
		}

		[Obsolete("Use StartSuppression instead")]
		public static void SuppressAnimation(Pawn pawn, int durationTicks = 60)
		{
			StartSuppression(pawn);
		}

		private static bool IsSmallPawnPlayJob(Pawn pawn)
		{
			if (pawn == null || pawn.CurJobDef == null)
			{
				return false;
			}

			bool isBaby = pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby();
			bool isToddler = ToddlersCompatUtility.IsToddler(pawn);
			if (!isBaby && !isToddler)
			{
				return false;
			}

			JobDef jobDef = pawn.CurJobDef;
			if (jobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerPlayAtToy)
			{
				return true;
			}

			string name = jobDef.defName;
			if (name.NullOrEmpty())
			{
				return false;
			}

			if (isBaby)
			{
				return name.StartsWith("RimTalk_ToddlerSelfPlay", StringComparison.Ordinal)
					|| name == "BabyPlay"
					|| name == "BePlayedWith"
					|| name == "PlayStatic"
					|| name == "PlayWalking"
					|| name == "PlayToys"
					|| name == "PlayCrib"
					|| name == "ToddlerPlayToys"
					|| name == "ToddlerBugwatching"
					|| name == "ToddlerSkydreaming"
					|| name == "ToddlerPlayDecor";
			}

			// Keep toddler scope to original Toddlers play jobs to avoid overriding RimTalk custom animation jobs.
			return name == "ToddlerFloordrawing"
				|| name == "ToddlerSkydreaming"
				|| name == "ToddlerBugwatching"
				|| name == "ToddlerPlayToys"
				|| name == "ToddlerWatchTelevision"
				|| name == "ToddlerFiregazing"
				|| name == "ToddlerPlayDecor";
		}

		private static bool TryApplySmallPawnPlayAnimationFromYayo(Pawn pawn, Rot4 rot, object pdd)
		{
			if (_aniStandingMethod == null || pdd == null)
			{
				return false;
			}

			try
			{
				string profile = SelectBabyPlayYayoProfile(pawn);
				if (profile == "ExtinguishSelf" && pawn.pather?.MovingNow == true)
				{
					// Extinguish profile has very large translation; avoid it while moving.
					profile = "PlayToys";
				}

				object[] args = { pawn, rot, pdd, profile };
				_aniStandingMethod.Invoke(null, args);
				ScaleSmallPawnYayoOffsets(pawn, pdd);

				if (Prefs.DevMode)
				{
					int pawnId = pawn.thingIDNumber;
					int jobId = pawn.CurJob?.loadID ?? -1;
					if (!_loggedBabyPlayYayoAnimation)
					{
						_loggedBabyPlayYayoAnimation = true;
						Log.Message("[RimTalk_ToddlersExpansion] Yayo baby play animation bridge enabled.");
					}

					if (!_lastLoggedBabyPlayJobByPawn.TryGetValue(pawnId, out int lastJobId) || lastJobId != jobId)
					{
						_lastLoggedBabyPlayJobByPawn[pawnId] = jobId;
						Log.Message($"[RimTalk_ToddlersExpansion] Small-pawn play animation profile: pawn={pawn.LabelShort} profile={profile} job={pawn.CurJobDef?.defName ?? "null"}");
					}
				}

				return true;
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to apply Yayo baby play animation bridge: {ex.Message}");
				}

				return false;
			}
		}

		private static string SelectBabyPlayYayoProfile(Pawn pawn)
		{
			if (BabyPlayYayoProfiles.Length == 0)
			{
				return "PlayToys";
			}

			int seed = Gen.HashCombineInt(pawn?.thingIDNumber ?? 0, pawn?.CurJob?.loadID ?? 0);
			int index = (seed & int.MaxValue) % BabyPlayYayoProfiles.Length;
			return BabyPlayYayoProfiles[index];
		}

		private static void ScaleSmallPawnYayoOffsets(Pawn pawn, object pdd)
		{
			if (pawn == null || pdd == null || _angleOffsetField == null || _posOffsetField == null)
			{
				return;
			}

			float angleScale;
			float posScale;
			if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby())
			{
				angleScale = 0.42f;
				posScale = 0.18f;
			}
			else
			{
				angleScale = 0.55f;
				posScale = 0.24f;
			}

			if (pawn.pather?.MovingNow == true)
			{
				angleScale *= 0.8f;
				posScale *= 0.65f;
			}

			try
			{
				float angle = (float)_angleOffsetField.GetValue(pdd);
				Vector3 pos = (Vector3)_posOffsetField.GetValue(pdd);
				_angleOffsetField.SetValue(pdd, angle * angleScale);
				_posOffsetField.SetValue(pdd, pos * posScale);
			}
			catch
			{
			}
		}
	}
}
