namespace SunshineLibrary.Models
{
    /// <summary>
    /// Settings for deterministic HDR automation driven by the managed Playnite
    /// category synchronized through Vibeshine. Only HDR is applied to stream
    /// overrides; every other Moonlight preference remains inherited unless the
    /// user explicitly configured an existing SunshineLibrary override.
    /// </summary>
    public sealed class HdrAutomationOptions
    {
        public bool Enabled { get; set; } = true;
        public string HdrCategoryName { get; set; } = "HDR Bridge: HDR";
        public bool ControlClientHdr { get; set; } = true;
        public bool RestoreClientToSdr { get; set; } = true;
        public bool CancelLaunchOnSwitchFailure { get; set; } = true;
    }
}
