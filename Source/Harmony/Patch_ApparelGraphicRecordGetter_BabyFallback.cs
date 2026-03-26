using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ApparelGraphicRecordGetter_BabyFallback
	{
		private const string NoFallbackPath = "__NO_FALLBACK__";

		private static readonly HashSet<string> _loggedKeys = new HashSet<string>();
		private static readonly Dictionary<string, bool> _directionalTextureCache = new Dictionary<string, bool>();
		private static readonly Dictionary<string, string> _fallbackPathCache = new Dictionary<string, string>();

		public static void Init(HarmonyLib.Harmony harmony)
		{
			var target = AccessTools.Method(typeof(ApparelGraphicRecordGetter), nameof(ApparelGraphicRecordGetter.TryGetGraphicApparel));
			if (target != null)
			{
				harmony.Patch(target, postfix: new HarmonyMethod(typeof(Patch_ApparelGraphicRecordGetter_BabyFallback), nameof(TryGetGraphicApparel_Postfix)));
			}
		}

		private static void TryGetGraphicApparel_Postfix(Apparel apparel, BodyTypeDef bodyType, bool forStatue, ref ApparelGraphicRecord rec, ref bool __result)
		{
			if (apparel == null || bodyType != BodyTypeDefOf.Baby || apparel.WornGraphicPath.NullOrEmpty())
			{
				return;
			}

			bool usesBodyType = apparel.def.apparel.LastLayer != ApparelLayerDefOf.Overhead
				&& apparel.def.apparel.LastLayer != ApparelLayerDefOf.EyeCover
				&& !apparel.RenderAsPack()
				&& apparel.WornGraphicPath != BaseContent.PlaceholderImagePath
				&& apparel.WornGraphicPath != BaseContent.PlaceholderGearImagePath;

			if (!usesBodyType)
			{
				return;
			}

			if (__result && rec.graphic != null && HasDirectionalTexture(rec.graphic.path))
			{
				return;
			}

			string babyPath = $"{apparel.WornGraphicPath}_{BodyTypeDefOf.Baby.defName}";
			if (HasDirectionalTexture(babyPath))
			{
				return;
			}

			string fallbackPath = ResolveFallbackPath(apparel.WornGraphicPath);
			if (fallbackPath == null)
			{
				LogMissingOnce(apparel, babyPath);
				return;
			}

			LogFallbackOnce(apparel, babyPath, fallbackPath);
			Shader shader = ResolveShader(apparel, forStatue);
			Graphic graphic = GraphicDatabase.Get<Graphic_Multi>(fallbackPath, shader, apparel.def.graphicData.drawSize, apparel.DrawColor);
			rec = new ApparelGraphicRecord(graphic, apparel);
			__result = true;
		}

		private static Shader ResolveShader(Apparel apparel, bool forStatue)
		{
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

			return shader;
		}

		private static bool HasDirectionalTexture(string path)
		{
			if (path.NullOrEmpty())
			{
				return false;
			}

			if (_directionalTextureCache.TryGetValue(path, out bool cached))
			{
				return cached;
			}

			bool found = ContentFinder<Texture2D>.Get(path + "_south", false) != null
				|| ContentFinder<Texture2D>.Get(path + "_north", false) != null
				|| ContentFinder<Texture2D>.Get(path + "_east", false) != null
				|| ContentFinder<Texture2D>.Get(path + "_west", false) != null;
			_directionalTextureCache[path] = found;
			return found;
		}

		private static string ResolveFallbackPath(string wornGraphicPath)
		{
			if (wornGraphicPath.NullOrEmpty())
			{
				return null;
			}

			if (_fallbackPathCache.TryGetValue(wornGraphicPath, out string cachedPath))
			{
				return cachedPath == NoFallbackPath ? null : cachedPath;
			}

			string resolvedPath = null;
			string childPath = $"{wornGraphicPath}_{BodyTypeDefOf.Child.defName}";
			if (HasDirectionalTexture(childPath))
			{
				resolvedPath = childPath;
			}
			else if (HasDirectionalTexture(wornGraphicPath))
			{
				resolvedPath = wornGraphicPath;
			}
			else
			{
				string malePath = $"{wornGraphicPath}_{BodyTypeDefOf.Male.defName}";
				if (HasDirectionalTexture(malePath))
				{
					resolvedPath = malePath;
				}
				else
				{
					string femalePath = $"{wornGraphicPath}_{BodyTypeDefOf.Female.defName}";
					if (HasDirectionalTexture(femalePath))
					{
						resolvedPath = femalePath;
					}
				}
			}

			_fallbackPathCache[wornGraphicPath] = resolvedPath ?? NoFallbackPath;
			return resolvedPath;
		}

		private static void LogFallbackOnce(Apparel apparel, string fromPath, string toPath)
		{
			if (!Prefs.DevMode || apparel?.def == null)
			{
				return;
			}

			string key = $"fallback:{apparel.def.defName}:{fromPath}->{toPath}";
			if (_loggedKeys.Add(key))
			{
				Log.Message($"[RimTalk_ToddlersExpansion][PKCCompat] Baby apparel fallback: def={apparel.def.defName} from={fromPath} to={toPath}");
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
				Log.Warning($"[RimTalk_ToddlersExpansion][PKCCompat] Missing baby fallback textures: def={apparel.def.defName} path={babyPath}");
			}
		}
	}
}
