using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ecoQuest.Services
{
    // strategy untuk aktivitas berbasis jarak
    public class DistanceImpactCalculator : IImpactCalculator
    {
        public int CalculatePoints(decimal quantity, decimal pointFactor)
        {
            return (int)(quantity * pointFactor);
        }

        public decimal CalculateCo2(decimal quantity, decimal factor)
        {
            return quantity * factor;
        }
    }
}