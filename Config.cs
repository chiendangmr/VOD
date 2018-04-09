using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VODServiceBase
{
    public class Config
    {
        public string DBStationConnectionString { get; set; }

        public string AsRunLogFolder { get; set; }

        public string AdTypes { get; set; }

        /// <summary>
        /// Max duration of ad clip
        /// </summary>
        public long MaxAdDuration { get; set; }

        public string VODFolder { get; set; }
        public string VODXmlFolder { get; set; }
        public string sessionStr { get; set; }

        /// <summary>
        /// Second time bettween 2 sync
        /// </summary>
        public int ImportTime { get; set; }

        /// <summary>
        /// Second time bettween 2 sync
        /// </summary>
        public int ExportTime { get; set; }

        public int LastExportDay { get; set; }

        public int LastExportHour { get; set; }

        public string EmailSend { get; set; }

        public Config()
        {
            DBStationConnectionString = "";
            AsRunLogFolder = "";
            AdTypes = "";
            MaxAdDuration = 7500;
            VODFolder = "";
            VODXmlFolder = "";
            sessionStr = "";
            ImportTime = 30 * 60;
            ExportTime = 30 * 60;
            LastExportDay = 0;
            LastExportHour = -1;
            EmailSend = "";
        }
    }
}
