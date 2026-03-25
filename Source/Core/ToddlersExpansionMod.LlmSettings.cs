using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public sealed partial class ToddlersExpansionMod
	{
		private static void DrawLlmSettingsSection(Listing_Standard listingStandard, ToddlersExpansionSettings settings)
		{
			listingStandard.Label("RimTalk_ToddlersExpansion_Llm_Settings_Header".Translate());
			listingStandard.GapLine();
			listingStandard.Label("RimTalk_ToddlersExpansion_Llm_Settings_Desc".Translate());
			listingStandard.Gap();

			Rect rimTalkRect = listingStandard.GetRect(24f);
			if (Widgets.RadioButtonLabeled(
				rimTalkRect,
				"RimTalk_ToddlersExpansion_Llm_UseRimTalk".Translate(),
				!settings.UseStandaloneLlmApi))
			{
				settings.UseStandaloneLlmApi = false;
			}

			Rect standaloneRect = listingStandard.GetRect(24f);
			if (Widgets.RadioButtonLabeled(
				standaloneRect,
				"RimTalk_ToddlersExpansion_Llm_UseStandalone".Translate(),
				settings.UseStandaloneLlmApi))
			{
				settings.UseStandaloneLlmApi = true;
			}

			Text.Font = GameFont.Tiny;
			GUI.color = Color.gray;
			Rect noteRect = listingStandard.GetRect(Text.LineHeight * 2f);
			Widgets.Label(noteRect, "RimTalk_ToddlersExpansion_Llm_ScopeNote".Translate());
			GUI.color = Color.white;
			Text.Font = GameFont.Small;
			listingStandard.Gap();

			if (settings.UseStandaloneLlmApi)
			{
				DrawStandaloneApiConfig(listingStandard, settings.StandaloneApi);
			}
			else if (!RimTalkCompatUtility.IsRimTalkActive)
			{
				GUI.color = Color.yellow;
				listingStandard.Label("RimTalk_ToddlersExpansion_Llm_RimTalkUnavailable".Translate());
				GUI.color = Color.white;
				listingStandard.Gap();
			}
		}

		private static void DrawStandaloneApiConfig(Listing_Standard listingStandard, ToddlersExpansionStandaloneApiConfig config)
		{
			Rect providerRow = listingStandard.GetRect(30f);
			DrawLabeledButtonRow(
				providerRow,
				"RimTalk_ToddlersExpansion_Llm_Provider".Translate().ToString(),
				config.Provider.GetLabel(),
				() => ShowStandaloneProviderMenu(config));
			listingStandard.Gap(4f);

			if (config.RequiresCustomBaseUrl)
			{
				Rect baseUrlRow = listingStandard.GetRect(30f);
				config.BaseUrl = DrawLabeledTextFieldRow(
					baseUrlRow,
					"RimTalk_ToddlersExpansion_Llm_BaseUrl".Translate().ToString(),
					config.BaseUrl);
				listingStandard.Gap(4f);
			}
			else
			{
				Text.Font = GameFont.Tiny;
				GUI.color = Color.gray;
				Rect endpointRect = listingStandard.GetRect(Text.LineHeight * 2f);
				Widgets.Label(endpointRect, "RimTalk_ToddlersExpansion_Llm_Endpoint".Translate(config.GetResolvedBaseUrl()));
				GUI.color = Color.white;
				Text.Font = GameFont.Small;
				listingStandard.Gap(4f);
			}

			Rect apiKeyRow = listingStandard.GetRect(30f);
			config.ApiKey = DrawLabeledTextFieldRow(
				apiKeyRow,
				"RimTalk_ToddlersExpansion_Llm_ApiKey".Translate().ToString(),
				config.ApiKey);
			listingStandard.Gap(4f);

			Rect modelRow = listingStandard.GetRect(30f);
			config.Model = DrawLabeledTextFieldRow(
				modelRow,
				"RimTalk_ToddlersExpansion_Llm_Model".Translate().ToString(),
				config.Model);
			listingStandard.Gap(4f);

			if (!config.IsValid())
			{
				GUI.color = Color.yellow;
				listingStandard.Label("RimTalk_ToddlersExpansion_Llm_ConfigWarning".Translate());
				GUI.color = Color.white;
			}
		}

		private static void ShowStandaloneProviderMenu(ToddlersExpansionStandaloneApiConfig config)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ToddlersExpansionStandaloneLlmProvider provider in
				System.Enum.GetValues(typeof(ToddlersExpansionStandaloneLlmProvider)))
			{
				ToddlersExpansionStandaloneLlmProvider captured = provider;
				options.Add(new FloatMenuOption(provider.GetLabel(), delegate
				{
					config.Provider = captured;
					if (!config.RequiresCustomBaseUrl)
					{
						config.BaseUrl = string.Empty;
					}
				}));
			}

			Find.WindowStack.Add(new FloatMenu(options));
		}

		private static string DrawLabeledTextFieldRow(Rect rowRect, string label, string value)
		{
			const float labelWidth = 180f;
			Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
			Rect fieldRect = new Rect(labelRect.xMax + 10f, rowRect.y, rowRect.width - labelWidth - 10f, rowRect.height);
			Widgets.Label(labelRect, label);
			return Widgets.TextField(fieldRect, value ?? string.Empty);
		}

		private static void DrawLabeledButtonRow(Rect rowRect, string label, string buttonText, System.Action onClick)
		{
			const float labelWidth = 180f;
			Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
			Rect buttonRect = new Rect(labelRect.xMax + 10f, rowRect.y, rowRect.width - labelWidth - 10f, rowRect.height);
			Widgets.Label(labelRect, label);
			if (Widgets.ButtonText(buttonRect, buttonText))
			{
				onClick?.Invoke();
			}
		}
	}
}
