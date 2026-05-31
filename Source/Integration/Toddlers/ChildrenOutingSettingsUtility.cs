using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ChildrenOutingSettingsUtility
	{
		private const string ChildrenOutingDefName = "RimTalk_ChildrenOuting";
		private static float? _baseRandomSelectionWeight;

		public static void ApplyConfiguredRandomSelectionWeight()
		{
			GatheringDef def = DefDatabase<GatheringDef>.GetNamedSilentFail(ChildrenOutingDefName);
			if (def == null)
			{
				return;
			}

			if (!_baseRandomSelectionWeight.HasValue)
			{
				_baseRandomSelectionWeight = def.randomSelectionWeight;
			}

			def.randomSelectionWeight = _baseRandomSelectionWeight.Value * ToddlersExpansionSettings.GetChildrenOutingChanceFactor();
		}
	}
}
