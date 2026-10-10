using System;

namespace DragLab.Domain
{
    public enum SpeedUnit { KilometresPerHour, MilesPerHour, MetresPerSecond }
    public enum PressureUnit { Bar, Psi, Kilopascals }
    public enum TorqueUnit { NewtonMetres, PoundFeet }
    [Serializable] public sealed class UnitPreferences
    {
        public int version = 1;
        public SpeedUnit speed;
        public PressureUnit pressure;
        public TorqueUnit torque;
        public void Sanitize()
        {
            if (!Enum.IsDefined(typeof(SpeedUnit), speed)) speed = SpeedUnit.KilometresPerHour;
            if (!Enum.IsDefined(typeof(PressureUnit), pressure)) pressure = PressureUnit.Bar;
            if (!Enum.IsDefined(typeof(TorqueUnit), torque)) torque = TorqueUnit.NewtonMetres;
        }
    }
    public static class DisplayUnits
    {
        // Game currency keeps the established dollar prefix; separators follow the user's locale.
        public static string Money(double amount, int decimals = 0, bool signed = false)
        {
            return (amount < 0 ? "-" : signed && amount > 0 ? "+" : "") + "$" +
                Math.Abs(amount).ToString("N" + decimals, System.Globalization.CultureInfo.CurrentCulture);
        }
        public const double NewtonMetresPerPoundFoot = 1.3558179483314004;
        public static double Torque(double nm, TorqueUnit unit) { return unit == TorqueUnit.PoundFeet ? nm / NewtonMetresPerPoundFoot : nm; }
        public static double ToNewtonMetres(double value, TorqueUnit unit) { return unit == TorqueUnit.PoundFeet ? value * NewtonMetresPerPoundFoot : value; }
        public static string TorqueLabel(TorqueUnit unit) { return unit == TorqueUnit.PoundFeet ? "lb-ft" : "Nm"; }
        public static string FormatTorque(double nm, UnitPreferences units, string format = "F0") { return Torque(nm, units.torque).ToString(format) + " " + TorqueLabel(units.torque); }
        public const double PsiPerBar = 14.503773773, MetresPerInch = .0254;
        public static double Speed(double mps, SpeedUnit unit) { return unit == SpeedUnit.MilesPerHour ? mps * 2.236936292 : unit == SpeedUnit.MetresPerSecond ? mps : mps * 3.6; }
        public static string SpeedLabel(SpeedUnit unit) { return unit == SpeedUnit.MilesPerHour ? "mph" : unit == SpeedUnit.MetresPerSecond ? "m/s" : "km/h"; }
        public static double Pressure(double bar, PressureUnit unit) { return unit == PressureUnit.Psi ? bar * PsiPerBar : unit == PressureUnit.Kilopascals ? bar * 100 : bar; }
        public static double ToBar(double value, PressureUnit unit) { return unit == PressureUnit.Psi ? value / PsiPerBar : unit == PressureUnit.Kilopascals ? value / 100 : value; }
        public static string PressureLabel(PressureUnit unit) { return unit == PressureUnit.Psi ? "psi" : unit == PressureUnit.Kilopascals ? "kPa" : "bar"; }
        public static string FormatSpeed(double mps, UnitPreferences units, string format = "F1") { return Speed(mps, units.speed).ToString(format) + " " + SpeedLabel(units.speed); }
        public static string FormatPressure(double bar, UnitPreferences units) { return Pressure(bar, units.pressure).ToString("F2") + " " + PressureLabel(units.pressure); }
        public static string Boost(double bar, UnitPreferences units) { return units.pressure == PressureUnit.Psi ? (bar * PsiPerBar).ToString("F1") + " psi / " + bar.ToString("F2") + " bar" : FormatPressure(bar, units) + " / " + (bar * PsiPerBar).ToString("F1") + " psi"; }
    }
}
