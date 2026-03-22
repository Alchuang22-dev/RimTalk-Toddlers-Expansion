using Verse;

namespace RimAudio
{
    public class RimAudioSettings : ModSettings
    {
        public int audioTickInterval = 500;
        public int audioRadius = 10;
        public bool uncappedAudio = false;
        public bool colonistsCanHear = true;
        public bool prisonersCanHear = true;
        public bool slavesCanHear = true;
        public bool friendlyFactionsCanHear = false;
        public bool enemyFactionsCanHear = false;
        public bool homeOnly = false;
        public bool allowMoodStacking = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref audioTickInterval, "audioTickInterval", 500);
            Scribe_Values.Look(ref audioRadius, "audioRadius", 10);
            Scribe_Values.Look(ref uncappedAudio, "uncappedAudio", false);
            Scribe_Values.Look(ref colonistsCanHear, "colonistsCanHear", true);
            Scribe_Values.Look(ref prisonersCanHear, "prisonersCanHear", true);
            Scribe_Values.Look(ref slavesCanHear, "slavesCanHear", true);
            Scribe_Values.Look(ref friendlyFactionsCanHear, "friendlyFactionsCanHear", false);
            Scribe_Values.Look(ref enemyFactionsCanHear, "enemyFactionsCanHear", false);
            Scribe_Values.Look(ref homeOnly, "homeOnly", false);
            Scribe_Values.Look(ref allowMoodStacking, "allowMoodStacking", true);
            base.ExposeData();
        }
    }
}
