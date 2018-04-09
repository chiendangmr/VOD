using HDCPProtocol2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Dapper;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;

namespace VODServiceBase
{
    public class ExportVOD : IThreadClass
    {
        MainClass Owner = null;
        public ExportVOD(MainClass owner)
        {
            this.Owner = owner;
        }

        /// <summary>
        /// Tên tiến trình
        /// </summary>
        public string ThreadName { get { return "Export VOD"; } }

        private bool isBusy = false;
        /// <summary>
        /// Trạng thái tiến trình
        /// </summary>
        /// <returns></returns>
        public bool IsBusy()
        {
            return isBusy;
        }

        /// <summary>
        /// Hủy 1 hàng trong tiến trình
        /// </summary>
        /// <param name="index">index của dòng cần dừng</param>
        public void CancelIndex(long index) { }

        public void RemoveIndex(long index) { }

        /// <summary>
        /// Ưu tiên hàng trong tiến trình
        /// </summary>
        /// <param name="index"></param>
        public void UuTienIndex(long index) { }

        /// <summary>
        /// Làm ngay hàng trong tiến trình
        /// </summary>
        /// <param name="index"></param>
        public void LamNgayIndex(long index) { }

        private bool isRun = false;
        /// <summary>
        /// Bắt đầu tiến trình
        /// </summary>
        public void Start()
        {
            if (!isBusy)
            {
                isRun = true;

                Thread thr = new Thread(ExportThread);
                thr.IsBackground = true;
                thr.Start();
            }
        }

        /// <summary>
        /// Khởi động lại tiến trình
        /// </summary>
        public void Restart()
        {
            bool needRun = isRun;

            if (isBusy)
            {
                Stop();

                while (isBusy)
                    Thread.Sleep(100);
            }

            if (needRun)
                Start();
        }

        /// <summary>
        /// Dừng tiến trình
        /// </summary>
        public void Stop()
        {
            isRun = false;
        }

        /// <summary>
        /// Dừng tiến trình khi kết thúc
        /// </summary>
        public void StopWhenFinish()
        {
            isRun = false;
        }

        /// <summary>
        /// Sự kiện khi tiến trình bắt đầu hoặc dừng
        /// </summary>
        public event EventHandler OnBusyChanged;

        string ChuanHoaTu(string word)
        {
            if (word == "" || word.Length == 1)
                return word.ToUpper();
            return word.Substring(0, 1).ToUpper() + word.Substring(1).ToLower();
        }

