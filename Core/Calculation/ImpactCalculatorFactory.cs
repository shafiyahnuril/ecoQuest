using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Calculation;

public sealed class ImpactCalculatorFactory : IImpactCalculatorFactory
{
    private readonly IReadOnlyDictionary<InputControlType, IImpactCalculator> _map;
    public ImpactCalculatorFactory(IEnumerable<IImpactCalculator> calculators) =>
        _map = calculators.ToDictionary(c => c.SupportedType);          // duplikasi tipe → exception saat startup (bagus)
    public IImpactCalculator For(InputControlType type) =>
        _map.TryGetValue(type, out var c) ? c : throw new NotSupportedException($"Tidak ada kalkulator untuk {type}");
}
