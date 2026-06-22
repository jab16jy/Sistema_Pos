using System.Text.Json;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence;

public sealed class LocalFileOperatorSession : ICurrentOperatorSession
{
    private readonly Guid _operatorId;

    public LocalFileOperatorSession()
    {
        _operatorId = LoadOrCreate();
    }

    public Guid OperatorId => _operatorId;

    private static Guid LoadOrCreate()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SistemaPos");
        Directory.CreateDirectory(dir);

        var file = Path.Combine(dir, "operator_session.json");

        if (File.Exists(file))
        {
            try
            {
                var json = File.ReadAllText(file);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("operatorId", out var prop) &&
                    Guid.TryParse(prop.GetString(), out var stored) &&
                    stored != Guid.Empty)
                    return stored;
            }
            catch { /* fall through to create */ }
        }

        var id = Guid.NewGuid();
        File.WriteAllText(file, JsonSerializer.Serialize(new { operatorId = id }));
        return id;
    }
}
