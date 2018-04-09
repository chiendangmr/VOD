using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace VODServiceBase.Object
{
    public class VODObject
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

        XmlDocument xmlDoc = null;
        XmlNode elementDownload = null;
        XmlNode rootNode = null;
        XmlAttribute rootSchedule = null;
        XmlAttribute rootID = null;
        XmlAttribute attribute = null;
        XmlNode elementPeriod = null;
        XmlAttribute periodAttDuration = null;
        XmlAttribute periodAttStart = null;
       
        public VODObject()
        {
            serviceRef = "";
            scheRootID = "GLOBAL";
            isStartOver = true;
            isCatchUp = true;
            locale = "vi_VN";
            Rating = 0;
            xmlDoc = new XmlDocument();
            rootNode = xmlDoc.CreateElement("ScheduleProvider");
            xmlDoc.AppendChild(rootNode);
            
            rootSchedule = creatXmlAtt(xmlDoc, rootNode, "scheduleDate", this.scheduleDate.ToString("yyyy-MM-ddT")+ this.timeStart.ToString(@"hh\:mm\:ss") + "Z");
            rootID = creatXmlAtt(xmlDoc, rootNode, "id", this.scheRootID);

            elementDownload = addNodeChild(xmlDoc, "DownloadPeriod", rootNode);
            attribute = creatXmlAtt(xmlDoc, elementDownload, "serviceRef", this.serviceRef);

            elementPeriod = addNodeChild(xmlDoc, "Period", elementDownload);
            periodAttDuration = creatXmlAtt(xmlDoc, elementPeriod, "duration", this.totalDuration.ToString());
            periodAttStart = creatXmlAtt(xmlDoc, elementPeriod, "start", this.scheduleDate.ToString("yyyy-MM-ddT") + this.timeStart.ToString(@"hh\:mm\:ss") + "Z");

        }
        public void GenerateXml(VODChildObject tempObj)
        {
            try
            {
                rootSchedule = creatXmlAtt(this.xmlDoc, rootNode, "scheduleDate", tempObj.scheduleDate.ToString("yyyy-MM-ddT") + tempObj.timeStart.ToString(@"hh\:mm\:ss") + "Z");
                rootID = creatXmlAtt(this.xmlDoc, rootNode, "id", this.scheRootID);
                attribute = creatXmlAtt(this.xmlDoc, elementDownload, "serviceRef", tempObj.serviceRef);
                periodAttDuration = creatXmlAtt(this.xmlDoc, elementPeriod, "duration", this.totalDuration.ToString());
                periodAttStart = creatXmlAtt(this.xmlDoc, elementPeriod, "start", tempObj.scheduleDate.ToString("yyyy-MM-ddT") + tempObj.timeStart.ToString(@"hh\:mm\:ss") + "Z");
                XmlNode elementProgram = addNodeChild(this.xmlDoc, "Programme", elementDownload);
                XmlAttribute programTitle = creatXmlAtt(this.xmlDoc, elementProgram, "title", tempObj.Title);
                XmlAttribute programCatchUp = creatXmlAtt(this.xmlDoc, elementProgram, "isCatchUp", this.isCatchUp.ToString().ToLower());
                XmlAttribute programStartOver = creatXmlAtt(this.xmlDoc, elementProgram, "isStartOver", this.isStartOver.ToString().ToLower());

                XmlNode periodChild = addNodeChild(this.xmlDoc, "Period", elementProgram);
                XmlAttribute durationChild = creatXmlAtt(this.xmlDoc, periodChild, "duration", tempObj.duration.ToString());
                XmlAttribute startChild = creatXmlAtt(this.xmlDoc, periodChild, "start", tempObj.dateStart.ToString("yyyy-MM-ddT") + tempObj.startTime.ToString(@"hh\:mm\:ss") + "Z");

                XmlNode epgDescription = addNodeChild(this.xmlDoc, "EpgDescription", elementProgram);

                XmlNode epgElement = addNodeChild(this.xmlDoc, "EpgElement", epgDescription, tempObj.PromoImages);
                XmlAttribute key = creatXmlAtt(this.xmlDoc, epgElement, "key", "PromoImages");

                XmlNode epgElement2 = addNodeChild(this.xmlDoc, "EpgElement", epgDescription, tempObj.Rating.ToString());
                XmlAttribute key2 = creatXmlAtt(this.xmlDoc, epgElement2, "key", "Rating");

                XmlNode epgDescription2 = addNodeChild(this.xmlDoc, "EpgDescription", elementProgram);
                XmlAttribute locale = creatXmlAtt(this.xmlDoc, epgDescription2, "locale", tempObj.locale);

                XmlNode epgElement21 = addNodeChild(this.xmlDoc, "EpgElement", epgDescription2, tempObj.Title);
                XmlAttribute key21 = creatXmlAtt(this.xmlDoc, epgElement21, "key", "Title");

                XmlNode epgElement22 = addNodeChild(this.xmlDoc, "EpgElement", epgDescription2, tempObj.Description);
                XmlAttribute key22 = creatXmlAtt(this.xmlDoc, epgElement22, "key", "Description");

                XmlNode epgElement23 = addNodeChild(this.xmlDoc, "EpgElement", epgDescription2, tempObj.Episode.ToString());
                XmlAttribute key23 = creatXmlAtt(this.xmlDoc, epgElement23, "key", "Episode");                
            }
            catch { }
        }
        public void SaveXmlFile(string tempPath)
        {
            this.xmlDoc.Save(tempPath);
        }
        private XmlAttribute creatXmlAtt(XmlDocument xmlDocTemp, XmlNode nodeTemp, string nameAtt, string val)
        {
            XmlAttribute attTemp = xmlDocTemp.CreateAttribute(nameAtt);
            attTemp.Value = val;
            nodeTemp.Attributes.Append(attTemp);
            return attTemp;
        }
        private XmlNode addNodeChild(XmlDocument xmlDocTemp, string nodeName, XmlNode parentNode, string innerTxt = "")
        {
            XmlNode nodeTemp = xmlDocTemp.CreateElement(nodeName);
            parentNode.AppendChild(nodeTemp);
            if (innerTxt != "")
            {
                nodeTemp.InnerText = innerTxt;
            }
            return nodeTemp;
        }
    }
}
