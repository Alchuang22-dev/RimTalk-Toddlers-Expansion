using System.Collections.Generic;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public class ToddlerPlayRegistration
	{
		public string JobDefName { get; set; }
		public ToddlerPlayCategory Category { get; set; }
		public float BoredomWeight { get; set; } = 1.0f;
		public string ModId { get; set; }

		public ToddlerPlayRegistration()
		{
		}

		public ToddlerPlayRegistration(string jobDefName, ToddlerPlayCategory category, float boredomWeight = 1.0f, string modId = null)
		{
			JobDefName = jobDefName;
			Category = category;
			BoredomWeight = boredomWeight;
			ModId = modId;
		}
	}

	/// <summary>
	/// Central registry for toddler play jobs used by boredom and dialogue systems.
	/// </summary>
	public static class ToddlerPlayRegistry
	{
		private static readonly Dictionary<string, ToddlerPlayRegistration> _registrations = new Dictionary<string, ToddlerPlayRegistration>();
		private static readonly Dictionary<string, int> _customCategories = new Dictionary<string, int>();
		private static int _nextCustomCategoryId = (int)ToddlerPlayCategory.Custom;
		private static bool _initialized;

		public static void Initialize()
		{
			if (_initialized)
			{
				return;
			}

			_initialized = true;

			RegisterToddlersModActivities();
			RegisterRimTalkActivities();

			Log.Message("[RimTalk Toddlers Expansion] ToddlerPlayRegistry initialized with " + _registrations.Count + " activities.");
		}

		private static void RegisterToddlersModActivities()
		{
			Register("ToddlerFloordrawing", ToddlerPlayCategory.Creative, 1.0f, "Toddlers");
			Register("ToddlerSkydreaming", ToddlerPlayCategory.Observation, 1.0f, "Toddlers");
			Register("ToddlerBugwatching", ToddlerPlayCategory.Observation, 1.0f, "Toddlers");
			Register("ToddlerPlayToys", ToddlerPlayCategory.ToyPlay, 1.0f, "Toddlers");
			Register("ToddlerWatchTelevision", ToddlerPlayCategory.Media, 1.0f, "Toddlers");
			Register("ToddlerFiregazing", ToddlerPlayCategory.Observation, 1.0f, "Toddlers");
			Register("ToddlerPlayDecor", ToddlerPlayCategory.ToyPlay, 1.0f, "Toddlers");
		}

		private static void RegisterRimTalkActivities()
		{
			Register("RimTalk_ToddlerSelfPlayJob", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_Ratkin_PlaywithOwnEar", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_Ratkin_PlaywithOwnTail", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_Kiiro_PlaywithOwnTail", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_Kiiro_PlaywithBobbles", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_MoeLotl_LickOwnBody", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_MoeLotl_Wiggle", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_Milira_LearntoFly", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_Milira_PlaywithFeathers", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_Bunny_DigHole", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_Bunny_PickupEars", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_Cinder_PlaywithOwnTentacle", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerSelfPlay_Cinder_WatchCrystals", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerMutualPlayJob", ToddlerPlayCategory.SocialPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerMutualPlayPartnerJob", ToddlerPlayCategory.SocialPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerPlayAtToy", ToddlerPlayCategory.ToyPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerObserveAdultWork", ToddlerPlayCategory.Observation, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_WatchToddlerPlayJob", ToddlerPlayCategory.SocialPlay, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_MidnightSnack", ToddlerPlayCategory.None, 0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_BeingCarried_Observe", ToddlerPlayCategory.Exploration, 0.8f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_BeingCarried_Idle", ToddlerPlayCategory.Passive, 0.3f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_BeingCarried", ToddlerPlayCategory.Passive, 0.3f, "RimTalk_ToddlersExpansion");

			Register("RimTalk_ToddlerListenStory", ToddlerPlayCategory.Passive, 0.8f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerExploreWild", ToddlerPlayCategory.Exploration, 1.2f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerPlayWater", ToddlerPlayCategory.Exploration, 1.2f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerWatchAnimal", ToddlerPlayCategory.Observation, 1.0f, "RimTalk_ToddlersExpansion");
			Register("RimTalk_ToddlerGroupPlay", ToddlerPlayCategory.SocialPlay, 1.0f, "RimTalk_ToddlersExpansion");
		}

		public static void Register(string jobDefName, ToddlerPlayCategory category, float boredomWeight = 1.0f, string modId = null)
		{
			if (string.IsNullOrEmpty(jobDefName))
			{
				Log.Warning("[RimTalk Toddlers Expansion] Attempted to register activity with null or empty jobDefName");
				return;
			}

			var registration = new ToddlerPlayRegistration(jobDefName, category, boredomWeight, modId);

			if (_registrations.ContainsKey(jobDefName))
			{
				_registrations[jobDefName] = registration;
				if (Prefs.DevMode)
				{
					Log.Message($"[RimTalk Toddlers Expansion] Updated registration for {jobDefName} -> {category}");
				}
			}
			else
			{
				_registrations.Add(jobDefName, registration);
				if (Prefs.DevMode)
				{
					Log.Message($"[RimTalk Toddlers Expansion] Registered {jobDefName} -> {category}");
				}
			}
		}

		public static void RegisterBatch(IEnumerable<ToddlerPlayRegistration> registrations)
		{
			foreach (var reg in registrations)
			{
				Register(reg.JobDefName, reg.Category, reg.BoredomWeight, reg.ModId);
			}
		}

		public static int RegisterCustomCategory(string categoryName)
		{
			if (_customCategories.ContainsKey(categoryName))
			{
				return _customCategories[categoryName];
			}

			int categoryId = _nextCustomCategoryId++;
			_customCategories.Add(categoryName, categoryId);

			Log.Message($"[RimTalk Toddlers Expansion] Registered custom category '{categoryName}' with ID {categoryId}");

			return categoryId;
		}

		public static ToddlerPlayCategory GetCategory(JobDef jobDef)
		{
			if (jobDef == null)
			{
				return ToddlerPlayCategory.None;
			}

			return GetCategory(jobDef.defName);
		}

		public static ToddlerPlayCategory GetCategory(string jobDefName)
		{
			if (string.IsNullOrEmpty(jobDefName))
			{
				return ToddlerPlayCategory.None;
			}

			if (!_initialized)
			{
				Initialize();
			}

			if (_registrations.TryGetValue(jobDefName, out var registration))
			{
				return registration.Category;
			}

			if (ToddlersExpansionSettings.enableAutoDetection)
			{
				var jobDef = DefDatabase<JobDef>.GetNamedSilentFail(jobDefName);
				if (jobDef != null)
				{
					return AutoDetectCategory(jobDef);
				}
			}

			return ToddlerPlayCategory.None;
		}

		public static float GetBoredomWeight(JobDef jobDef)
		{
			if (jobDef == null)
			{
				return 0f;
			}

			return GetBoredomWeight(jobDef.defName);
		}

		public static float GetBoredomWeight(string jobDefName)
		{
			if (string.IsNullOrEmpty(jobDefName))
			{
				return 0f;
			}

			if (!_initialized)
			{
				Initialize();
			}

			if (_registrations.TryGetValue(jobDefName, out var registration))
			{
				return registration.BoredomWeight;
			}

			return 1.0f;
		}

		public static bool IsRegistered(string jobDefName)
		{
			if (!_initialized)
			{
				Initialize();
			}

			return _registrations.ContainsKey(jobDefName);
		}

		public static IEnumerable<ToddlerPlayRegistration> GetAllRegistrations()
		{
			if (!_initialized)
			{
				Initialize();
			}

			return _registrations.Values;
		}

		public static ToddlerPlayCategory AutoDetectCategory(JobDef jobDef)
		{
			if (jobDef == null)
			{
				return ToddlerPlayCategory.None;
			}

			if (jobDef.joyKind != null)
			{
				switch (jobDef.joyKind.defName)
				{
					case "Meditative":
						return ToddlerPlayCategory.SoloPlay;
					case "Social":
						return ToddlerPlayCategory.SocialPlay;
					case "Gluttonous":
						return ToddlerPlayCategory.Media;
				}
			}

			string name = jobDef.defName.ToLower();

			if (name.Contains("watch") || name.Contains("observe") || name.Contains("gaze"))
			{
				return ToddlerPlayCategory.Observation;
			}

			if (name.Contains("toy") || name.Contains("decor"))
			{
				return ToddlerPlayCategory.ToyPlay;
			}

			if (name.Contains("mutual") || name.Contains("social") || name.Contains("group"))
			{
				return ToddlerPlayCategory.SocialPlay;
			}

			if (name.Contains("explore") || name.Contains("water") || name.Contains("wild"))
			{
				return ToddlerPlayCategory.Exploration;
			}

			if (name.Contains("draw") || name.Contains("build") || name.Contains("create") || name.Contains("floor"))
			{
				return ToddlerPlayCategory.Creative;
			}

			if (name.Contains("listen") || name.Contains("story"))
			{
				return ToddlerPlayCategory.Passive;
			}

			if (name.Contains("television") || name.Contains("tv") || name.Contains("screen"))
			{
				return ToddlerPlayCategory.Media;
			}

			if (name.Contains("self") || name.Contains("solo") || name.Contains("alone"))
			{
				return ToddlerPlayCategory.SoloPlay;
			}

			return ToddlerPlayCategory.None;
		}

		public static void Reset()
		{
			_registrations.Clear();
			_customCategories.Clear();
			_nextCustomCategoryId = (int)ToddlerPlayCategory.Custom;
			_initialized = false;
		}
	}
}
