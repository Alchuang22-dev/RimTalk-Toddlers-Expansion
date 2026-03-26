using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.BioTech;
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
			CustomYayoSocial,
			CustomWiggle,
			CustomSway,
			CustomLay,
			CustomProne,
			CustomWobble,
			CustomBabyRoll,
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
		private const float YayoAngleReduce = 0.5f;
		private const float YayoAngleToPos = 0.01f;
		private static readonly Vector3 YayoZOffset005 = new Vector3(0f, 0f, 0.05f);
		private static readonly SmallPawnPlayProfile ToddlerPlayToysProfile =
			new SmallPawnPlayProfile(SmallPawnPlayProfileKind.CustomYayoSocial, "PlayToys");

		private static bool _initialized;
		private static bool _yayoAnimationLoaded;
		private static bool _loggedBabyPlayYayoAnimation;
		private static bool _loggedCustomYayoSocialRotationLock;
		private static bool _walkHediffChecked;
		private static readonly Dictionary<int, int> _lastLoggedBabyPlayJobByPawn = new Dictionary<int, int>(32);
		private static readonly Dictionary<int, string> _lastLoggedPlayDecisionByPawn = new Dictionary<int, string>(32);

		private static readonly HashSet<Pawn> _suppressedPawns = new HashSet<Pawn>();
		private static readonly HashSet<Pawn> _safeFallbackTrackedPawns = new HashSet<Pawn>();

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
			bool isSmallPlayJob = IsSmallPawnPlayJob(pawn);
			bool isEngagedPlay = ToddlersCompatUtility.IsEngagedInToddlerPlay(pawn);
			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (carrier != null)
			{
				LogPlayAnimationDecision(pawn, $"default render allowed: custom-carried by {carrier.LabelShort}");
				return true;
			}

			if (isEngagedPlay && !isSmallPlayJob)
			{
				if (ShouldLogMissingPlayClassification(pawn))
				{
					LogPlayAnimationDecision(pawn, "default render allowed: engaged play job not classified as small-pawn play");
				}

				return true;
			}

			if (_suppressedPawns.Contains(pawn))
			{
				LogPlayAnimationDecision(pawn, "default render allowed: explicit suppression active");
				return true;
			}

			if (isSmallPlayJob && !HasAnyEnabledPlayProfile(pawn))
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

				LogPlayAnimationDecision(pawn, "fallback: no enabled play profiles");
				return false;
			}

			if (isSmallPlayJob && ShouldUseStandingOnlySmallPlayProfile(pawn))
			{
				if (TryApplyStandingOnlySmallPlayProfile(pawn, rot, pdd))
				{
					LogPlayAnimationDecision(pawn, "standing-only small-pawn play profile applied");
					return false;
				}
			}

			if (isSmallPlayJob && TryGetNativePlayAnimationOverride(pawn, out AnimationDef nativeAnimation))
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

				LogPlayAnimationDecision(pawn, $"native animation selected: {nativeAnimation?.defName ?? "null"}");
				return false;
			}

			if (isSmallPlayJob && TryApplySmallPawnPlayAnimationFromYayo(pawn, rot, pdd))
			{
				return false;
			}

			if (isSmallPlayJob)
			{
				LogPlayAnimationDecision(pawn, "fallback: no native or custom Yayo profile applied");
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
			return IsYayoAnimationLoaded
				&& ToddlersCompatUtility.IsToddlerOrBaby(pawn)
				&& ToddlerPlayAnimationUtility.ArePlayAnimationsAllowedForPawn(pawn);
		}

		public static bool ShouldAllowManagedPlayAnimation(Pawn pawn)
		{
			return ToddlersCompatUtility.IsToddlerOrBaby(pawn)
				&& ToddlerPlayAnimationUtility.ArePlayAnimationsAllowedForPawn(pawn)
				&& IsSmallPawnPlayJob(pawn)
				&& !ToddlerPlayAnimationUtility.ShouldDelayPlayAnimationForMovement(pawn)
				&& !IsSuppressed(pawn)
				&& !ToddlerCarryingUtility.IsBeingCarried(pawn);
		}

		public static bool TryGetNativePlayAnimationOverride(Pawn pawn, out AnimationDef animation)
		{
			animation = null;
			if (!ShouldUseYayoPlayAnimation(pawn))
			{
				return false;
			}

			if (!HasAnyEnabledPlayProfile(pawn))
			{
				return false;
			}

			SmallPawnPlayProfile profile = SelectSmallPawnPlayProfile(pawn);
			switch (profile.Kind)
			{
				case SmallPawnPlayProfileKind.CustomWiggle:
					animation = ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Wiggle;
					break;
				case SmallPawnPlayProfileKind.CustomSway:
					animation = ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Sway;
					break;
				case SmallPawnPlayProfileKind.CustomLay:
					animation = ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Lay;
					break;
				case SmallPawnPlayProfileKind.CustomProne:
					animation = ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Crawl;
					break;
			}

			return animation != null;
		}

		public static bool ShouldEnableManagedPlayAnimation(Pawn pawn)
		{
			return IsYayoAnimationLoaded && ShouldAllowManagedPlayAnimation(pawn);
		}

		public static void SyncSafeNativePlayAnimation(Pawn pawn)
		{
			if (pawn?.Drawer?.renderer == null)
			{
				UntrackSafeFallbackPawn(pawn);
				return;
			}

			bool shouldEnableManaged = ShouldEnableManagedPlayAnimation(pawn);
			AnimationDef animation = null;
			bool hasNativeOverride = shouldEnableManaged && TryGetNativePlayAnimationOverride(pawn, out animation);
			if (!shouldEnableManaged || !hasNativeOverride)
			{
				if (!ShouldKeepSafeFallbackPawnTracked(pawn))
				{
					UntrackSafeFallbackPawn(pawn);
				}
				return;
			}

			TrackSafeFallbackPawn(pawn);
			if (pawn.Drawer.renderer.CurAnimation != animation)
			{
				ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, animation);
				LogPlayAnimationDecision(pawn, $"safe native fallback applied: {animation.defName}");
			}
		}

		public static void StartSuppression(Pawn pawn)
		{
			if (pawn != null)
			{
				_suppressedPawns.Add(pawn);
				if (!ShouldKeepSafeFallbackPawnTracked(pawn))
				{
					UntrackSafeFallbackPawn(pawn);
				}
			}
		}

		public static void StopSuppression(Pawn pawn)
		{
			if (pawn != null)
			{
				_suppressedPawns.Remove(pawn);
				if (ShouldKeepSafeFallbackPawnTracked(pawn))
				{
					TrackSafeFallbackPawn(pawn);
				}
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

		public static bool IsRelevantSmallPawnCandidate(Pawn pawn)
		{
			return pawn != null
				&& !pawn.Dead
				&& !pawn.Destroyed
				&& pawn.Spawned
				&& ToddlersCompatUtility.IsToddlerOrBaby(pawn);
		}

		public static bool ShouldKeepSafeFallbackPawnTracked(Pawn pawn)
		{
			if (!IsRelevantSmallPawnCandidate(pawn))
			{
				return false;
			}

			if (ShouldEnableManagedPlayAnimation(pawn))
			{
				return true;
			}

			return ToddlerPlayAnimationUtility.HasManagedPlayAnimation(pawn);
		}

		public static void TrackSafeFallbackPawn(Pawn pawn)
		{
			if (!IsRelevantSmallPawnCandidate(pawn))
			{
				return;
			}

			_safeFallbackTrackedPawns.Add(pawn);
		}

		public static void UntrackSafeFallbackPawn(Pawn pawn)
		{
			if (pawn == null)
			{
				return;
			}

			_safeFallbackTrackedPawns.Remove(pawn);
		}

		public static void CopyTrackedSafeFallbackPawnsTo(List<Pawn> buffer)
		{
			if (buffer == null)
			{
				return;
			}

			buffer.Clear();
			foreach (Pawn pawn in _safeFallbackTrackedPawns)
			{
				buffer.Add(pawn);
			}
		}

		public static void ClearTrackedSafeFallbackPawns()
		{
			_safeFallbackTrackedPawns.Clear();
		}

		private static void LogPlayAnimationDecision(Pawn pawn, string decision)
		{
			if (!Prefs.DevMode || pawn == null)
			{
				return;
			}

			int pawnId = pawn.thingIDNumber;
			int jobId = pawn.CurJob?.loadID ?? -1;
			string jobName = pawn.CurJobDef?.defName ?? "null";
			string toil = pawn.jobs?.curDriver?.CurToilString ?? "null";
			string message = $"{jobId}|{jobName}|{toil}|{decision}";
			if (_lastLoggedPlayDecisionByPawn.TryGetValue(pawnId, out string lastMessage) && lastMessage == message)
			{
				return;
			}

			_lastLoggedPlayDecisionByPawn[pawnId] = message;
			Log.Message($"[RimTalk_ToddlersExpansion] Play animation decision: pawn={pawn.LabelShort} job={jobName} toil={toil} {decision}");
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

			bool isBaby = IsBabyOnly(pawn);
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

			if (IsExcludedNativeOrNonPlayJob(name) || IsExcludedByAdultPlayJob(pawn, name))
			{
				return false;
			}

			if (IsToddlerSelfPlayJobDef(name))
			{
				return IsToddlerSelfPlayToil(pawn);
			}

			if (name == "ToddlerPlayToys" || name == "ToddlerPlayDecor")
			{
				return pawn.jobs?.curDriver?.CurToilString == "ToddlerPlayToil";
			}

			if (name == "ToddlerFloordrawing")
			{
				return pawn.jobs?.curDriver?.CurToilString == "MakeNewToils";
			}

			if (isBaby)
			{
				return name == "BePlayedWith";
			}

			return isToddler && (name == "BabyPlay"
				|| name == "BePlayedWith"
				|| name == "PlayStatic"
				|| name == "PlayToys"
				|| name == "PlayCrib"
				|| name == "PlayRead"
				|| name == "ToddlerFloordrawing"
				|| name == "ToddlerSkydreaming"
				|| name == "ToddlerBugwatching"
				|| name == "ToddlerPlayToys"
				|| name == "ToddlerWatchTelevision"
				|| name == "ToddlerFiregazing"
				|| name == "ToddlerPlayDecor");
		}

		private static bool IsExcludedNativeOrNonPlayJob(string jobDefName)
		{
			return jobDefName == "PlayWalking"
				|| jobDefName == "LayAngleInCrib"
				|| jobDefName == "WiggleInCrib"
				|| jobDefName == "RestIdleInCrib"
				|| jobDefName == "ToddlerSkydreaming"
				|| jobDefName == "ToddlerBugwatching"
				|| jobDefName == "ToddlerWatchTelevision"
				|| jobDefName == "ToddlerFiregazing";
		}

		private static bool IsExcludedByAdultPlayJob(Pawn pawn, string jobDefName)
		{
			if (pawn?.CurJob == null || jobDefName != "BePlayedWith")
			{
				return false;
			}

			Pawn adult = pawn.CurJob.targetA.Thing as Pawn;
			string adultJobName = adult?.CurJobDef?.defName;
			return adultJobName == "PlayWalking";
		}

		private static bool ShouldLogMissingPlayClassification(Pawn pawn)
		{
			if (pawn?.CurJobDef == null)
			{
				return false;
			}

			string jobDefName = pawn.CurJobDef.defName;
			if (IsToddlerSelfPlayJobDef(jobDefName) && pawn.jobs?.curDriver?.CurToilString == "GotoCell")
			{
				return false;
			}

			if ((jobDefName == "ToddlerPlayToys" || jobDefName == "ToddlerPlayDecor")
				&& pawn.jobs?.curDriver?.CurToilString != "ToddlerPlayToil")
			{
				return false;
			}

			if (jobDefName == "ToddlerFloordrawing" && pawn.jobs?.curDriver?.CurToilString != "MakeNewToils")
			{
				return false;
			}

			if (pawn.CurJobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob
				&& pawn.jobs?.curDriver?.CurToilString == "GotoThing")
			{
				return false;
			}

			if (pawn.CurJobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayPartnerJob
				&& pawn.jobs?.curDriver?.CurToilString == "WaitForInitiator")
			{
				return false;
			}

			return true;
		}

		private static bool ShouldUseStandingOnlySmallPlayProfile(Pawn pawn)
		{
			return pawn?.CurJobDef?.defName == "ToddlerFloordrawing";
		}

		private static bool TryApplyStandingOnlySmallPlayProfile(Pawn pawn, Rot4 rot, object pdd)
		{
			if (pdd == null)
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

				return true;
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to apply standing-only play profile: {ex.Message}");
				}

				return false;
			}
		}

		private static bool IsToddlerSelfPlayJobDef(string jobDefName)
		{
			return !jobDefName.NullOrEmpty()
				&& jobDefName.StartsWith("RimTalk_ToddlerSelfPlay", StringComparison.Ordinal);
		}

		private static bool IsToddlerSelfPlayToil(Pawn pawn)
		{
			return pawn?.jobs?.curDriver?.CurToilString == "ToddlerSelfPlay";
		}

		private static bool TryApplySmallPawnPlayAnimationFromYayo(Pawn pawn, Rot4 rot, object pdd)
		{
			if (_aniStandingMethod == null || pdd == null)
			{
				return false;
			}

			try
			{
				if (!HasAnyEnabledPlayProfile(pawn))
				{
					return false;
				}

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

				int cycleTicks = IsBabyOnly(pawn) ? 240 : 210;
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
			List<SmallPawnPlayProfile> profiles = BuildEnabledPlayProfiles(pawn);
			if (profiles.Count == 0)
			{
				return default;
			}

			if (pawn != null && ToddlersCompatUtility.IsToddler(pawn))
			{
				bool allowActiveSelfPlay = IsMobileToddlerSelfPlay(pawn);
				List<SmallPawnPlayProfile> activeProfiles = allowActiveSelfPlay
					? profiles
					: FilterOutMobileOnlyProfiles(profiles);

				if (activeProfiles.Count == 0)
				{
					activeProfiles = profiles;
				}

				int seed = GetPlayProfileSeed(pawn) & int.MaxValue;
				return activeProfiles[seed % activeProfiles.Count];
			}

			int babySeed = Gen.HashCombineInt(pawn?.thingIDNumber ?? 0, pawn?.CurJob?.loadID ?? 0);
			return profiles[(babySeed & int.MaxValue) % profiles.Count];
		}

		private static int GetPlayProfileSeed(Pawn pawn)
		{
			if (TryGetMutualPlayPartner(pawn, out Pawn partner))
			{
				return GetSharedMutualPlaySeed(pawn, partner);
			}

			return Gen.HashCombineInt(pawn?.thingIDNumber ?? 0, pawn?.CurJob?.loadID ?? 0);
		}

		private static bool TryGetMutualPlayPartner(Pawn pawn, out Pawn partner)
		{
			partner = null;
			if (pawn?.CurJob == null)
			{
				return false;
			}

			if (pawn.CurJobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob
				|| pawn.CurJobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayPartnerJob)
			{
				partner = pawn.CurJob.targetA.Thing as Pawn;
			}

			return partner != null;
		}

		private static int GetSharedMutualPlaySeed(Pawn pawn, Pawn partner)
		{
			int first = Mathf.Min(pawn?.thingIDNumber ?? 0, partner?.thingIDNumber ?? 0);
			int second = Mathf.Max(pawn?.thingIDNumber ?? 0, partner?.thingIDNumber ?? 0);
			return Gen.HashCombineInt(first, second);
		}

		private static bool HasAnyEnabledPlayProfile(Pawn pawn)
		{
			return BuildEnabledPlayProfiles(pawn).Count > 0;
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
					return TryApplyToddlerProneProfile(pawn, rot, pdd);
				case SmallPawnPlayProfileKind.CustomWobble:
					return TryApplyToddlerWobbleProfile(pawn, rot, pdd);
				case SmallPawnPlayProfileKind.CustomBabyRoll:
					return TryApplyBabyRollProfile(pawn, rot, pdd);
				case SmallPawnPlayProfileKind.CustomRoll:
					if (pawn.pather?.MovingNow == true)
					{
						return TryApplySelectedPlayProfile(
							pawn,
							rot,
							pdd,
							ToddlerPlayToysProfile);
					}

					return TryApplyToddlerRollProfile(pawn, rot, pdd);
				case SmallPawnPlayProfileKind.CustomYayoSocial:
					return TryApplyToddlerYayoSocialProfile(pawn, rot, pdd, profile.YayoProfile);
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

		private static List<SmallPawnPlayProfile> BuildEnabledPlayProfiles(Pawn pawn)
		{
			bool isToddler = pawn != null && ToddlersCompatUtility.IsToddler(pawn);
			List<SmallPawnPlayProfile> profiles = new List<SmallPawnPlayProfile>(16);

			if (isToddler)
			{
				AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableNativePlayWiggle, SmallPawnPlayProfileKind.CustomWiggle, "RimTalk_ToddlerPlay_Wiggle");
				AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableNativePlayLay, SmallPawnPlayProfileKind.CustomLay, "RimTalk_ToddlerPlay_Lay");
				AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableNativePlayCrawl, SmallPawnPlayProfileKind.CustomProne, "RimTalk_ToddlerPlay_Crawl");
				AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableNativePlaySway, SmallPawnPlayProfileKind.CustomSway, "RimTalk_ToddlerPlay_Sway");
				AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableNativePlayToddlerWobble, SmallPawnPlayProfileKind.CustomWobble, "ToddlerWobble");
			}

			AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableYayoPlayToys, SmallPawnPlayProfileKind.CustomYayoSocial, "PlayToys");
			AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableYayoPlayHoopstone, SmallPawnPlayProfileKind.YayoStanding, "Play_Hoopstone");
			AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableYayoPlayDartsBoard, SmallPawnPlayProfileKind.YayoStanding, "Play_DartsBoard");
			AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableYayoGoldenCube, SmallPawnPlayProfileKind.CustomYayoSocial, "GoldenCubePlay");
			AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableYayoBabyRoll && CanUseBabyRollProfile(pawn), SmallPawnPlayProfileKind.CustomBabyRoll, "BabyRoll");
			AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableYayoCustomRoll, SmallPawnPlayProfileKind.CustomRoll, "BabyWiggle");
			AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableYayoSocialRelax, SmallPawnPlayProfileKind.CustomYayoSocial, "SocialRelax");

			if (isToddler)
			{
				AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableYayoCustomSpin, SmallPawnPlayProfileKind.CustomSpin, ToddlerSpinProfile);
				AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableYayoCustomHop, SmallPawnPlayProfileKind.CustomHop, ToddlerHopProfile);
				AddEnabledProfile(profiles, ToddlersExpansionSettings.EnableYayoCustomRunLoop, SmallPawnPlayProfileKind.CustomRunLoop, ToddlerRunLoopProfile);
			}

			return profiles;
		}

		private static void AddEnabledProfile(List<SmallPawnPlayProfile> profiles, bool enabled, SmallPawnPlayProfileKind kind, string profileName)
		{
			if (enabled)
			{
				profiles.Add(new SmallPawnPlayProfile(kind, profileName));
			}
		}

		private static List<SmallPawnPlayProfile> FilterOutMobileOnlyProfiles(List<SmallPawnPlayProfile> profiles)
		{
			List<SmallPawnPlayProfile> filtered = new List<SmallPawnPlayProfile>(profiles.Count);
			for (int i = 0; i < profiles.Count; i++)
			{
				if (profiles[i].Kind != SmallPawnPlayProfileKind.CustomHop
					&& profiles[i].Kind != SmallPawnPlayProfileKind.CustomRunLoop)
				{
					filtered.Add(profiles[i]);
				}
			}

			return filtered;
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

			// Yayo social/game standing profiles may rewrite fixedRot for adults.
			// On toddlers this reads like a frame-by-frame spin bug, so keep the current facing stable.
			if (_fixedRotField != null && ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				_fixedRotField.SetValue(pdd, rot);
			}

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

		private static bool CanUseBabyRollProfile(Pawn pawn)
		{
			if (pawn == null || IsBabyOnly(pawn) || ToddlerCarryingUtility.IsBeingCarried(pawn) || IsPawnInBedOrCrib(pawn))
			{
				return false;
			}

			JobDef jobDef = pawn.CurJobDef;
			string jobName = jobDef?.defName;
			if (jobName.NullOrEmpty() || jobName == "PlayCrib" || jobName == "PlayWalking")
			{
				return false;
			}

			if (jobName == "BePlayedWith")
			{
				return !IsExcludedByAdultPlayJob(pawn, jobName);
			}

			if (IsToddlerSelfPlayJobDef(jobName))
			{
				return IsToddlerSelfPlayToil(pawn);
			}

			if (jobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob)
			{
				return pawn.jobs?.curDriver?.CurToilString == "ToddlerMutualPlay";
			}

			if (jobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayPartnerJob)
			{
				return pawn.jobs?.curDriver?.CurToilString == "ToddlerMutualPlayPartner";
			}

			return false;
		}

		private static bool IsPawnInBedOrCrib(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			if (pawn.CurrentBed() != null)
			{
				return true;
			}

			Map map = pawn.Map;
			if (map == null || !pawn.Position.IsValid)
			{
				return false;
			}

			List<Thing> thingList = pawn.Position.GetThingList(map);
			for (int i = 0; i < thingList.Count; i++)
			{
				if (thingList[i] is Building_Bed)
				{
					return true;
				}
			}

			return false;
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

		private static bool TryApplyToddlerProneProfile(Pawn pawn, Rot4 rot, object pdd)
		{
			Rot4 proneFacing = ResolveToddlerProneFacing(pawn, rot);
			if (!TryPrepareCustomPose(pdd, proneFacing))
			{
				return false;
			}

			float angle = proneFacing == Rot4.East ? 40f : -40f;

			_angleOffsetField.SetValue(pdd, angle);
			_posOffsetField.SetValue(pdd, Vector3.zero);
			return true;
		}

		private static Rot4 ResolveToddlerProneFacing(Pawn pawn, Rot4 rot)
		{
			if (rot == Rot4.East || rot == Rot4.West)
			{
				return rot;
			}

			int seed = Gen.HashCombineInt(pawn?.thingIDNumber ?? 0, pawn?.CurJob?.loadID ?? 0);
			return (seed & 1) == 0 ? Rot4.East : Rot4.West;
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

		private static bool TryApplyBabyRollProfile(Pawn pawn, Rot4 rot, object pdd)
		{
			if (!TryPrepareCustomPose(pdd, rot) || pawn == null)
			{
				return false;
			}

			try
			{
				int stepTicks = IsBabyOnly(pawn) ? 12 : 10;
				int cycleTicks = stepTicks * 12;
				int tick = Find.TickManager?.TicksGame ?? 0;
				int seedOffset = Mathf.Abs(Gen.HashCombineInt(pawn.thingIDNumber ^ 9137, pawn.CurJob?.loadID ?? 0)) % cycleTicks;
				tick = (tick + seedOffset) % cycleTicks;

				float angle = 0f;
				Vector3 pos = Vector3.zero;
				float rollAngle = IsBabyOnly(pawn) ? 24f : 30f;
				Rot4 facing = rot;

				Vector3 posStepM3 = GetScaledBabyRollStepPos(-3, pawn);
				Vector3 posStepM2 = GetScaledBabyRollStepPos(-2, pawn);
				Vector3 posStepM1 = GetScaledBabyRollStepPos(-1, pawn);
				Vector3 posStep0 = GetScaledBabyRollStepPos(0, pawn);
				Vector3 posStep1 = GetScaledBabyRollStepPos(1, pawn);
				Vector3 posStep2 = GetScaledBabyRollStepPos(2, pawn);
				Vector3 posStep3 = GetScaledBabyRollStepPos(3, pawn);

				if (!TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStep0, posStep1, rot, true))
				{
					facing = rot.Rotated(RotationDirection.Clockwise);
					if (!TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStep1, posStep2, rot, true))
					{
						facing = facing.Rotated(RotationDirection.Clockwise);
						if (!TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStep2, posStep3, rot, true))
						{
							facing = facing.Rotated(RotationDirection.Clockwise);
							if (!TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStep3, posStep2, rot, true))
							{
								facing = facing.Rotated(RotationDirection.Counterclockwise);
								if (!TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStep2, posStep1, rot, true))
								{
									facing = facing.Rotated(RotationDirection.Counterclockwise);
									if (!TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStep1, posStep0, rot, true))
									{
										facing = facing.Rotated(RotationDirection.Counterclockwise);
										if (!TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStep0, posStepM1, rot, true))
										{
											facing = facing.Rotated(RotationDirection.Counterclockwise);
											if (!TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStepM1, posStepM2, rot, true))
											{
												facing = facing.Rotated(RotationDirection.Counterclockwise);
												if (!TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStepM2, posStepM3, rot, true))
												{
													facing = facing.Rotated(RotationDirection.Counterclockwise);
													if (!TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStepM3, posStepM2, rot, true))
													{
														facing = facing.Rotated(RotationDirection.Clockwise);
														if (!TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStepM2, posStepM1, rot, true))
														{
															facing = facing.Rotated(RotationDirection.Clockwise);
															TryApplyYayoSegment(ref tick, stepTicks, ref angle, rollAngle, rollAngle, -1f, ref pos, posStepM1, posStep0, rot, true);
														}
													}
												}
											}
										}
									}
								}
							}
						}
					}
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
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to apply baby roll profile: {ex.Message}");
				}

				return false;
			}
		}

		private static Vector3 GetScaledBabyRollStepPos(int step, Pawn pawn)
		{
			float scale = IsBabyOnly(pawn) ? 0.080f : 0.110f;
			return new Vector3((-0.5f + -0.25f * step) * scale, 0f, (0.25f * step) * scale);
		}

		private static bool TryApplyToddlerYayoSocialProfile(Pawn pawn, Rot4 rot, object pdd, string yayoProfile)
		{
			if (!TryPrepareCustomPose(pdd, rot))
			{
				return false;
			}

			try
			{
				float angle = 0f;
				Vector3 pos = Vector3.zero;
				int tick = 0;
				int total = 221;
				int idTick = (pawn?.thingIDNumber ?? 0) * 20;
				int doubledTick = ((Find.TickManager?.TicksGame ?? 0) + idTick) % (total * 2);
				tick = doubledTick % total;

				Rot4 rotated = rot.Rotated(RotationDirection.Clockwise);
				Rot4 facing = rot;

				if (!TryConsumeYayoTicks(ref tick, 20) &&
					!TryApplyYayoSegment(ref tick, 5, ref angle, 0f, 10f, -1f, ref pos, rotated) &&
					!TryApplyYayoHold(ref tick, 20, ref angle, 10f, -1f, ref pos, rotated) &&
					!TryApplyYayoSegment(ref tick, 5, ref angle, 10f, -10f, -1f, ref pos, rotated) &&
					!TryApplyYayoHold(ref tick, 20, ref angle, -10f, -1f, ref pos, rotated) &&
					!TryApplyYayoSegment(ref tick, 5, ref angle, -10f, 0f, -1f, ref pos, rotated))
				{
					facing = doubledTick >= total
						? rot.Rotated(RotationDirection.Clockwise)
						: rot.Rotated(RotationDirection.Counterclockwise);

					if (!TryApplyYayoHold(ref tick, 15, ref angle, 0f, -1f, ref pos, rot))
					{
						facing = rot;
						if (!TryApplyYayoHold(ref tick, 20, ref angle, 0f, -1f, ref pos, rot) &&
							!TryApplyYayoSegment(ref tick, 5, ref angle, 0f, 0f, -1f, ref pos, Vector3.zero, YayoZOffset005, rot) &&
							!TryApplyYayoSegment(ref tick, 6, ref angle, 0f, 0f, -1f, ref pos, YayoZOffset005, Vector3.zero, rot) &&
							!TryApplyYayoHold(ref tick, 35, ref angle, 0f, -1f, ref pos, rot) &&
							!TryApplyYayoSegment(ref tick, 10, ref angle, 0f, 10f, -1f, ref pos, rot) &&
							!TryApplyYayoSegment(ref tick, 10, ref angle, 10f, 0f, -1f, ref pos, rot) &&
							!TryApplyYayoSegment(ref tick, 10, ref angle, 0f, 10f, -1f, ref pos, rot) &&
							!TryApplyYayoSegment(ref tick, 10, ref angle, 10f, 0f, -1f, ref pos, rot))
						{
							TryApplyYayoHold(ref tick, 25, ref angle, 0f, -1f, ref pos, rot);
						}
					}
				}

				_fixedRotField?.SetValue(pdd, facing);
				ApplySmallPawnRotationLock(pawn, pdd, rot, yayoProfile);
				_angleOffsetField.SetValue(pdd, angle);
				_posOffsetField.SetValue(pdd, pos);
				return true;
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] Failed to apply toddler-local Yayo social profile: {ex.Message}");
				}

				return false;
			}
		}

		private static void GetToddlerRollPose(float phase, Pawn pawn, out float angle, out Vector3 pos)
		{
			float angleAmplitude = IsBabyOnly(pawn) ? 18f : 24f;
			float posAmplitude = IsBabyOnly(pawn) ? 0.035f : 0.05f;

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

		private static void ApplySmallPawnRotationLock(Pawn pawn, object pdd, Rot4 rot, string yayoProfile)
		{
			if (_fixedRotField == null || pdd == null || !ToddlersCompatUtility.IsToddlerOrBaby(pawn))
			{
				return;
			}

			_fixedRotField.SetValue(pdd, rot);
			if (!_loggedCustomYayoSocialRotationLock)
			{
				_loggedCustomYayoSocialRotationLock = true;
				Log.Message($"[RimTalk_ToddlersExpansion] Locked custom Yayo social rotation for small pawns (profile={yayoProfile ?? "unknown"}).");
			}
		}

		private static bool TryConsumeYayoTicks(ref int tick, int duration)
		{
			if (tick >= duration)
			{
				tick -= duration;
				return false;
			}

			return true;
		}

		private static bool TryApplyYayoHold(ref int tick, int duration, ref float angle, float targetAngle, float centerY, ref Vector3 pos, Rot4? rot = null)
		{
			return TryApplyYayoSegment(
				ref tick,
				duration,
				ref angle,
				targetAngle,
				targetAngle,
				centerY,
				ref pos,
				Vector3.zero,
				Vector3.zero,
				rot,
				false);
		}

		private static bool TryApplyYayoSegment(ref int tick, int duration, ref float angle, float startAngle, float targetAngle, float centerY, ref Vector3 pos, Rot4? rot = null)
		{
			return TryApplyYayoSegment(
				ref tick,
				duration,
				ref angle,
				startAngle,
				targetAngle,
				centerY,
				ref pos,
				Vector3.zero,
				Vector3.zero,
				rot,
				false);
		}

		private static bool TryApplyYayoSegment(
			ref int tick,
			int duration,
			ref float angle,
			float startAngle,
			float targetAngle,
			float centerY,
			ref Vector3 pos,
			Vector3 startPos,
			Vector3 targetPos,
			Rot4? rot = null,
			bool useLineTween = false)
		{
			if (tick >= duration)
			{
				tick -= duration;
				return false;
			}

			ApplyYayoRotationTransform(rot, ref startAngle, ref targetAngle, ref startPos, ref targetPos, centerY);

			float tickPercent = tick / (float)duration;
			if (!useLineTween)
			{
				tickPercent = Mathf.Sin(Mathf.PI * 0.5f * tickPercent);
			}

			angle += startAngle + (targetAngle - startAngle) * tickPercent;
			pos += startPos + (targetPos - startPos) * tickPercent;
			return true;
		}

		private static void ApplyYayoRotationTransform(Rot4? rot, ref float startAngle, ref float targetAngle, ref Vector3 startPos, ref Vector3 targetPos, float centerY)
		{
			if (rot == null)
			{
				return;
			}

			switch (rot.Value.AsByte)
			{
				case 3:
					startAngle = -startAngle;
					targetAngle = -targetAngle;
					startPos = new Vector3(-startPos.x, 0f, startPos.z);
					targetPos = new Vector3(-targetPos.x, 0f, targetPos.z);
					break;
				case 2:
					startAngle *= YayoAngleReduce;
					targetAngle *= YayoAngleReduce;
					startPos = new Vector3(0f, 0f, startPos.z - startPos.x - startAngle * YayoAngleToPos);
					targetPos = new Vector3(0f, 0f, targetPos.z - targetPos.x - targetAngle * YayoAngleToPos);
					break;
				case 0:
					startAngle *= -YayoAngleReduce;
					targetAngle *= -YayoAngleReduce;
					startPos = new Vector3(0f, 0f, startPos.z + startPos.x - startAngle * YayoAngleToPos);
					targetPos = new Vector3(0f, 0f, targetPos.z + targetPos.x - targetAngle * YayoAngleToPos);
					break;
			}

			if (centerY != 0f)
			{
				startPos += new Vector3(startAngle * -0.01f * centerY, 0f, 0f);
				targetPos += new Vector3(targetAngle * -0.01f * centerY, 0f, 0f);
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

		private static bool IsBabyOnly(Pawn pawn)
		{
			return BiotechCompatUtility.IsBaby(pawn) && !ToddlersCompatUtility.IsToddler(pawn);
		}

		private static void ScaleSmallPawnYayoOffsets(Pawn pawn, object pdd, string profile)
		{
			if (pawn == null || pdd == null || _angleOffsetField == null || _posOffsetField == null)
			{
				return;
			}

			float angleScale;
			float posScale;
			if (IsBabyOnly(pawn))
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
