using System;
using Xunit;

namespace EcoQuest.Tests.TestDoubles;

/// <summary>
/// Test yang memanggil Supabase SUNGGUHAN. Otomatis di-skip kecuali ECOQUEST_IT=true.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ECOQUEST_IT"), "true", StringComparison.OrdinalIgnoreCase))
            Skip = "Uji integrasi: set ECOQUEST_IT=true dan isi .env untuk menjalankan";
    }
}
