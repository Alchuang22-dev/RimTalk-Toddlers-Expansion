using UnityEngine;
using Verse;
using System.Linq;

namespace RimAudio
{
    [StaticConstructorOnStartup]
    public class RimAudioMod : Mod
    {
        public static RimAudioSettings Settings;

        public RimAudioMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimAudioSettings>();
            Log.Message("RIMAUDIO: startup successful.");
            LogAttachmentDiagnostics();
        }

        public override string SettingsCategory()
        {
            return "RimAudio";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.Label($"Audio check interval (default: 500): {Settings.audioTickInterval} ticks");
            list.Label("How often pawns evaluate nearby sounds. Lower values are more responsive but cost more performance.");
            Settings.audioTickInterval = (int)list.Slider(Settings.audioTickInterval, 60, 1000);

            list.Label($"Audio radius (default: 10): {Settings.audioRadius}");
            list.Label("Default scan radius for local audio sources when an extension does not specify its own radius.");
            Settings.audioRadius = (int)list.Slider(Settings.audioRadius, 2, 32);

            list.CheckboxLabeled("Uncapped mode", ref Settings.uncappedAudio, "If enabled, pawns can keep all qualifying audio mood effects simultaneously instead of only the top three strongest ones.");
            list.CheckboxLabeled("Allow mood stacking", ref Settings.allowMoodStacking, "If enabled, audio thoughts can stack up to their stack limit. If disabled, only one instance of each audio thought is kept.");
            list.CheckboxLabeled("Home area only", ref Settings.homeOnly, "If enabled, local scans only count sources inside the home area. Weather and game conditions still apply globally.");

            list.GapLine();
            list.Label("Pawn hearing eligibility:");
            list.CheckboxLabeled("Colonists can hear", ref Settings.colonistsCanHear);
            list.CheckboxLabeled("Prisoners can hear", ref Settings.prisonersCanHear);
            list.CheckboxLabeled("Slaves can hear", ref Settings.slavesCanHear);
            list.CheckboxLabeled("Friendly faction pawns can hear", ref Settings.friendlyFactionsCanHear);
            list.CheckboxLabeled("Enemy faction pawns can hear", ref Settings.enemyFactionsCanHear);

            list.End();
        }

        private static void LogAttachmentDiagnostics()
        {
            var humanlikes = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def?.race != null && def.race.Humanlike)
                .ToList();

            int attached = 0;
            var missing = new System.Collections.Generic.List<string>();

            for (int i = 0; i < humanlikes.Count; i++)
            {
                ThingDef def = humanlikes[i];
                bool hasTracker = def.comps != null && def.comps.Any(comp => comp?.compClass == typeof(Pawn_AudioTracker));
                if (hasTracker)
                {
                    attached++;
                }
                else
                {
                    missing.Add(def.defName);
                }
            }

            Log.Message($"RIMAUDIO: Pawn_AudioTracker attached to {attached}/{humanlikes.Count} humanlike race defs.");
            if (missing.Count > 0)
            {
                Log.Warning($"RIMAUDIO: missing Pawn_AudioTracker on humanlike defs: {string.Join(", ", missing.Take(12))}");
            }
        }
    }
}
