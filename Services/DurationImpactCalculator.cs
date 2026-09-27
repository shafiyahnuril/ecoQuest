using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ecoQuest.Services
{
    // strategy untuk aktivitas berbasis durasi
    public class DurationImpactCalculator : IImpactCalculator
    {
        public int CalculatePoints(decimal co2SavedKg, decimal pointFactor)
        {
            return (int)Math.Round(co2SavedKg * 20m * pointFactor);
        }

        public decimal CalculateCo2(decimal quantity, decimal factor)
        {
            return quantity * factor;
        }
    }
}