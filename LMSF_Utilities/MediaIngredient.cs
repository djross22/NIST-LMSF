using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMSF_Utilities
{
    class MediaIngredient
    {
        public string Name { get; set; }
        public float Concentration { get; set; }
        public string Units { get; set; }

        public MediaIngredient(string name, float conc, string units)
        {
            Name = name;
            Concentration = conc;
            Units = units;
        }

        public override string ToString()
        {
            return $"{Concentration} {Units} \t{Name}";
            
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
