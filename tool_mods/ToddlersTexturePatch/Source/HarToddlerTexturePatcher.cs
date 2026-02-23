using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace ToddlersTexturePatch
{
	[StaticConstructorOnStartup]
	public static class HarToddlerTexturePatcher
	{
		private const string LogPrefix = "[ToddlersTexturePatch]";
		private const string FallbackBodyPath = "Naked_Baby";
		private static readonly BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

		static HarToddlerTexturePatcher()
		{
			LongEventHandler.ExecuteWhenFinished(ApplySafely);
		}

		private static void ApplySafely()
		{
			try
			{
				Apply();
			}
			catch (Exception ex)
			{
				Log.Error($"{LogPrefix} Failed: {ex}");
			}
		}

		private static void Apply()
		{
			if (!HasDirectionalTextures(FallbackBodyPath))
			{
				Log.Error($"{LogPrefix} Missing fallback textures for '{FallbackBodyPath}' (_south/_north/_east).");
				return;
			}

			Type harThingDefType = GenTypes.GetTypeInAnyAssembly("AlienRace.ThingDef_AlienRace");
			if (harThingDefType == null)
			{
				Log.Message($"{LogPrefix} HAR not detected. Skip.");
				return;
			}

			int raceCount = 0;
			int changedRaceCount = 0;
			int stagePatched = 0;
			int stageAdded = 0;

			foreach (ThingDef raceDef in DefDatabase<ThingDef>.AllDefsListForReading)
			{
				if (raceDef == null || !harThingDefType.IsAssignableFrom(raceDef.GetType()))
				{
					continue;
				}

				raceCount++;
				if (TryPatchRace(raceDef, out int patched, out int added))
				{
					changedRaceCount++;
					stagePatched += patched;
					stageAdded += added;
				}
			}

			Log.Message($"{LogPrefix} Done. races={raceCount}, changed={changedRaceCount}, stagePatched={stagePatched}, stageAdded={stageAdded}, path={FallbackBodyPath}");
		}

		private static bool TryPatchRace(ThingDef raceDef, out int patched, out int added)
		{
			patched = 0;
			added = 0;

			object alienRace = GetFieldValue(raceDef, "alienRace");
			object graphicPaths = GetFieldValue(alienRace, "graphicPaths");
			object bodyGraphic = GetFieldValue(graphicPaths, "body");
			if (bodyGraphic == null)
			{
				return false;
			}

			IList ageGraphics = GetOrCreateListField(bodyGraphic, "ageGraphics", out Type ageGraphicType);
			if (ageGraphics == null || ageGraphicType == null)
			{
				return false;
			}

			List<LifeStageDef> targetStages = GetTargetStages(raceDef);
			if (targetStages.Count == 0)
			{
				return false;
			}

			bool changed = false;
			foreach (LifeStageDef stage in targetStages)
			{
				object entry = FindAgeGraphicEntry(ageGraphics, stage);
				if (entry == null)
				{
					if (TryAddAgeGraphicEntry(ageGraphics, ageGraphicType, stage))
					{
						added++;
						changed = true;
					}
					continue;
				}

				if (TrySetFieldValue(entry, FallbackBodyPath, "path"))
				{
					patched++;
					changed = true;
				}
			}

			return changed;
		}

		private static List<LifeStageDef> GetTargetStages(ThingDef raceDef)
		{
			HashSet<LifeStageDef> result = new HashSet<LifeStageDef>();

			List<LifeStageAge> lifeStages = raceDef?.race?.lifeStageAges;
			if (lifeStages != null)
			{
				foreach (LifeStageAge lifeStageAge in lifeStages)
				{
					LifeStageDef stage = lifeStageAge?.def;
					if (stage == null)
					{
						continue;
					}

					if (IsNewbornStage(stage) || IsToddlerStage(stage))
					{
						result.Add(stage);
					}
				}
			}

			if (LifeStageDefOf.HumanlikeBaby != null)
			{
				result.Add(LifeStageDefOf.HumanlikeBaby);
			}

			LifeStageDef humanlikeToddler = DefDatabase<LifeStageDef>.GetNamedSilentFail("HumanlikeToddler");
			if (humanlikeToddler != null)
			{
				result.Add(humanlikeToddler);
			}

			return result.ToList();
		}

		private static bool IsNewbornStage(LifeStageDef stage)
		{
			if (stage == null)
			{
				return false;
			}

			if (stage == LifeStageDefOf.HumanlikeBaby)
			{
				return true;
			}

			if (stage.developmentalStage == DevelopmentalStage.Baby)
			{
				return true;
			}

			return ContainsIgnoreCase(stage.defName, "baby") || ContainsIgnoreCase(stage.defName, "newborn");
		}

		private static bool IsToddlerStage(LifeStageDef stage)
		{
			if (stage == null)
			{
				return false;
			}

			if (ContainsIgnoreCase(stage.defName, "toddler"))
			{
				return true;
			}

			return ContainsIgnoreCase(stage.workerClass?.Name, "toddler");
		}

		private static object FindAgeGraphicEntry(IList ageGraphics, LifeStageDef stage)
		{
			if (ageGraphics == null || stage == null)
			{
				return null;
			}

			foreach (object item in ageGraphics)
			{
				LifeStageDef age = GetFieldValue(item, "age") as LifeStageDef;
				if (age == stage)
				{
					return item;
				}
			}

			return null;
		}

		private static bool TryAddAgeGraphicEntry(IList ageGraphics, Type ageGraphicType, LifeStageDef stage)
		{
			try
			{
				object entry = Activator.CreateInstance(ageGraphicType);
				if (entry == null)
				{
					return false;
				}

				if (!SetFieldValue(entry, stage, "age"))
				{
					return false;
				}

				SetFieldValue(entry, FallbackBodyPath, "path");
				SetFieldValue(entry, 1, "variantCount");
				ageGraphics.Add(entry);
				return true;
			}
			catch (Exception ex)
			{
				Log.Warning($"{LogPrefix} Add ageGraphics failed for {stage?.defName}: {ex.Message}");
				return false;
			}
		}

		private static bool HasDirectionalTextures(string path)
		{
			if (path.NullOrEmpty())
			{
				return false;
			}

			return ContentFinder<Texture2D>.Get(path + "_south", false) != null
				&& ContentFinder<Texture2D>.Get(path + "_north", false) != null
				&& ContentFinder<Texture2D>.Get(path + "_east", false) != null;
		}

		private static IList GetOrCreateListField(object obj, string fieldName, out Type elementType)
		{
			elementType = null;
			if (obj == null)
			{
				return null;
			}

			FieldInfo field = FindField(obj.GetType(), fieldName);
			if (field == null)
			{
				return null;
			}

			elementType = ResolveListElementType(field.FieldType);
			object raw = field.GetValue(obj);
			if (raw == null)
			{
				IList created = CreateListInstance(field.FieldType, elementType);
				if (created == null)
				{
					return null;
				}

				field.SetValue(obj, created);
				return created;
			}

			return raw as IList;
		}

		private static IList CreateListInstance(Type listType, Type elementType)
		{
			if (listType == null)
			{
				return null;
			}

			try
			{
				object direct = Activator.CreateInstance(listType);
				if (direct is IList directList)
				{
					return directList;
				}
			}
			catch
			{
			}

			if (elementType == null)
			{
				elementType = typeof(object);
			}

			Type fallbackType = typeof(List<>).MakeGenericType(elementType);
			return Activator.CreateInstance(fallbackType) as IList;
		}

		private static Type ResolveListElementType(Type listType)
		{
			if (listType == null)
			{
				return null;
			}

			if (listType.IsArray)
			{
				return listType.GetElementType();
			}

			if (listType.IsGenericType)
			{
				Type[] args = listType.GetGenericArguments();
				if (args.Length == 1)
				{
					return args[0];
				}
			}

			foreach (Type iface in listType.GetInterfaces())
			{
				if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IList<>))
				{
					continue;
				}

				Type[] args = iface.GetGenericArguments();
				if (args.Length == 1)
				{
					return args[0];
				}
			}

			return null;
		}

		private static object GetFieldValue(object obj, params string[] fieldNames)
		{
			if (obj == null)
			{
				return null;
			}

			FieldInfo field = FindField(obj.GetType(), fieldNames);
			return field?.GetValue(obj);
		}

		private static bool TrySetFieldValue(object obj, object value, params string[] fieldNames)
		{
			if (obj == null)
			{
				return false;
			}

			FieldInfo field = FindField(obj.GetType(), fieldNames);
			if (field == null || field.IsInitOnly || field.IsLiteral)
			{
				return false;
			}

			object current = field.GetValue(obj);
			if (Equals(current, value))
			{
				return false;
			}

			field.SetValue(obj, value);
			return true;
		}

		private static bool SetFieldValue(object obj, object value, params string[] fieldNames)
		{
			if (obj == null)
			{
				return false;
			}

			FieldInfo field = FindField(obj.GetType(), fieldNames);
			if (field == null || field.IsInitOnly || field.IsLiteral)
			{
				return false;
			}

			field.SetValue(obj, value);
			return true;
		}

		private static FieldInfo FindField(Type type, params string[] fieldNames)
		{
			if (type == null || fieldNames == null || fieldNames.Length == 0)
			{
				return null;
			}

			for (Type current = type; current != null; current = current.BaseType)
			{
				foreach (string fieldName in fieldNames)
				{
					FieldInfo field = current.GetField(fieldName, FieldFlags);
					if (field != null)
					{
						return field;
					}
				}
			}

			return null;
		}

		private static bool ContainsIgnoreCase(string text, string token)
		{
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
			{
				return false;
			}

			return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
