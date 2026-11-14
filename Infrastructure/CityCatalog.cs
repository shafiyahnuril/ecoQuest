using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EcoQuest.Infrastructure;

/// <summary>
/// Menyediakan daftar kota kanonik yang didukung aplikasi dari Resources/Data/cities.json (ADR-012).
/// </summary>
public sealed class CityCatalog
{
    public IReadOnlyList<string> Cities { get; }

    public CityCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Data", "cities.json");
        Cities = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [];
    }

    public bool IsSupported(string? city)
        => !string.IsNullOrWhiteSpace(city) && Cities.Contains(city, StringComparer.Ordinal);
}
