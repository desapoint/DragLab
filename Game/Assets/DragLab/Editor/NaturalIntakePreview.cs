using DragLab.Domain;
namespace DragLab.Editor
{
    public sealed class NaturalIntakePreview : IntakeRushSynth
    {
        public NaturalIntakePreview(EngineSoundProfile profile, int sampleRate = 48000, uint seed = 171818, Voice voice = Voice.Airflow)
            : base(profile, sampleRate, seed, voice) { }
    }
}
