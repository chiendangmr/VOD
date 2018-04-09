using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HDStation
{
    public class ETERE_AS_RUN_LOG_PLAYLIST
    {
        public int ID { get; set; }

        public int SectionID { get; set; }

        public DateTime DateList { get; set; }
    }

    public class ETERE_AS_RUN_LOG_PLAYLIST_ITEM
    {
        public long ID { get; set; }

        public int ListID { get; set; }

        public DateTime StartTime { get; set; }

        public long Duration { get; set; }

        public long AssetID { get; set; }

        public string Type { get; set; }

        public string Title { get; set; }
    }
}
