namespace DragLab.Domain
{
    public enum EngineBenchControlSource { Sliders, Keyboard, Scenario }

    // The selected source persists. A released key must never fall back to an old
    // slider pedal or a scenario's fixed-RPM override.
    public sealed class EngineBenchInput
    {
        public EngineBenchControlSource Source { get; private set; }
        public EngineBenchControls Effective { get; private set; } = new EngineBenchControls();
        public void Sliders(EngineBenchControls controls) { Source = EngineBenchControlSource.Sliders; Publish(controls); }
        public void Scenario(EngineBenchControls controls) { Source = EngineBenchControlSource.Scenario; Publish(controls); }
        public EngineBenchControls Keyboard()
        {
            var controls = Effective.Copy();
            controls.throttle = controls.clutch = controls.brake = 0;
            controls.fixedRpm = false;
            controls.warmthOverride = controls.damageOverride = -1;
            Source = EngineBenchControlSource.Keyboard;
            Publish(controls);
            return controls;
        }
        public EngineBenchControls Resolve(EngineBenchControls assigned, bool gas, bool clutch, bool brake)
        {
            var controls = assigned.Copy();
            if (Source == EngineBenchControlSource.Keyboard)
            {
                controls.throttle = gas ? 1 : 0;
                controls.clutch = clutch ? 1 : 0;
                controls.brake = brake ? 1 : 0;
            }
            return controls;
        }
        public void Publish(EngineBenchControls controls, CarState state = null)
        {
            Effective = controls.Copy();
            if (state == null) return;
            Effective.throttle = (float)state.throttle;
            Effective.clutch = (float)state.clutch;
            Effective.brake = (float)state.brake;
            Effective.gear = state.gear;
            Effective.roadSpeedKmh = (float)(state.speed * 3.6);
            Effective.tireSlip = (float)state.slip;
        }
    }
}
