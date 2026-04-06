using System.Collections.Generic;
using Verse;

namespace RimTalk_ToddlersExpansion.Defs
{
	public class ToddlerSoulPoolDef : Def
	{
		public List<ToddlerSoulEntry> entries = new List<ToddlerSoulEntry>();

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors())
			{
				yield return error;
			}

			if (entries == null || entries.Count == 0)
			{
				yield return $"{defName ?? "<unnamed>"} has no soul entries.";
				yield break;
			}

			for (int i = 0; i < entries.Count; i++)
			{
				ToddlerSoulEntry entry = entries[i];
				if (entry == null)
				{
					yield return $"{defName ?? "<unnamed>"} has null entry at index {i}.";
					continue;
				}

				if (string.IsNullOrWhiteSpace(entry.textKey) && string.IsNullOrWhiteSpace(entry.text))
				{
					yield return $"{defName ?? "<unnamed>"} entry {i} has no textKey or text.";
				}

				if (entry.weight <= 0f)
				{
					yield return $"{defName ?? "<unnamed>"} entry {i} has non-positive weight.";
				}

				if (entry.chattiness < 0f || entry.chattiness > 1f)
				{
					yield return $"{defName ?? "<unnamed>"} entry {i} has invalid chattiness {entry.chattiness}.";
				}
			}
		}
	}

	public class ToddlerSoulEntry
	{
		public YoungPawnSoulAgeGroup ageGroup = YoungPawnSoulAgeGroup.Toddler;
		public string textKey;
		public string text;
		public float chattiness = 0.5f;
		public float weight = 1f;
	}
}