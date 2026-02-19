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
				Log.Error($"{LogPrefix} Failed to apply patch: {ex}");
			}
		}

		private static void Apply()
		{
			if (!HasFallbackTextures())
			{
				Log.Error($"{LogPrefix} Missing fallback textures at path '{FallbackBodyPath}'. Expected at least _south/_north/_east.");
				return;
			}

			Type harThingDefType = GenTypes.GetTypeInAnyAssembly("AlienRace.ThingDef_AlienRace");
			if (harThingDefType == null)
			{
				Log.Message($"{LogPrefix} HAR not detected. Skipping.");
				return;
			}

			int raceCount = 0;
			int changedRaceCount = 0;
			int agePatched = 0;
			int ageAdded = 0;
			int bodyTypePatched = 0;
			int bodyTypeAdded = 0;

			foreach (ThingDef raceDef in DefDatabase<ThingDef>.AllDefsListForReading)
			{
				if (raceDef == null || !harThingDefType.IsAssignableFrom(raceDef.GetType()))
				{
					continue;
				}

				raceCount++;
				if (!TryPatchRace(raceDef, out int raceAgePatched, out int raceAgeAdded, out int raceBodyTypePatched, out int raceBodyTypeAdded))
				{
					continue;
				}

				changedRaceCount++;
				agePatched += raceAgePatched;
				ageAdded += raceAgeAdded;
				bodyTypePatched += raceBodyTypePatched;
				bodyTypeAdded += raceBodyTypeAdded;
			}

			Log.Message(
				$"{LogPrefix} Done. races={raceCount}, changed={changedRaceCount}, " +
				$"agePatched={agePatched}, ageAdded={ageAdded}, " +
				$"bodyTypePatched={bodyTypePatched}, bodyTypeAdded={bodyTypeAdded}, path={FallbackBodyPath}");
		}

		private static bool TryPatchRace(
			ThingDef raceDef,
			out int agePatched,
			out int ageAdded,
			out int bodyTypePatched,
			out int bodyTypeAdded)
		{
			agePatched = 0;
			ageAdded = 0;
			bodyTypePatched = 0;
			bodyTypeAdded = 0;

			object alienRace = GetFieldValue(raceDef, "alienRace");
			object graphicPaths = GetFieldValue(alienRace, "graphicPaths");
			object bodyGraphic = GetFieldValue(graphicPaths, "body");
			if (bodyGraphic == null)
			{
				return false;
			}

			bool changed = false;
			if (PatchAgeGraphics(raceDef, bodyGraphic, ref agePatched, ref ageAdded))
			{
				changed = true;
			}

			if (PatchBodyTypeGraphics(bodyGraphic, ref bodyTypePatched, ref bodyTypeAdded))
			{
				changed = true;
			}

			return changed;
		}

		private static bool PatchAgeGraphics(ThingDef raceDef, object bodyGraphic, ref int patched, ref int added)
		{
			IList ageGraphics = GetListField(bodyGraphic, "ageGraphics", out Type ageGraphicType);
			if (ageGraphics == null)
			{
				return false;
			}

			bool changed = false;
			bool hasBaby = false;
			HashSet<LifeStageDef> toddlerStagesSeen = new HashSet<LifeStageDef>();

			foreach (object entry in ageGraphics)
			{
				LifeStageDef stage = GetFieldValue(entry, "age") as LifeStageDef;
				if (stage == null)
				{
					continue;
				}

				bool isBaby = IsBabyLifeStage(stage);
				bool isToddler = IsToddlerLifeStage(stage);
				if (!isBaby && !isToddler)
				{
					continue;
				}

				if (TrySetFieldValue(entry, FallbackBodyPath, "path"))
				{
					changed = true;
					patched++;
				}

				if (isBaby)
				{
					hasBaby = true;
				}

				if (isToddler)
				{
					toddlerStagesSeen.Add(stage);
				}
			}

			if (!hasBaby && LifeStageDefOf.HumanlikeBaby != null && TryAddAgeGraphic(ageGraphics, ageGraphicType, LifeStageDefOf.HumanlikeBaby))
			{
				changed = true;
				added++;
			}

			List<LifeStageDef> toddlerStages = GetToddlerLifeStages(raceDef);
			foreach (LifeStageDef stage in toddlerStages)
			{
				if (toddlerStagesSeen.Contains(stage))
				{
					continue;
				}

				if (!TryAddAgeGraphic(ageGraphics, ageGraphicType, stage))
				{
					continue;
				}

				changed = true;
				added++;
			}

			return changed;
		}

		private static bool PatchBodyTypeGraphics(object bodyGraphic, ref int patched, ref int added)
		{
			IList bodyTypeGraphics = GetListField(bodyGraphic, "bodytypeGraphics", out Type bodyTypeGraphicType);
			if (bodyTypeGraphics == null)
			{
				return false;
			}

			bool changed = false;
			bool hasBaby = false;
			bool hasChild = false;

			foreach (object entry in bodyTypeGraphics)
			{
				BodyTypeDef bodyType = GetFieldValue(entry, "bodytype", "bodyType") as BodyTypeDef;
				if (bodyType == null)
				{
					continue;
				}

				if (IsBabyBodyType(bodyType))
				{
					hasBaby = true;
					if (TrySetFieldValue(entry, FallbackBodyPath, "path"))
					{
						changed = true;
						patched++;
					}
					continue;
				}

				if (IsChildBodyType(bodyType))
				{
					hasChild = true;
					if (TrySetFieldValue(entry, FallbackBodyPath, "path"))
					{
						changed = true;
						patched++;
					}
				}
			}

			if (!hasBaby && BodyTypeDefOf.Baby != null && TryAddBodyTypeGraphic(bodyTypeGraphics, bodyTypeGraphicType, BodyTypeDefOf.Baby))
			{
				changed = true;
				added++;
			}

			if (!hasChild && BodyTypeDefOf.Child != null && TryAddBodyTypeGraphic(bodyTypeGraphics, bodyTypeGraphicType, BodyTypeDefOf.Child))
			{
				changed = true;
				added++;
			}

			return changed;
		}

		private static bool TryAddAgeGraphic(IList list, Type entryType, LifeStageDef stage)
		{
			if (list == null || entryType == null || stage == null)
			{
				return false;
			}

			try
			{
				object entry = Activator.CreateInstance(entryType);
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
				list.Add(entry);
				return true;
			}
			catch (Exception ex)
			{
				Log.Warning($"{LogPrefix} Failed to add age graphic entry for {stage.defName}: {ex.Message}");
				return false;
			}
		}

		private static bool TryAddBodyTypeGraphic(IList list, Type entryType, BodyTypeDef bodyType)
		{
			if (list == null || entryType == null || bodyType == null)
			{
				return false;
			}

			try
			{
				object entry = Activator.CreateInstance(entryType);
				if (entry == null)
				{
					return false;
				}

				if (!SetFieldValue(entry, bodyType, "bodytype", "bodyType"))
				{
					return false;
				}

				SetFieldValue(entry, FallbackBodyPath, "path");
				SetFieldValue(entry, 1, "variantCount");
				list.Add(entry);
				return true;
			}
			catch (Exception ex)
			{
				Log.Warning($"{LogPrefix} Failed to add body type graphic entry for {bodyType.defName}: {ex.Message}");
				return false;
			}
		}

		private static List<LifeStageDef> GetToddlerLifeStages(ThingDef raceDef)
		{
			List<LifeStageDef> result = new List<LifeStageDef>();
			List<LifeStageAge> lifeStages = raceDef?.race?.lifeStageAges;
			if (lifeStages == null)
			{
				return result;
			}

			foreach (LifeStageAge stageAge in lifeStages)
			{
				LifeStageDef stage = stageAge?.def;
				if (stage == null || !IsToddlerLifeStage(stage))
				{
					continue;
				}

				if (!result.Contains(stage))
				{
					result.Add(stage);
				}
			}

			return result;
		}

		private static bool IsBabyLifeStage(LifeStageDef stage)
		{
			if (stage == null)
			{
				return false;
			}

			if (stage == LifeStageDefOf.HumanlikeBaby)
			{
				return true;
			}

			return ContainsIgnoreCase(stage.defName, "baby");
		}

		private static bool IsToddlerLifeStage(LifeStageDef stage)
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

		private static bool IsBabyBodyType(BodyTypeDef bodyType)
		{
			if (bodyType == null)
			{
				return false;
			}

			if (bodyType == BodyTypeDefOf.Baby)
			{
				return true;
			}

			return ContainsIgnoreCase(bodyType.defName, "baby");
		}

		private static bool IsChildBodyType(BodyTypeDef bodyType)
		{
			if (bodyType == null)
			{
				return false;
			}

			if (bodyType == BodyTypeDefOf.Child)
			{
				return true;
			}

			return ContainsIgnoreCase(bodyType.defName, "child");
		}

		private static bool HasFallbackTextures()
		{
			return ContentFinder<Texture2D>.Get($"{FallbackBodyPath}_south", false) != null
				&& ContentFinder<Texture2D>.Get($"{FallbackBodyPath}_north", false) != null
				&& ContentFinder<Texture2D>.Get($"{FallbackBodyPath}_east", false) != null;
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

		private static IList GetListField(object obj, string fieldName, out Type elementType)
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

			object raw = field.GetValue(obj);
			if (!(raw is IList list))
			{
				return null;
			}

			elementType = ResolveListElementType(raw.GetType());
			return list;
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

			const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
			for (Type current = type; current != null; current = current.BaseType)
			{
				foreach (string fieldName in fieldNames)
				{
					FieldInfo field = current.GetField(fieldName, Flags);
					if (field != null)
					{
						return field;
					}
				}
			}

			return null;
		}

		private static bool ContainsIgnoreCase(string text, string needle)
		{
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(needle))
			{
				return false;
			}

			return text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
