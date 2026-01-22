using RimTalk_ToddlersExpansion.Harmony;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public sealed class ToddlersExpansionMod : Mod
	{
		public static ToddlersExpansionSettings Settings;
		private static Vector2 scrollPosition = Vector2.zero;

		public ToddlersExpansionMod(ModContentPack content) : base(content)
		{
			Settings = GetSettings<ToddlersExpansionSettings>();

			HarmonyBootstrap.Init();
			LongEventHandler.ExecuteWhenFinished(() =>
			{
				DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionJobDefOf));
				DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionJoyGiverDefOf));
				DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionHediffDefOf));
				DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionThoughtDefOf));
				DefOfHelper.EnsureInitializedInCtor(typeof(ToddlersExpansionAnimationDefOf));
				ToddlersExpansionDiagnostics.Run();
				RimTalkCompatUtility.TryRegisterToddlerVariables();
			});
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			var settings = Settings;
			var listingStandard = new Listing_Standard();

			listingStandard.Begin(inRect);

			// 标题
			Text.Font = GameFont.Medium;
			listingStandard.Label("RimTalk_ToddlersExpansion_Settings_Title".Translate());
			Text.Font = GameFont.Small;
			listingStandard.Gap();

			// 启用过路者/商队幼儿生成
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_EnableCaravanToddlerGeneration".Translate(), ref settings.EnableCaravanToddlerGeneration, "RimTalk_ToddlersExpansion_EnableCaravanToddlerGeneration_Tooltip".Translate());
			listingStandard.Gap();

			if (settings.EnableCaravanToddlerGeneration)
			{
				listingStandard.Label("RimTalk_ToddlersExpansion_MaxToddlersPerGroup".Translate(settings.MaxToddlersPerGroup));
				settings.MaxToddlersPerGroup = (int)listingStandard.Slider(settings.MaxToddlersPerGroup, 0, 5);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_MaxChildrenPerGroup".Translate(settings.MaxChildrenPerGroup));
				settings.MaxChildrenPerGroup = (int)listingStandard.Slider(settings.MaxChildrenPerGroup, 0, 5);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_ToddlerGenerationChance".Translate(settings.ToddlerGenerationChance.ToStringPercent()));
				settings.ToddlerGenerationChance = listingStandard.Slider(settings.ToddlerGenerationChance, 0f, 1f);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_ChildGenerationChance".Translate(settings.ChildGenerationChance.ToStringPercent()));
				settings.ChildGenerationChance = listingStandard.Slider(settings.ChildGenerationChance, 0f, 1f);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_MinBatchCount".Translate(settings.MinBatchCount));
				settings.MinBatchCount = (int)listingStandard.Slider(settings.MinBatchCount, 1, 5);
				listingStandard.Gap();

				listingStandard.Label("RimTalk_ToddlersExpansion_MaxBatchCount".Translate(settings.MaxBatchCount));
				settings.MaxBatchCount = (int)listingStandard.Slider(settings.MaxBatchCount, 1, 5);
				listingStandard.Gap();

				// 确保最小值不大于最大值
				if (settings.MinBatchCount > settings.MaxBatchCount)
				{
					settings.MinBatchCount = settings.MaxBatchCount;
				}

				listingStandard.Label("RimTalk_ToddlersExpansion_ExtraBatchChance".Translate(settings.ExtraBatchChance.ToStringPercent()));
				settings.ExtraBatchChance = listingStandard.Slider(settings.ExtraBatchChance, 0f, 1f);
				listingStandard.Gap();
			}

			listingStandard.End();
		}

		public override string SettingsCategory() => "RimTalk Toddlers Expansion";
	}
}
