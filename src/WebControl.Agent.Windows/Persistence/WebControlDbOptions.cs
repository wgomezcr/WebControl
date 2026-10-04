namespace WebControl.Agent.Windows.Persistence;

public sealed class WebControlDbOptions
{
    public const string SectionName = "Database";

    public string Path { get; set; } = string.Empty;
}
