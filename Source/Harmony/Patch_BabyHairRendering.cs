using HarmonyLib;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	/// <summary>
	/// Harmony补丁：允许婴儿渲染头发
	/// patch PawnRenderNode_Hair.GraphicFor()中检查DevelopmentalStage，
	/// </summary>
	public static class Patch_BabyHairRendering
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			// 补丁 PawnRenderNode_Hair.GraphicFor
			var targetMethod = AccessTools.Method(typeof(PawnRenderNode_Hair), nameof(PawnRenderNode_Hair.GraphicFor));
			if (targetMethod != null)
			{
				harmony.Patch(
					targetMethod,
					postfix: new HarmonyMethod(typeof(Patch_BabyHairRendering), nameof(GraphicFor_Postfix))
				);
				Log.Message("[RimTalk_ToddlersExpansion] Baby hair rendering patch applied.");
			}
			else
			{
				Log.Warning("[RimTalk_ToddlersExpansion] Could not find PawnRenderNode_Hair.GraphicFor method to patch.");
			}
		}

		/// <summary>
		/// Postfix补丁：如果原方法因为婴儿阶段返回null，我们重新计算并返回头发图形
		/// </summary>
		/// <param name="__result">原方法的返回值</param>
		/// <param name="pawn">要渲染的pawn</param>
		/// <param name="__instance">PawnRenderNode_Hair实例</param>
		private static void GraphicFor_Postfix(ref Graphic __result, Pawn pawn, PawnRenderNode_Hair __instance)
		{
			// 如果已经有结果，不需要修改
			if (__result != null)
			{
				return;
			}

			// 检查是否是婴儿阶段（这是我们要修复的情况）
			if (!pawn.DevelopmentalStage.Baby() && !pawn.DevelopmentalStage.Newborn())
			{
				// 不是婴儿，原版返回null是因为其他原因（没有头发等），保持原样
				return;
			}

			// 检查pawn是否有有效的头发定义
			if (pawn.story?.hairDef == null || pawn.story.hairDef.noGraphic)
			{
				return;
			}

			// 为婴儿生成头发图形
			try
			{
				// 使用反射获取ColorFor方法的结果
				var colorForMethod = AccessTools.Method(typeof(PawnRenderNode), "ColorFor");
				if (colorForMethod != null)
				{
					var color = (UnityEngine.Color)colorForMethod.Invoke(__instance, new object[] { pawn });
					__result = pawn.story.hairDef.GraphicFor(pawn, color);
				}
				else
				{
					// 回退：使用pawn的头发颜色
					__result = pawn.story.hairDef.GraphicFor(pawn, pawn.story.HairColor);
				}
			}
			catch (System.Exception ex)
			{
				Log.ErrorOnce($"[RimTalk_ToddlersExpansion] Error generating hair graphic for baby {pawn.LabelShort}: {ex.Message}", pawn.thingIDNumber ^ 0x12345678);
			}
		}
	}
}