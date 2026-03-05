using System.Text.Json;
using System.Text.Json.Serialization;
using ChatLogTaker.Models;

namespace ChatLogTaker.Export;

public class JsonExporter(string outputDir)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _outputDir = outputDir;

    public async Task ExportAsync(ChatLog log)
    {
        Directory.CreateDirectory(_outputDir);

        var safeName = MakeSafeFileName(log.TeamName is not null
            ? $"{log.TeamName}_{log.Name}"
            : log.Name);

        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var fileName = $"{log.Type}_{safeName}_{date}.json";
        var path = Path.Combine(_outputDir, fileName);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, log, JsonOptions);

        Console.WriteLine($"    Saved: {path}");
    }

    private static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c))
                     .Trim('_')
                     [..Math.Min(80, name.Length)];
    }
}
