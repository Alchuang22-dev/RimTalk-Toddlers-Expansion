using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ToddlersCompatUtility
	{
		private const string ToddlerUtilityTypeName = "Toddlers.ToddlerUtility";
		private const float DefaultMinToddlerAge = 1f;
		private const float DefaultEndToddlerAge = 3f;

		private static bool _initialized;
		private static bool _isActive;
		private static bool _warned;
		private static Func<Pawn, bool> _isToddler;
		private static Func<Pawn, float> _toddlerMinAge;
		private static Func<Pawn, float> _toddlerEndAge;
		private static bool _playTypesInitialized;
		private static Func<Pawn, bool> _toddlersIsPlaying;
		private static Type _toddlersWatchTelevisionDriverType;
		private static Type[] _toddlersExtraPlayDriverTypes;

		public static bool IsToddlersActive
		{
			get
			{
				EnsureInitialized();
				return _isActive;
			}
		}

		public static bool IsToddler(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			EnsureInitialized();
			if (!_isActive || _isToddler == null)
			{
				return false;
			}

			try
			{
				return _isToddler(pawn);
			}
			catch (Exception ex)
			{
				WarnOnce("IsToddler", ex);
				return false;
			}
		}

		public static float GetToddlerMinAgeYears(Pawn pawn)
		{
			if (pawn == null)
			{
				return DefaultMinToddlerAge;
			}

			EnsureInitialized();
			if (!_isActive || _toddlerMinAge == null)
			{
				return DefaultMinToddlerAge;
			}

			try
			{
				return _toddlerMinAge(pawn);
			}
			catch (Exception ex)
			{
				WarnOnce("ToddlerMinAge", ex);
				return DefaultMinToddlerAge;
			}
		}

		public static float GetToddlerEndAgeYears(Pawn pawn)
		{
			if (pawn == null)
			{
				return DefaultEndToddlerAge;
			}

			EnsureInitialized();
			if (!_isActive || _toddlerEndAge == null)
			{
				return DefaultEndToddlerAge;
			}

			try
			{
				return _toddlerEndAge(pawn);
			}
			catch (Exception ex)
			{
				WarnOnce("ToddlerEndAge", ex);
				return DefaultEndToddlerAge;
			}
		}

		public static bool IsEligibleForSelfPlay(Pawn pawn)
		{
			if (!IsToddler(pawn))
			{
				return false;
			}

			return pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYearsFloat >= GetToddlerMinAgeYears(pawn);
		}

		public static bool IsCurrentlyPlaying(Pawn pawn)
		{
			if (pawn?.CurJob?.def == null)
			{
				return false;
			}

			JobDef jobDef = pawn.CurJob.def;
			return jobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfPlayJob
				|| jobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob;
		}

		public static bool IsEngagedInToddlerPlay(Pawn pawn)
		{
			if (pawn?.jobs?.curDriver == null)
			{
				return false;
			}

			if (pawn.CurJobDef == JobDefOf.BabyPlay)
			{
				return true;
			}

			if (IsCurrentlyPlaying(pawn))
			{
				return true;
			}

			if (IsVanillaBabyPlay(pawn.jobs.curDriver))
			{
				return true;
			}

			return IsToddlersPlayJob(pawn);
		}

		public static bool IsToddlerOrBaby(Pawn pawn)
		{
			if (IsToddler(pawn))
			{
				return true;
			}

			return pawn?.DevelopmentalStage.Baby() ?? false;
		}

		public static float GetToddlersAgeYears(Pawn pawn)
		{
			return pawn?.ageTracker?.AgeBiologicalYearsFloat ?? 0f;
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
				Type utilityType = AccessTools.TypeByName(ToddlerUtilityTypeName);
				if (utilityType == null)
				{
					_isActive = false;
					return;
				}

				_isActive = true;
				MethodInfo isToddlerMethod = AccessTools.Method(utilityType, "IsToddler", new[] { typeof(Pawn) });
				if (isToddlerMethod != null)
				{
					_isToddler = (Func<Pawn, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, bool>), isToddlerMethod);
				}

				MethodInfo minAgeMethod = AccessTools.Method(utilityType, "ToddlerMinAge", new[] { typeof(Pawn) });
				if (minAgeMethod != null)
				{
					_toddlerMinAge = (Func<Pawn, float>)Delegate.CreateDelegate(typeof(Func<Pawn, float>), minAgeMethod);
				}

				MethodInfo endAgeMethod = AccessTools.Method(utilityType, "ToddlerEndAge", new[] { typeof(Pawn) });
				if (endAgeMethod != null)
				{
					_toddlerEndAge = (Func<Pawn, float>)Delegate.CreateDelegate(typeof(Func<Pawn, float>), endAgeMethod);
				}
			}
			catch (Exception ex)
			{
				_isActive = false;
				WarnOnce("Initialize", ex);
			}
		}

		private static bool IsVanillaBabyPlay(JobDriver driver)
		{
			return driver is JobDriver_BabyPlay;
		}

		private static bool IsToddlersPlayJob(Pawn pawn)
		{
			EnsureInitialized();
			if (!_isActive)
			{
				return false;
			}

			EnsurePlayTypesInitialized();
			JobDriver driver = pawn.jobs?.curDriver;
			if (driver == null)
			{
				return false;
			}

			if (_toddlersIsPlaying != null)
			{
				bool playing = false;
				try
				{
					playing = _toddlersIsPlaying(pawn);
				}
				catch (Exception ex)
				{
					WarnOnce("IsToddlerPlaying", ex);
				}

				if (playing)
				{
					if (_toddlersWatchTelevisionDriverType != null && _toddlersWatchTelevisionDriverType.IsInstanceOfType(driver))
					{
						return false;
					}

					return true;
				}
			}

			if (_toddlersExtraPlayDriverTypes == null || _toddlersExtraPlayDriverTypes.Length == 0)
			{
				return false;
			}

			Type driverType = driver.GetType();
			for (int i = 0; i < _toddlersExtraPlayDriverTypes.Length; i++)
			{
				Type type = _toddlersExtraPlayDriverTypes[i];
				if (type != null && type.IsAssignableFrom(driverType))
				{
					return true;
				}
			}

			return false;
		}

		private static void EnsurePlayTypesInitialized()
		{
			if (_playTypesInitialized)
			{
				return;
			}

			_playTypesInitialized = true;
			try
			{
				Type playUtilityType = AccessTools.TypeByName("Toddlers.ToddlerPlayUtility");
				if (playUtilityType != null)
				{
					MethodInfo isPlayingMethod = AccessTools.Method(playUtilityType, "IsToddlerPlaying", new[] { typeof(Pawn) });
					if (isPlayingMethod != null)
					{
						_toddlersIsPlaying = (Func<Pawn, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, bool>), isPlayingMethod);
					}
				}

				_toddlersWatchTelevisionDriverType = AccessTools.TypeByName("Toddlers.JobDriver_ToddlerWatchTelevision");
				_toddlersExtraPlayDriverTypes = new[]
				{
					AccessTools.TypeByName("Toddlers.JobDriver_ToddlerBugwatching"),
					AccessTools.TypeByName("Toddlers.JobDriver_ToddlerFiregazing"),
					AccessTools.TypeByName("Toddlers.JobDriver_ToddlerFloordrawing"),
					AccessTools.TypeByName("Toddlers.JobDriver_ToddlerPlayDecor"),
					AccessTools.TypeByName("Toddlers.JobDriver_ToddlerPlayToys"),
					AccessTools.TypeByName("Toddlers.JobDriver_ToddlerSkydreaming"),
					AccessTools.TypeByName("Toddlers.JobDriver_WiggleInCrib"),
					AccessTools.TypeByName("Toddlers.JobDriver_LayAngleInCrib"),
					AccessTools.TypeByName("Toddlers.JobDriver_RestIdleInCrib"),
					AccessTools.TypeByName("Toddlers.JobDriver_PlayCrib"),
					AccessTools.TypeByName("Toddlers.JobDriver_BePlayedWith")
				};
			}
			catch (Exception ex)
			{
				WarnOnce("InitializePlayTypes", ex);
			}
		}

		private static void WarnOnce(string context, Exception ex)
		{
			if (_warned || !Prefs.DevMode)
			{
				return;
			}

			_warned = true;
			Log.Warning($"[RimTalk_ToddlersExpansion] Toddlers compat {context} failed: {ex.Message}");
		}
	}
}
