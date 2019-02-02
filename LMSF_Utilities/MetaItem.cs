using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace LMSF_Utilities
{
    public class MetaItem
    {
        public string ShortID { get; set; }
        public string Name { get; set; }
        public int TimesUsed { get; set; }
        public string MetaType { get; set; }

        public MetaItem(string shortID, int timesUsed, string name, string type)
        {
            ShortID = shortID;
            Name = name;
            TimesUsed = timesUsed;
            MetaType = type;
        }

        public MetaItem(XmlNode itemNode, string type)
        {
            string shortID = "";
            string name = "";
            int timesUsed = 0;
            MetaType = type;

            string baseNodeStr = $"{MetaType}s";
            string detailNodeStr = MetaType;
            string idNodeStr = $"{MetaType}Id";
            string longNameNodeStr = $"{MetaType}LongName";
            string timesUsedNodeStr = $"timesUsed";
            string key = "";
            if (MetaType == "media")
            {
                baseNodeStr = "media";
                detailNodeStr = "medium";
                idNodeStr = "mediaId";
                longNameNodeStr = "mediaLongName";
            }
            if (MetaType == "antibiotic")
            {
                baseNodeStr = "additives";
                detailNodeStr = "additive";
                idNodeStr = "additiveId";
                longNameNodeStr = "additiveLongName";
                key = "antibiotic";
            }
            
            XmlNode idNode = itemNode.SelectSingleNode(idNodeStr);
            XmlNode longNameNode = itemNode.SelectSingleNode(longNameNodeStr);
            XmlNode timesUsedNode = itemNode.SelectSingleNode(timesUsedNodeStr);

            if (!(idNode is null))
            {
                shortID = idNode.InnerText;
            }
            if (!(longNameNode is null))
            {
                name = longNameNode.InnerText;
            }
            if (!(timesUsedNode is null))
            {
                int.TryParse(timesUsedNode.InnerText, out timesUsed);
            }

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
