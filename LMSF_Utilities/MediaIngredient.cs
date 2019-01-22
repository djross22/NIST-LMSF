using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMSF_Utilities
{
    public class MediaIngredient
    {
        public string Name { get; set; }
        public double Concentration { get; set; }
        public string Units { get; set; }

        public MediaIngredient(string name, double conc, string units)
        {
            Name = name;
            Concentration = conc;
            Units = units;
        }

        public override string ToString()
        {
            string units = Units;
            if (units=="%")
            {
                units = "%        ";
            }
            return $"{Concentration} {units} \t{Name}";
        }

        public string SaveString()
        {
            return $"{Name},{Concentration},{Units}";
        }

        public static string HeaderString()
        {
            return "Ingredient,Concentration,Units";
        }
    }
}
