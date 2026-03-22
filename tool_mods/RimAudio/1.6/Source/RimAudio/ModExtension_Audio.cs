using RimWorld;
using Verse;

namespace RimAudio
{
    public class ModExtension_Audio : DefModExtension
    {
        public ThoughtDef thought;
        public float radius = -1f;
        public float loudness = 1f;
        public bool requireLineOfSight = true;
        public bool indoorsOnly;
        public bool outdoorsOnly;
        public int minInterval;
    }
}
