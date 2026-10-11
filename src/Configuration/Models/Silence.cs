using System.Text.Json.Serialization;

namespace LunaPlayer.Configuration.Models;

internal enum SilenceDetection { Peak, Rms }

internal sealed class SilenceSettings
{
    public bool Enabled { get; set; }
    public bool Advanced { get; set; }
    public int StartPeriods { get; set; } = 1;
    public double StartDuration { get; set; } = 0.2;
    public double Threshold { get; set; } = -30;
    public int StopPeriods { get; set; } = -1;
    public double StopDuration { get; set; } = 0.5;
    public double StopSilence { get; set; } = 0.2;
    public double Window { get; set; } = 0.02;
    [JsonConverter(typeof(JsonStringEnumConverter<SilenceDetection>))]
    public SilenceDetection Detection { get; set; } = SilenceDetection.Peak;
    internal SilenceSettings Copy() => (SilenceSettings)MemberwiseClone();
    internal void Apply(SilenceSettings source)
    {
        Enabled = source.Enabled;
        Advanced = source.Advanced;
        StartPeriods = source.StartPeriods;
        StartDuration = source.StartDuration;
        Threshold = source.Threshold;
        StopPeriods = source.StopPeriods;
        StopDuration = source.StopDuration;
        StopSilence = source.StopSilence;
        Window = source.Window;
        Detection = source.Detection;
    }
}
