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
        public Concentration Conc { get; set; }

        public MediaIngredient(string name, double concValue, string units)
        {
            Name = name;
            Conc = new Concentration(concValue, units);
        }

        public MediaIngredient(string name, Concentration conc)
        {
            Name = name;
            Conc = conc;
        }

        //This ToString method is aimed at formating the MediaIngredient for listing in a ListBox or other list control
        //    the extra spaces added after the "%" units are a kludge to get the formatting nice without adjusting the tab stop positions in the ListBox
        public override string ToString()
        {
            string units = Conc.Units;
            if (units=="%")
            {
                units = "%        ";
            }
            return $"{Conc.ConcValue} \t{units} \t\t{Name}";
        }

        public string SaveString()
        {
            return $"{Name},{Conc.ConcValue},{Conc.Units}";
        }

        public static string HeaderString()
        {
            return "Ingredient,Concentration,Units";
        }
    }

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
