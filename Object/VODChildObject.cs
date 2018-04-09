using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace VODServiceBase.Object
{
    public class VODChildObject
    {
        public string scheRootID { get; set; }
        public string serviceRef { get; set; }
        public DateTime scheduleDate { get; set; }
        public TimeSpan startTime { get; set; }
        public long totalDuration { get; set; }
        public long duration { get; set; }
        public string locale { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int? Episode { get; set; }
        public string PromoImages { get; set; }
        public int Rating { get; set; }
        public bool isCatchUp { get; set; }
        public bool isStartOver { get; set; }
        public DateTime dateStart { get; set; }
        public TimeSpan timeStart { get; set; }
        
        public VODChildObject()
        {
            serviceRef = "";
            scheRootID = "GLOBAL";
            isStartOver = true;
            isCatchUp = true;
            locale = "vi_VN";
            Rating = 0;            
        }        
    }
}
