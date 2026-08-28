namespace ElBruno.OllamaMonitor.Models;

/// <summary>
/// A single context line in the Mini Monitor. <see cref="Text"/> is the version fitted to the
/// window's fixed width (the name may be middle-ellipsized when it is long);
/// <see cref="FullText"/> is the unabridged "name - stats" string the line's tooltip shows,
/// so a long model name is never actually lost - only visually compressed.
/// </summary>
public sealed record MiniContextLine(string Text, string FullText);
