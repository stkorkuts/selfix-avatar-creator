namespace Selfix.Jobs.Shared.Settings;

public sealed class EnvironmentSettings
{
    public required string UnetPath { get; set; }
    public required string ClipLargePath { get; set; }
    public required string T5XXLPath { get; set; }
    public required string VAEPath { get; set; }
    public required string TrainScriptPath { get; set; }
    public required string CaptionsScriptPath { get; set; }
    public required string InputDir { get; set; }
    public required string OutputDir { get; set; }
    public required bool IsHighVram { get; set; }
}