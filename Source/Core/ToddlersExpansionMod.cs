using RimTalk_ToddlersExpansion.Harmony;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public sealed class ToddlersExpansionMod : Mod
	{
		public static ToddlersExpansionSettings Settings;

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
	}
}
