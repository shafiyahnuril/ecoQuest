namespace ecoQuest.Services
{
    // Interface untuk strategy pattern kalkulasi dampak (poin & CO2)
    public interface IImpactCalculator
    {
        int CalculatePoints(decimal co2SavedKg, decimal pointFactor);
        decimal CalculateCo2(decimal quantity, decimal factor);
    }
}