        string ChuanHoaTenChuongTrinh(string name)
        {
            if (name == null)
                return "";
            name = name.Replace("-", " - ");
            var lstWord = name.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            name = "";
            foreach (var word in lstWord)
            {
                if (name != "") name += " ";
                name += ChuanHoaTu(word);
            }
            return name;
        }
        /// <summary>
        /// Phần kiểm tra xem chương trình nào gần nhất với một mốc thời gian cho trước
        /// </summary>
        /// 
        private bool isNearest(HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM item, List<HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM> lstItem, DateTime compareTime)
        {
            List<TimeSpan> lstTimeSpan = new List<TimeSpan>();
            foreach (var i in lstItem)
            {
                if (i.StartTime < compareTime)
                    lstTimeSpan.Add(compareTime.Subtract(i.StartTime));
            }

            if (item.StartTime < compareTime && compareTime.Subtract(item.StartTime) == lstTimeSpan.Min())
                return true;

            return false;
        }
        private void ExportThread()
        {
            isBusy = true;
            if (OnBusyChanged != null)
                OnBusyChanged(this, new EventArgs());

            SqlConnection db = null;
            while (isRun)
            {
                try
                {
                    if (Owner.config.DBStationConnectionString == null || Owner.config.DBStationConnectionString == "")
                        throw new Exception("Không có chuỗi kết nối database");

                    if (Owner.config.VODFolder == null || Owner.config.VODFolder == "")
                        throw new Exception("Không có cấu hình thư mục VOD");
                    if (Owner.config.VODXmlFolder == null || Owner.config.VODXmlFolder == "")
                    {
                        throw new Exception("Không có cấu hình thư mục VODXml");
                    }
                    if (Owner.config.sessionStr == null || Owner.config.sessionStr == "")
                    {
                        throw new Exception("Không có thông tin kênh xuất Xml");
                    }
                    if (db == null)
                        db = new SqlConnection(Owner.config.DBStationConnectionString);

                    DateTime dateNow = DateTime.Now.AddDays(Owner.config.LastExportDay).AddHours(Owner.config.LastExportHour).Date;

                    var lstPlayList = db.Query<HDStation.ETERE_AS_RUN_LOG_PLAYLIST>(@"Select * From ETERE_AS_RUN_LOG_PLAYLIST Where DateList >= @DateNow And DateList<@DateNext"
                        , new { DateNow = dateNow, DateNext = dateNow.AddDays(2) }).ToList();

                    bool exportError = false;

                    List<string> childStrList = getChildString(Owner.config.sessionStr);
                    Dictionary<string, string> sessionDic = new Dictionary<string, string>();
                    foreach (var childStr in childStrList)
                    {
                        sessionDic.Add(getNumber(childStr), childStr.Replace(getNumber(childStr), "").Replace(" - ", ""));
                    }

                    foreach (var playlist in lstPlayList)
                    {
                        if (!isRun)
                            break;

                        try
                        {
                            Owner.SendLogToAll(ThreadName + " export play list " + playlist.DateList.ToString("yyyy-MM-dd") + " section " + playlist.SectionID);

                            var lstItem = db.Query<HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM>(@"Select * From ETERE_AS_RUN_LOG_PLAYLIST_ITEM Where ListID = @ListID",
                                new { ListID = playlist.ID }).OrderBy(i => i.StartTime).Where(f => f.StartTime >= new DateTime(playlist.DateList.Year, playlist.DateList.Month, playlist.DateList.Day, 0, 0, 0) && f.StartTime <= new DateTime(playlist.DateList.Year, playlist.DateList.Month, playlist.DateList.Day, 0, 0, 0).AddDays(1)).ToList();
                            Owner.SendLogToAll("Dang lay du lieu voi ID la " + playlist.ID + " tu ETERE");
                            Owner.SendLogToAll("List " + playlist.ID + " co " + lstItem.Count + " items tu ETERE_AS_RUN_LOG_PLAYLIST_ITEM");
                            if (!isRun)
                                break;
                            #region Phần xử lý lịch các kênh có ID 19, 20, 21, 23; đưa lịch từ 6h hôm trước đến 6h hôm sau sang chuẩn từ 0h đến 24h cùng ngày

                            if (playlist.SectionID.ToString() == "19" || playlist.SectionID.ToString() == "20" || playlist.SectionID.ToString() == "21" || playlist.SectionID.ToString() == "23")
                            {
                                try
                                {
                                    var lstPrePlayList = db.Query<HDStation.ETERE_AS_RUN_LOG_PLAYLIST>(@"Select * From ETERE_AS_RUN_LOG_PLAYLIST Where DateList >= @DateNow"
                                        , new { DateNow = dateNow.AddDays(-1) }).ToList();

                                    foreach (var prePlaylist in lstPrePlayList)
                                    {
                                        if (!isRun)
                                            break;
                                        if ((playlist.SectionID.ToString() == "19" && prePlaylist.SectionID.ToString() == "19") || (playlist.SectionID.ToString() == "20" && prePlaylist.SectionID.ToString() == "20") || (playlist.SectionID.ToString() == "21" && prePlaylist.SectionID.ToString() == "21") || (playlist.SectionID.ToString() == "23" && prePlaylist.SectionID.ToString() == "23"))
                                            try
                                            {
                                                var lstFullPreItem = db.Query<HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM>(@"Select * From ETERE_AS_RUN_LOG_PLAYLIST_ITEM Where ListID = @ListID",
                                                    new { ListID = prePlaylist.ID }).OrderBy(i => i.StartTime).ToList();

                                                var tempList = lstFullPreItem.Where(f => isNearest(f, lstFullPreItem, new DateTime(playlist.DateList.Year, playlist.DateList.Month,
                                                    playlist.DateList.Day, 0, 0, 0)) || (f.StartTime >= new DateTime(playlist.DateList.Year, playlist.DateList.Month,
                                                    playlist.DateList.Day, 0, 0, 0) && f.StartTime <= new DateTime(playlist.DateList.Year, playlist.DateList.Month, playlist.DateList.Day, 6, 0, 0))).ToList();

                                                var lstPreItem = tempList.OrderBy(i => i.StartTime).ToList();
                                                //var lstPreItem = db.Query<HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM>(@"Select * From ETERE_AS_RUN_LOG_PLAYLIST_ITEM Where ListID = @ListID",
                                                //    new { ListID = prePlaylist.ID }).OrderBy(i => i.StartTime).Where(f => f.StartTime >= new DateTime(playlist.DateList.Year, playlist.DateList.Month,
                                                //    playlist.DateList.Day, 0, 0, 0) && f.StartTime <= new DateTime(playlist.DateList.Year, playlist.DateList.Month, playlist.DateList.Day, 6, 0, 0)).ToList();
                                                if (!isRun)
                                                    break;
                                                lstPreItem.AddRange(lstItem);
                                                lstItem = lstPreItem;
                                                if (!isRun)
                                                    break;
                                            }
                                            catch { }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Owner.SendLogToAll(ex.ToString());
                                }
                            }

                            #endregion
                            string ids = "";
                            foreach (var id in lstItem.Select(i => i.AssetID).Distinct())
                            {
                                if (ids != "") ids += ",";
                                ids += "N'" + id.ToString() + "'";
                            }

                            List<HDStation.INFORTAPE> lstTape = new List<HDStation.INFORTAPE>();
                            if (ids != "")
                                lstTape = db.Query<HDStation.INFORTAPE>(@"Select * From INFORTAPE Where EXTERNAL_CODE in (" + ids + ")").ToList();

                            if (!isRun)
                                break;

                            Object.VODObject vod = new Object.VODObject();

                            List<Object.VODChildObject> lstVodChild = new List<Object.VODChildObject>();
                            List<Object.VODObject> lstVodEx = new List<Object.VODObject>();

                            TimeSpan totalTemp = lstItem.LastOrDefault().StartTime - lstItem[0].StartTime;

                            long totalDurationTemp = (long)totalTemp.TotalSeconds /*+ (long)(lstItem[0].Duration / 25) */+ (long)(lstItem[lstItem.Count - 1].Duration / 25) + 1;
                            List<DateTime> startList = new List<DateTime>();
                            List<TimeSpan> startTimeList = new List<TimeSpan>();

                            foreach (var item in lstItem)
                            {
                                var tape = lstTape.Where(t => t.EXTERNAL_CODE == item.AssetID.ToString()).FirstOrDefault();

                                Object.VODChildObject vodChild = new Object.VODChildObject();
                                vodChild.Description = tape == null ? "" : tape.NOI_DUNG;
                                vodChild.scheduleDate = item.StartTime.AddHours(-7);
                                vodChild.Title = tape == null ? item.Title : ChuanHoaTenChuongTrinh(tape.CommingNextNow);
                                vodChild.duration = Math.Abs((long)item.Duration / 25);
                                vodChild.startTime = item.StartTime.AddHours(-7).TimeOfDay;
                                vodChild.totalDuration = totalDurationTemp;

                                vodChild.PromoImages = "http://img.static.vtvcab.vn/tvshows/" + HDCore.Utils.ConvertToVietnameseNonSign(tape == null ? item.Title : ChuanHoaTenChuongTrinh(tape.CommingNextNow)).Replace(" ", "").Replace("-", "").Replace("0", "").Replace("1", "").Replace("2", "").Replace("3", "").Replace("4", "").Replace("5", "").Replace("6", "").Replace("7", "").Replace("8", "").Replace("9", "").Replace("_", "").ToLower() + ".jpg";
                                vodChild.serviceRef = "";
                                startList.Add(item.StartTime.Date.AddHours(-7));
                                startTimeList.Add(item.StartTime.AddHours(-7).TimeOfDay);
                                if (tape != null)
                                {
                                    int sub = -1;
                                    var index = HDCore.Utils.ConvertToVietnameseNonSign(vodChild.Title).ToLower().LastIndexOf("tap");
                                    if (index < 0)
                                    {
                                        index = HDCore.Utils.ConvertToVietnameseNonSign(vodChild.Title).ToLower().LastIndexOf("so");
                                        if (index >= 0)
                                            sub = 2;
                                    }
                                    else
                                        sub = 3;

                                    if (index >= 0)
                                    {
                                        var tapStr = vodChild.Title.Substring(index).Substring(sub).Trim();
                                        if (tapStr.IndexOf(' ') > 0)
                                            tapStr = tapStr.Substring(0, tapStr.IndexOf(' '));
                                        int tap = 0;
                                        if (int.TryParse(tapStr, out tap))
                                        {
                                            vodChild.Episode = tap;
                                        }
                                    }

                                    if (vodChild.Description == null || vodChild.Description == "")
                                    {
                                        index = HDCore.Utils.ConvertToVietnameseNonSign(vodChild.Title).ToLower().LastIndexOf("phan");
                                        if (index < 0)
                                            index = HDCore.Utils.ConvertToVietnameseNonSign(vodChild.Title).ToLower().LastIndexOf("tap");
                                        if (index < 0)
                                            index = HDCore.Utils.ConvertToVietnameseNonSign(vodChild.Title).ToLower().LastIndexOf("so");
                                        if (index >= 0)
                                        {
                                            vodChild.Description = vodChild.Title.Substring(index).Trim();
                                            if (tape.NOI_DUNG != null && tape.NOI_DUNG != "")
                                                vodChild.Description = tape.NOI_DUNG;
                                        }
                                    }
                                }

                                Object.VODObject vodEx = new Object.VODObject()
                                {
                                    scheduleDate = item.StartTime.Date,
                                    startTime = item.StartTime.TimeOfDay,
                                    Title = vodChild.Title,
                                    Description = vodChild.Description,
                                    PromoImages = vodChild.PromoImages,
                                    Episode = vodChild.Episode
                                };

                                if (sessionDic.ContainsKey(playlist.SectionID.ToString()))
                                {
                                    if (startList.Count > 0 && startTimeList.Count > 0)
                                    {
                                        if (vodChild.duration == 0)
                                        {
                                            TimeSpan tempTime = new TimeSpan(17, 0, 0);
                                            vodChild.duration = Math.Abs((long)tempTime.Subtract(vodChild.startTime).TotalSeconds);
                                        }
                                        vodChild.serviceRef = sessionDic[playlist.SectionID.ToString()].Replace("-", "");
                                        vodChild.PromoImages = vodChild.PromoImages.Trim().Replace("http://img.static.vtvcab.vn/tvshows/", "http://img.static.vtvcab.vn/tvshows/" + vodChild.serviceRef + "/");
                                        if (playlist.SectionID.ToString() == "19" || playlist.SectionID.ToString() == "20" || playlist.SectionID.ToString() == "21" || playlist.SectionID.ToString() == "23")
                                        {
                                            vodChild.scheduleDate = startList[0].AddDays(1);
                                            //vodChild.dateStart = item.StartTime.AddHours(-7).Date.AddDays(1);
                                        }
                                        else
                                        {
                                            vodChild.scheduleDate = startList[0];
                                        }
                                        vodChild.dateStart = item.StartTime.AddHours(-7).Date;
                                        vodChild.timeStart = startTimeList[0];

                                        if (lstVodChild.Count == 0 || vodChild.Title != lstVodChild[lstVodChild.Count - 1].Title)
                                        {
                                            lstVodChild.Add(vodChild);
                                        }
                                        else
                                        {
                                            lstVodChild[lstVodChild.Count - 1].duration += vodChild.duration;
                                        }

                                    }
                                }

                                if (lstVodEx.Count == 0 || vodEx.Title != lstVodEx[lstVodEx.Count - 1].Title)
                                    lstVodEx.Add(vodEx);
                            }
                            repVOD rep = new repVOD();
                            rep.DataSource = lstVodEx;
                            rep.CreateDocument();

                            string exportFile = Path.Combine(Owner.config.VODFolder, playlist.SectionID.ToString() + "_" + playlist.DateList.ToString("yyyyMMdd") + ".xlsx");

                            if (File.Exists(exportFile))
                                File.Delete(exportFile);

                            rep.ExportToXlsx(exportFile);

                            Owner.SendLogToAll(ThreadName + " export play list " + playlist.DateList.ToString("yyyy-MM-dd") + " section " + playlist.SectionID + " completed");
                            if (sessionDic.ContainsKey(playlist.SectionID.ToString()))
                            {
                                try
                                {
                                    vod.totalDuration = totalDurationTemp + lstVodChild[lstVodChild.Count - 1].duration;
                                    foreach (var vodItem in lstVodChild)
                                    {
                                        vod.GenerateXml(vodItem);
                                    }

                                    string xmlExportFile = Path.Combine(Owner.config.VODXmlFolder, playlist.SectionID.ToString() + "_" + playlist.DateList.ToString("yyyyMMdd") + ".xml");
                                    if (File.Exists(xmlExportFile))
                                        File.Delete(xmlExportFile);

                                    File.Create(xmlExportFile).Dispose();
                                    vod.SaveXmlFile(xmlExportFile);
                                }
                                catch
                                {
                                    Owner.SendLogToAll("Error when export XML for Section " + playlist.SectionID.ToString() + ".");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Owner.SendLogToAll(ThreadName + " export play list " + playlist.DateList.ToString("yyyy-MM-dd") + " section " + playlist.SectionID + " error:" + ex.ToString());
                            exportError = true;
                        }
                    }

                    if (!exportError)
                    {
                        for (int time = 0; isRun && time < Owner.config.ExportTime * 1000; time += 100)
                            Thread.Sleep(100);
                    }
                    else
                    {
                        for (int time = 0; isRun && time < 1000; time += 100)
                            Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    Owner.SendLogToAll(ThreadName + " error:" + ex.ToString());

                    for (int time = 0; isRun && time < 1000; time += 100)
                        Thread.Sleep(100);
                }
            }

            isBusy = false;
            if (OnBusyChanged != null)
                OnBusyChanged(this, new EventArgs());
        }
        private string getNumber(string str)
        {
            var number = Regex.Match(str, @"\d+").Value;

            return number;
        }
        private List<string> getChildString(string strParent, char keyChar = ',')
        {
            List<string> tempResult = new List<string>(100);
            string tempStr = "";
            int j = 0;
            for (var i = 0; i < strParent.Length; i++)
            {
                if (strParent[i] != keyChar)
                {
                    tempStr += strParent[i];
                }
                else
                {
                    tempResult.Add(tempStr.Replace(" ", ""));
                    tempStr = "";
                    j++;
                }

            }
            return tempResult;
        }
    }
}
