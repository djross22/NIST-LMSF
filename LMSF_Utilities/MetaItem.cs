using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMSF_Utilities
{
    public class MetaItem
    {
        public string ShortID { get; set; }
        public string Name { get; set; }
        public int TimesUsed { get; set; }

        public MetaItem(string shortID, int timesUsed, string name)
        {
            ShortID = shortID;
            Name = name;
            TimesUsed = timesUsed;
        }

        public override string ToString()
        {
            return ShortID;
        }

        public string SaveString()
        {
            return ShortID + SharedParameters.Delimeter + $"{TimesUsed}" + SharedParameters.Delimeter + Name;
        }
    }
}
