using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace VODServiceBase.Object.EtereAsRunLog
{
    [Serializable]
    [XmlRoot("data")]
    public class AsRunLogFile
    {
        [XmlIgnore]
        public DateTime ExportTime { get; set; }

        [XmlAttribute("datetime")]
        public string ExportTimeString
        {
            get { return ExportTime.ToString("yyyy-MM-dd HH:mm"); }

            set { ExportTime = DateTime.ParseExact(value, "yyyy-MM-dd HH:mm", null); }
        }

        [XmlElement("TPALINSE")]
        public PlayList PlayList { get; set; }
    }

    [Serializable]
    [XmlRoot("TPALINSE")]
    public class PlayList
    {
        [XmlIgnore]
        public DateTime ExportTime { get; set; }

        [XmlIgnore]
        public string StationName { get; set; }

        [XmlIgnore]
        public int Level { get; set; }

        [XmlIgnore]
        public DateTime DateList { get; set; }

        [XmlIgnore]
        public int StationId { get; set; }

        [XmlAttribute("lastrevision")]
        public string ExportTimeString
        {
            get { return ExportTime.ToString("yyyy-MM-dd HH:mm"); }

            set { ExportTime = DateTime.ParseExact(value, "yyyy-MM-dd HH:mm", null); }
        }

        [XmlAttribute("description")]
        public string Description
        {
            get
            {
                return string.Format("Station={0} (Level={1}) Date Exported={2} User={3}", StationName, Level, DateList.ToString("yyyy-MM-dd"), StationId);
            }

            set
            {
                Match match = Regex.Match(value, @"Station=(?<StationName>.+)\s+\(Level=(?<Level>\d+)\)\s+Date Exported=(?<DateList>[\d-]+)\s+User=(?<StationId>\d+)");
                if (match.Success)
                {
                    StationName = match.Groups["StationName"].Value;
                    Level = int.Parse(match.Groups["Level"].Value);
                    DateList = DateTime.ParseExact(match.Groups["DateList"].Value, "yyyy-MM-dd", null).Date;
                    StationId = int.Parse(match.Groups["StationId"].Value);
                }
            }
        }

        [XmlElement("row")]
        public PlayListItem[] Items { get; set; }
    }

    [Serializable]
    [XmlRoot("row")]
    public class PlayListItem
    {
        [XmlElement("ID_FILMATI")]
        public long AssetID { get; set; }

        [XmlElement("NEWTYPE")]
        public string TypeName { get; set; }

        [XmlElement("ORA")]
        public long TimeIn { get; set; }

        [XmlElement("TITLE")]
        public string ProgramName { get; set; }
    }
}
