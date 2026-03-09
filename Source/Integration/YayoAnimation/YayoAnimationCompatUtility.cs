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
		private enum SmallPawnPlayProfileKind
		{
			YayoStanding,
			CustomWiggle,
			CustomSway,
			CustomLay,
			CustomProne,
			CustomWobble,
			CustomRoll,
			CustomSpin,
			CustomHop,
			CustomRunLoop
		}

		private readonly struct SmallPawnPlayProfile
		{
			public SmallPawnPlayProfile(SmallPawnPlayProfileKind kind, string yayoProfile = null)
			{
				Kind = kind;
				YayoProfile = yayoProfile;
			}

			public SmallPawnPlayProfileKind Kind { get; }
			public string YayoProfile { get; }
		}

		private const string ToddlerSpinProfile = "RimTalk_ToddlerSpin";
		private const string ToddlerHopProfile = "RimTalk_ToddlerHop";
		private const string ToddlerRunLoopProfile = "RimTalk_ToddlerRunLoop";

		private static bool _initialized;
		private static bool _yayoAnimationLoaded;
		private static bool _loggedBabyPlayYayoAnimation;
		private static bool _walkHediffChecked;
		private static readonly Dictionary<int, int> _lastLoggedBabyPlayJobByPawn = new Dictionary<int, int>(32);
		private static readonly SmallPawnPlayProfile[] BabyPlayProfiles =
		{
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "PlayToys"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "Play_Hoopstone"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "Play_DartsBoard"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "GoldenCubePlay"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.CustomRoll, "ExtinguishSelf"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "SocialRelax")
		};
		private static readonly SmallPawnPlayProfile[] ToddlerPlayProfiles =
		{
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.CustomWiggle, "RimTalk_ToddlerPlay_Wiggle"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.CustomLay, "RimTalk_ToddlerPlay_Lay"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.CustomProne, "RimTalk_ToddlerPlay_Crawl"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.CustomSway, "RimTalk_ToddlerPlay_Sway"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.CustomWobble, "ToddlerWobble"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "PlayToys"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "Play_Hoopstone"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "Play_DartsBoard"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "GoldenCubePlay"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.CustomRoll, "ExtinguishSelf"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "SocialRelax"),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.CustomSpin, ToddlerSpinProfile)
		};
		private static readonly SmallPawnPlayProfile[] ToddlerMobileSelfPlayProfiles =
		{
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.CustomHop, ToddlerHopProfile),
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.CustomRunLoop, ToddlerRunLoopProfile)
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
		private static HediffDef _learningToWalkDef;

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

		public static bool ShouldUseYayoPlayAnimation(Pawn pawn)
		{
			return IsYayoAnimationLoaded && ToddlersCompatUtility.IsToddlerOrBaby(pawn);
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
				return pawn.jobs?.curDriver?.CurToilString == "ToddlerPlayAtToy";
			}

			if (jobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob)
			{
				return pawn.jobs?.curDriver?.CurToilString == "ToddlerMutualPlay";
			}

			if (jobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayPartnerJob)
			{
				return pawn.jobs?.curDriver?.CurToilString == "ToddlerMutualPlayPartner";
			}

			string name = jobDef.defName;
			if (name.NullOrEmpty())
			{
				return false;
			}

			if (name.StartsWith("RimTalk_ToddlerSelfPlay", StringComparison.Ordinal))
			{
				return pawn.jobs?.curDriver?.CurToilString == "ToddlerSelfPlay";
			}

			if (isBaby)
			{
				return name == "BabyPlay"
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
				SmallPawnPlayProfile profile = SelectSmallPawnPlayProfile(pawn);
				if (!TryApplySelectedPlayProfile(pawn, rot, pdd, profile))
				{
					return false;
				}

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
						Log.Message($"[RimTalk_ToddlersExpansion] Small-pawn play animation profile: pawn={pawn.LabelShort} profile={profile.YayoProfile ?? profile.Kind.ToString()} job={pawn.CurJobDef?.defName ?? "null"}");
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

		private static bool TryApplyToddlerRollProfile(Pawn pawn, Rot4 rot, object pdd)
		{
			if (pawn == null || pdd == null)
			{
				return false;
			}

			try
			{
				if (_resetMethod != null)
				{
					_resetMethod.Invoke(pdd, null);
				}

				if (_fixedRotField != null)
				{
					_fixedRotField.SetValue(pdd, rot);
				}

				if (_angleOffsetField == null || _posOffsetField == null)
				{
					return false;
				}

				int cycleTicks = pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby() ? 240 : 210;
				int tick = Find.TickManager?.TicksGame ?? 0;
				int seedOffset = Mathf.Abs(Gen.HashCombineInt(pawn.thingIDNumber, pawn.CurJob?.loadID ?? 0)) % cycleTicks;
				float phase = ((tick + seedOffset) % cycleTicks) / (float)cycleTicks;

				GetToddlerRollPose(phase, pawn, out float angle, out Vector3 pos);
				_angleOffsetField.SetValue(pdd, angle);
				_posOffsetField.SetValue(pdd, pos);
				return true;
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to apply custom toddler roll profile: {ex.Message}");
				}

				return false;
			}
		}

		private static SmallPawnPlayProfile SelectSmallPawnPlayProfile(Pawn pawn)
		{
			if (pawn != null && ToddlersCompatUtility.IsToddler(pawn))
			{
				int seed = Gen.HashCombineInt(pawn.thingIDNumber, pawn.CurJob?.loadID ?? 0) & int.MaxValue;
				bool allowActiveSelfPlay = IsMobileToddlerSelfPlay(pawn);
				int total = ToddlerPlayProfiles.Length + (allowActiveSelfPlay ? ToddlerMobileSelfPlayProfiles.Length : 0);
				if (total <= 0)
				{
					return new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "PlayToys");
				}

				int index = seed % total;
				if (index < ToddlerPlayProfiles.Length)
				{
					return ToddlerPlayProfiles[index];
				}

				return ToddlerMobileSelfPlayProfiles[index - ToddlerPlayProfiles.Length];
			}

			if (BabyPlayProfiles.Length == 0)
			{
				return new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "PlayToys");
			}

			int babySeed = Gen.HashCombineInt(pawn?.thingIDNumber ?? 0, pawn?.CurJob?.loadID ?? 0);
			int babyIndex = (babySeed & int.MaxValue) % BabyPlayProfiles.Length;
			return BabyPlayProfiles[babyIndex];
		}

		private static bool TryApplySelectedPlayProfile(Pawn pawn, Rot4 rot, object pdd, SmallPawnPlayProfile profile)
		{
			switch (profile.Kind)
			{
				case SmallPawnPlayProfileKind.CustomWiggle:
					return TryApplyToddlerWiggleProfile(rot, pdd);
				case SmallPawnPlayProfileKind.CustomSway:
					return TryApplyToddlerSwayProfile(rot, pdd);
				case SmallPawnPlayProfileKind.CustomLay:
					return TryApplyToddlerLayProfile(rot, pdd);
				case SmallPawnPlayProfileKind.CustomProne:
					return TryApplyToddlerProneProfile(rot, pdd);
				case SmallPawnPlayProfileKind.CustomWobble:
					return TryApplyToddlerWobbleProfile(pawn, rot, pdd);
				case SmallPawnPlayProfileKind.CustomRoll:
					if (pawn.pather?.MovingNow == true)
					{
						return TryApplySelectedPlayProfile(
							pawn,
							rot,
							pdd,
							new SmallPawnPlayProfile(SmallPawnPlayProfileKind.YayoStanding, "PlayToys"));
					}

					return TryApplyToddlerRollProfile(pawn, rot, pdd);
				case SmallPawnPlayProfileKind.CustomSpin:
					return TryApplyToddlerSpinProfile(pawn, rot, pdd);
				case SmallPawnPlayProfileKind.CustomHop:
					return TryApplyToddlerHopProfile(pawn, rot, pdd);
				case SmallPawnPlayProfileKind.CustomRunLoop:
					return TryApplyToddlerRunLoopProfile(pawn, pdd);
				default:
					return TryApplyYayoStandingProfile(pawn, rot, pdd, profile.YayoProfile);
			}
		}

		private static bool TryApplyYayoStandingProfile(Pawn pawn, Rot4 rot, object pdd, string yayoProfile)
		{
			if (yayoProfile.NullOrEmpty())
			{
				yayoProfile = "PlayToys";
			}

			object[] args = { pawn, rot, pdd, yayoProfile };
			_aniStandingMethod.Invoke(null, args);
			ScaleSmallPawnYayoOffsets(pawn, pdd, yayoProfile);
			return true;
		}

		private static bool IsMobileToddlerSelfPlay(Pawn pawn)
		{
			if (pawn == null || !ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			JobDef jobDef = pawn.CurJobDef;
			if (jobDef?.defName.NullOrEmpty() != false || !jobDef.defName.StartsWith("RimTalk_ToddlerSelfPlay", StringComparison.Ordinal))
			{
				return false;
			}

			if (pawn.jobs?.curDriver?.CurToilString != "ToddlerSelfPlay")
			{
				return false;
			}

			EnsureWalkDef();
			if (_learningToWalkDef == null || pawn.health?.hediffSet == null)
			{
				return true;
			}

			Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(_learningToWalkDef);
			return hediff == null || hediff.Severity >= 0.5f;
		}

		private static bool TryApplyToddlerWiggleProfile(Rot4 rot, object pdd)
		{
			if (!TryPrepareCustomPose(pdd, rot))
			{
				return false;
			}

			float x = GetWavePhase(120);
			_angleOffsetField.SetValue(pdd, 12f * TriangleWave(x));
			_posOffsetField.SetValue(pdd, Vector3.zero);
			return true;
		}

		private static bool TryApplyToddlerSwayProfile(Rot4 rot, object pdd)
		{
			if (!TryPrepareCustomPose(pdd, rot))
			{
				return false;
			}

			float x = GetWavePhase(90);
			float wave = Mathf.Sin(x * Mathf.PI * 2f);
			_angleOffsetField.SetValue(pdd, 8f * wave);
			_posOffsetField.SetValue(pdd, new Vector3(0f, 0f, wave * 0.01f));
			return true;
		}

		private static bool TryApplyToddlerLayProfile(Rot4 rot, object pdd)
		{
			if (!TryPrepareCustomPose(pdd, rot))
			{
				return false;
			}

			float angle = 0f;
			if (rot == Rot4.East)
			{
				angle = -35f;
			}
			else if (rot == Rot4.West)
			{
				angle = 35f;
			}
			else if (rot == Rot4.North)
			{
				angle = 180f;
			}

			_angleOffsetField.SetValue(pdd, angle);
			_posOffsetField.SetValue(pdd, new Vector3(0.02f, 0f, 0f));
			return true;
		}

		private static bool TryApplyToddlerProneProfile(Rot4 rot, object pdd)
		{
			if (!TryPrepareCustomPose(pdd, rot))
			{
				return false;
			}

			float angle = 0f;
			if (rot == Rot4.East)
			{
				angle = 40f;
			}
			else if (rot == Rot4.West)
			{
				angle = -40f;
			}

			_angleOffsetField.SetValue(pdd, angle);
			_posOffsetField.SetValue(pdd, Vector3.zero);
			return true;
		}

		private static bool TryApplyToddlerWobbleProfile(Pawn pawn, Rot4 rot, object pdd)
		{
			if (!TryPrepareCustomPose(pdd, rot))
			{
				return false;
			}

			float severity = GetLearningToWalkSeverity(pawn);
			int cycleTicks = Mathf.RoundToInt(Mathf.Lerp(90f, 60f, Mathf.Clamp01(severity)));
			float x = GetWavePhase(cycleTicks);
			float angleMagnitude = Mathf.Lerp(10f, 5f, Mathf.Clamp01(severity));
			_angleOffsetField.SetValue(pdd, angleMagnitude * TriangleWave(x));
			_posOffsetField.SetValue(pdd, Vector3.zero);
			return true;
		}

		private static void GetToddlerRollPose(float phase, Pawn pawn, out float angle, out Vector3 pos)
		{
			float angleAmplitude = pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby() ? 18f : 24f;
			float posAmplitude = pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby() ? 0.035f : 0.05f;

			if (phase < 0.20f)
			{
				angle = 0f;
				pos = Vector3.zero;
				return;
			}

			if (phase < 0.38f)
			{
				float t = EaseInOut((phase - 0.20f) / 0.18f);
				angle = Mathf.Lerp(0f, angleAmplitude, t);
				pos = new Vector3(Mathf.Lerp(0f, posAmplitude, t), 0f, Mathf.Lerp(0f, posAmplitude * 0.4f, t));
				return;
			}

			if (phase < 0.48f)
			{
				angle = angleAmplitude;
				pos = new Vector3(posAmplitude, 0f, posAmplitude * 0.4f);
				return;
			}

			if (phase < 0.62f)
			{
				float t = EaseInOut((phase - 0.48f) / 0.14f);
				angle = Mathf.Lerp(angleAmplitude, -angleAmplitude, t);
				pos = new Vector3(Mathf.Lerp(posAmplitude, -posAmplitude, t), 0f, Mathf.Lerp(posAmplitude * 0.4f, -posAmplitude * 0.4f, t));
				return;
			}

			if (phase < 0.72f)
			{
				angle = -angleAmplitude;
				pos = new Vector3(-posAmplitude, 0f, -posAmplitude * 0.4f);
				return;
			}

			if (phase < 0.86f)
			{
				float t = EaseInOut((phase - 0.72f) / 0.14f);
				angle = Mathf.Lerp(-angleAmplitude, 0f, t);
				pos = new Vector3(Mathf.Lerp(-posAmplitude, 0f, t), 0f, Mathf.Lerp(-posAmplitude * 0.4f, 0f, t));
				return;
			}

			angle = 0f;
			pos = Vector3.zero;
		}

		private static bool TryApplyToddlerSpinProfile(Pawn pawn, Rot4 rot, object pdd)
		{
			if (!TryPrepareCustomPose(pdd, rot))
			{
				return false;
			}

			try
			{
				int cycleTicks = 300;
				int tick = Find.TickManager?.TicksGame ?? 0;
				int seedOffset = Mathf.Abs(Gen.HashCombineInt(pawn?.thingIDNumber ?? 0, pawn?.CurJob?.loadID ?? 0)) % cycleTicks;
				float phase = ((tick + seedOffset) % cycleTicks) / (float)cycleTicks;

				float angle;
				if (phase < 0.16f)
				{
					angle = 0f;
				}
				else if (phase < 0.36f)
				{
					angle = Mathf.Lerp(0f, 110f, EaseInOut((phase - 0.16f) / 0.20f));
				}
				else if (phase < 0.48f)
				{
					angle = 110f;
				}
				else if (phase < 0.70f)
				{
					angle = Mathf.Lerp(110f, 250f, EaseInOut((phase - 0.48f) / 0.22f));
				}
				else if (phase < 0.82f)
				{
					angle = 250f;
				}
				else
				{
					angle = Mathf.Lerp(250f, 360f, EaseInOut((phase - 0.82f) / 0.18f));
				}

				_angleOffsetField.SetValue(pdd, angle);
				_posOffsetField.SetValue(pdd, Vector3.zero);
				return true;
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to apply toddler spin profile: {ex.Message}");
				}

				return false;
			}
		}

		private static bool TryApplyToddlerHopProfile(Pawn pawn, Rot4 rot, object pdd)
		{
			if (!TryPrepareCustomPose(pdd, rot))
			{
				return false;
			}

			try
			{
				int cycleTicks = 260;
				int tick = Find.TickManager?.TicksGame ?? 0;
				int seedOffset = Mathf.Abs(Gen.HashCombineInt((pawn?.thingIDNumber ?? 0) ^ 1769, pawn?.CurJob?.loadID ?? 0)) % cycleTicks;
				float phase = ((tick + seedOffset) % cycleTicks) / (float)cycleTicks;

				float height = 0f;
				float angle = 0f;
				float xOffset = 0f;
				if (phase < 0.14f)
				{
					angle = 0f;
				}
				else if (phase < 0.32f)
				{
					float t = (phase - 0.14f) / 0.18f;
					height = Mathf.Sin(t * Mathf.PI) * 0.060f;
					angle = Mathf.Lerp(-6f, 8f, t);
					xOffset = Mathf.Lerp(-0.010f, 0.012f, t);
				}
				else if (phase < 0.48f)
				{
					angle = 0f;
				}
				else if (phase < 0.66f)
				{
					float t = (phase - 0.48f) / 0.18f;
					height = Mathf.Sin(t * Mathf.PI) * 0.075f;
					angle = Mathf.Lerp(7f, -9f, t);
					xOffset = Mathf.Lerp(0.012f, -0.014f, t);
				}
				else if (phase < 0.80f)
				{
					angle = 0f;
				}
				else
				{
					float t = (phase - 0.80f) / 0.20f;
					height = Mathf.Sin(t * Mathf.PI) * 0.055f;
					angle = Mathf.Lerp(-5f, 6f, t);
					xOffset = Mathf.Lerp(-0.008f, 0.010f, t);
				}

				_angleOffsetField.SetValue(pdd, angle);
				_posOffsetField.SetValue(pdd, new Vector3(xOffset, height, 0f));
				return true;
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to apply toddler hop profile: {ex.Message}");
				}

				return false;
			}
		}

		private static bool TryApplyToddlerRunLoopProfile(Pawn pawn, object pdd)
		{
			if (!TryPrepareCustomPose(pdd, Rot4.East))
			{
				return false;
			}

			try
			{
				int cycleTicks = 360;
				int tick = Find.TickManager?.TicksGame ?? 0;
				int seedOffset = Mathf.Abs(Gen.HashCombineInt((pawn?.thingIDNumber ?? 0) ^ 3037, pawn?.CurJob?.loadID ?? 0)) % cycleTicks;
				float phase = ((tick + seedOffset) % cycleTicks) / (float)cycleTicks;

				Vector3 east = new Vector3(0.34f, 0f, 0f);
				Vector3 north = new Vector3(0f, 0f, 0.34f);
				Vector3 west = new Vector3(-0.34f, 0f, 0f);
				Vector3 south = new Vector3(0f, 0f, -0.34f);

				Vector3 pos;
				Rot4 facing;
				float angle;
				if (phase < 0.10f)
				{
					pos = east;
					facing = Rot4.East;
					angle = 5f;
				}
				else if (phase < 0.30f)
				{
					float t = EaseInOut((phase - 0.10f) / 0.20f);
					pos = Vector3.Lerp(east, north, t);
					facing = Rot4.North;
					angle = Mathf.Lerp(5f, -3f, t);
				}
				else if (phase < 0.40f)
				{
					pos = north;
					facing = Rot4.North;
					angle = -3f;
				}
				else if (phase < 0.60f)
				{
					float t = EaseInOut((phase - 0.40f) / 0.20f);
					pos = Vector3.Lerp(north, west, t);
					facing = Rot4.West;
					angle = Mathf.Lerp(-3f, -5f, t);
				}
				else if (phase < 0.70f)
				{
					pos = west;
					facing = Rot4.West;
					angle = -5f;
				}
				else if (phase < 0.85f)
				{
					float t = EaseInOut((phase - 0.70f) / 0.15f);
					pos = Vector3.Lerp(west, south, t);
					facing = Rot4.South;
					angle = Mathf.Lerp(-5f, 4f, t);
				}
				else if (phase < 0.92f)
				{
					pos = south;
					facing = Rot4.South;
					angle = 4f;
				}
				else
				{
					float t = EaseInOut((phase - 0.92f) / 0.08f);
					pos = Vector3.Lerp(south, east, t);
					facing = Rot4.East;
					angle = Mathf.Lerp(4f, 5f, t);
				}

				_fixedRotField?.SetValue(pdd, facing);
				_angleOffsetField.SetValue(pdd, angle);
				_posOffsetField.SetValue(pdd, pos);
				return true;
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to apply toddler run-loop profile: {ex.Message}");
				}

				return false;
			}
		}

		private static bool TryPrepareCustomPose(object pdd, Rot4 rot)
		{
			if (pdd == null || _angleOffsetField == null || _posOffsetField == null)
			{
				return false;
			}

			try
			{
				if (_resetMethod != null)
				{
					_resetMethod.Invoke(pdd, null);
				}

				_fixedRotField?.SetValue(pdd, rot);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static float EaseInOut(float t)
		{
			t = Mathf.Clamp01(t);
			return t * t * (3f - 2f * t);
		}

		private static void EnsureWalkDef()
		{
			if (_walkHediffChecked)
			{
				return;
			}

			_walkHediffChecked = true;
			_learningToWalkDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningToWalk");
		}

		private static float GetLearningToWalkSeverity(Pawn pawn)
		{
			EnsureWalkDef();
			if (_learningToWalkDef == null || pawn?.health?.hediffSet == null)
			{
				return 1f;
			}

			Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(_learningToWalkDef);
			return Mathf.Clamp01(hediff?.Severity ?? 1f);
		}

		private static float GetWavePhase(int cycleTicks)
		{
			if (cycleTicks <= 0)
			{
				cycleTicks = 60;
			}

			int tick = Find.TickManager?.TicksGame ?? 0;
			return (tick % cycleTicks) / (float)cycleTicks;
		}

		private static float TriangleWave(float x)
		{
			x = Mathf.Clamp01(x);
			if (x <= 0.25f)
			{
				return x * 4f;
			}

			if (x <= 0.5f)
			{
				return (0.5f - x) * 4f;
			}

			if (x <= 0.75f)
			{
				return -4f * (x - 0.5f);
			}

			return -4f * (1f - x);
		}

		private static void ScaleSmallPawnYayoOffsets(Pawn pawn, object pdd, string profile)
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
