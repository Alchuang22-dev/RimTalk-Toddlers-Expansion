using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ApparelGraphicRecordGetter_BabyFallback
	{
		private static readonly HashSet<string> _loggedKeys = new HashSet<string>();

		public static void Init(HarmonyLib.Harmony harmony)
		{
			var target = AccessTools.Method(typeof(ApparelGraphicRecordGetter), nameof(ApparelGraphicRecordGetter.TryGetGraphicApparel));
			if (target != null)
			{
				harmony.Patch(target, prefix: new HarmonyMethod(typeof(Patch_ApparelGraphicRecordGetter_BabyFallback), nameof(TryGetGraphicApparel_Prefix)));
			}
		}

		private static bool TryGetGraphicApparel_Prefix(Apparel apparel, BodyTypeDef bodyType, bool forStatue, ref ApparelGraphicRecord rec, ref bool __result)
		{
			if (bodyType == null)
			{
				Log.Error("Getting apparel graphic with undefined body type.");
				bodyType = BodyTypeDefOf.Male;
			}

			if (apparel.WornGraphicPath.NullOrEmpty())
			{
				rec = new ApparelGraphicRecord(null, null);
				__result = false;
				return false;
			}

			bool usesBodyType = apparel.def.apparel.LastLayer != ApparelLayerDefOf.Overhead
				&& apparel.def.apparel.LastLayer != ApparelLayerDefOf.EyeCover
				&& !apparel.RenderAsPack()
				&& apparel.WornGraphicPath != BaseContent.PlaceholderImagePath
				&& apparel.WornGraphicPath != BaseContent.PlaceholderGearImagePath;

			string path = usesBodyType ? $"{apparel.WornGraphicPath}_{bodyType.defName}" : apparel.WornGraphicPath;

			if (usesBodyType && bodyType == BodyTypeDefOf.Baby)
			{
				if (!HasDirectionalTexture(path))
				{
					string childPath = $"{apparel.WornGraphicPath}_{BodyTypeDefOf.Child.defName}";
					if (HasDirectionalTexture(childPath))
					{
						LogFallbackOnce(apparel, path, childPath);
						path = childPath;
					}
					else
					{
						LogMissingOnce(apparel, path);
					}
				}
			}

			Shader shader = ShaderDatabase.Cutout;
			if (!forStatue)
			{
				if (apparel.StyleDef?.graphicData.shaderType != null)
				{
					shader = apparel.StyleDef.graphicData.shaderType.Shader;
				}
				else if ((apparel.StyleDef == null && apparel.def.apparel.useWornGraphicMask)
					|| (apparel.StyleDef != null && apparel.StyleDef.UseWornGraphicMask))
				{
					shader = ShaderDatabase.CutoutComplex;
				}
			}

			Graphic graphic = GraphicDatabase.Get<Graphic_Multi>(path, shader, apparel.def.graphicData.drawSize, apparel.DrawColor);
			rec = new ApparelGraphicRecord(graphic, apparel);
			__result = true;
			return false;
		}

		private static bool HasDirectionalTexture(string path)
		{
			return ContentFinder<Texture2D>.Get(path + "_south", false) != null
				|| ContentFinder<Texture2D>.Get(path + "_north", false) != null
				|| ContentFinder<Texture2D>.Get(path + "_east", false) != null
				|| ContentFinder<Texture2D>.Get(path + "_west", false) != null;
		}

		private static void LogFallbackOnce(Apparel apparel, string babyPath, string childPath)
		{
			if (!Prefs.DevMode || apparel?.def == null)
			{
				return;
			}

			string key = $"fallback:{apparel.def.defName}:{babyPath}->{childPath}";
			if (_loggedKeys.Add(key))
			{
				Log.Message($"[RimTalk_ToddlersExpansion][PKCCompat] Baby apparel fallback: def={apparel.def.defName} babyPath={babyPath} childPath={childPath}");
			}
		}

		private static void LogMissingOnce(Apparel apparel, string babyPath)
		{
			if (!Prefs.DevMode || apparel?.def == null)
			{
				return;
			}

			string key = $"missing:{apparel.def.defName}:{babyPath}";
			if (_loggedKeys.Add(key))
			{
				Log.Warning($"[RimTalk_ToddlersExpansion][PKCCompat] Missing baby/child apparel textures: def={apparel.def.defName} babyPath={babyPath}");
			}
		}
	}
}
