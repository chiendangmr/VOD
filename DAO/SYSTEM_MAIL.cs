namespace HDStation
{
    using System;
    
    public partial class SYSTEM_MAIL
    {
        public long ID { get; set; }
        public string TARGET_MAIL { get; set; }
        public string CONTENT { get; set; }
        public DateTime? DATECREATE { get; set; }
        public DateTime? DATERESPONSE { get; set; }
        public string STATUS { get; set; }
        public int RETRY { get; set; }
        public string TITLE { get; set; }
    }
}