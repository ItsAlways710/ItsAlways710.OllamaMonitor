using System.Text;
using ItsAlways710.OllamaMonitor.Models;

namespace ItsAlways710.OllamaMonitor.Helpers;

public static class SnapshotFormatter
{
    public static string ToMultilineStatus(OllamaMonitorSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"State: {StatusTextHelper.GetStateLabel(snapshot.State)}");
        builder.AppendLine($"Endpoint: {snapshot.Endpoint}");
        builder.AppendLine($"API Reachable: {(snapshot.IsApiReachable ? "Yes" : "No")}");
        builder.AppendLine($"Version: {snapshot.Version ?? "Unavailable"}");
        builder.AppendLine($"Last Checked: {snapshot.LastChecked:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"Loaded Models: {GetModelSummary(snapshot.Models)}");

        if (snapshot.Resources is not null)
        {
            builder.AppendLine($"CPU: {StatusTextHelper.FormatPercent(snapshot.Resources.CpuPercent)}");
            builder.AppendLine($"Memory: {StatusTextHelper.FormatBytes(snapshot.Resources.MemoryBytes)}");
            builder.AppendLine($"Private Memory: {StatusTextHelper.FormatBytes(snapshot.Resources.PrivateMemoryBytes)}");
            builder.AppendLine($"Disk Read: {StatusTextHelper.FormatBytesPerSecond(snapshot.Resources.DiskReadBytesPerSecond)}");
            builder.AppendLine($"Disk Write: {StatusTextHelper.FormatBytesPerSecond(snapshot.Resources.DiskWriteBytesPerSecond)}");
            builder.AppendLine($"GPU: {StatusTextHelper.BuildGpuSummary(snapshot.Resources)}");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
        {
            builder.AppendLine($"Error: {snapshot.ErrorMessage}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string GetModelSummary(IReadOnlyList<OllamaModelSnapshot> models) =>
        models.Count == 0 ? "No loaded models" : string.Join(", ", models.Select(model => model.Name));
}
