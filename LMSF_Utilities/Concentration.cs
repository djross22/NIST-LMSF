using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMSF_Utilities
{
    public class Concentration
    {
        public double ConcValue { get; set; }
        public string Units { get; set; }

        public Concentration(double concValue, string units)
        {
            ConcValue = concValue;
            Units = units;
        }

        public override string ToString()
        {
            return $"{ConcValue} {Units}";
        }
    }
}
