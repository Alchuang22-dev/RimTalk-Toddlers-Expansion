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

			string fallbackPath = null;
			string childPath = $"{apparel.WornGraphicPath}_{BodyTypeDefOf.Child.defName}";
			if (HasDirectionalTexture(childPath))
			{
				fallbackPath = childPath;
			}
			else if (HasDirectionalTexture(apparel.WornGraphicPath))
			{
				fallbackPath = apparel.WornGraphicPath;
			}
			else
			{
				string malePath = $"{apparel.WornGraphicPath}_{BodyTypeDefOf.Male.defName}";
				if (HasDirectionalTexture(malePath))
				{
					fallbackPath = malePath;
				}
				else
				{
					string femalePath = $"{apparel.WornGraphicPath}_{BodyTypeDefOf.Female.defName}";
					if (HasDirectionalTexture(femalePath))
					{
						fallbackPath = femalePath;
					}
				}
			}

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

			return ContentFinder<Texture2D>.Get(path + "_south", false) != null
				|| ContentFinder<Texture2D>.Get(path + "_north", false) != null
				|| ContentFinder<Texture2D>.Get(path + "_east", false) != null
				|| ContentFinder<Texture2D>.Get(path + "_west", false) != null;
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
