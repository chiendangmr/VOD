using HDCPProtocol2;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using Dapper;
using System.IO;
using HDCore;

namespace VODServiceBase
{
    public class ImportAsRunLog : IThreadClass
    {
        MainClass Owner = null;
        public ImportAsRunLog(MainClass owner)
        {
            this.Owner = owner;
        }

        /// <summary>
        /// Tên tiến trình
        /// </summary>
        public string ThreadName { get { return "Import as run log"; } }

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

                Thread thr = new Thread(ImportThread);
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

        private void ImportThread()
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

                    if (Owner.config.AsRunLogFolder == null || Owner.config.AsRunLogFolder == "")
                        throw new Exception("Không có cấu hình thư mục as run log");

                    if (db == null)
                        db = new SqlConnection(Owner.config.DBStationConnectionString);

                    var lstFile = Directory.GetFiles(Owner.config.AsRunLogFolder, "*.xml");
                    bool exportError = false;
                    if (lstFile.Length > 0)
                    {
                        foreach (var file in lstFile)
                        {
                            if (!isRun)
                                break;

                            try
                            {
                                Owner.SendLogToAll(ThreadName + " import file " + file);

                                string processFile = file + ".running";
                                if (File.Exists(processFile))
                                    File.Delete(processFile);
                                File.Move(file, processFile);
                                var asRunLog = Utils.GetObject<Object.EtereAsRunLog.AsRunLogFile>(processFile);

                                if (asRunLog == null)
                                    throw new Exception("File không đúng chuẩn");

                                List<HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM> lstItem = new List<HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM>();
                                for (int i = 0; i < asRunLog.PlayList.Items.Length; i++)
                                {
                                    if (!isRun)
                                        break;

                                    Object.EtereAsRunLog.PlayListItem currentItem = asRunLog.PlayList.Items[i];
                                    Object.EtereAsRunLog.PlayListItem nextItem = null;
                                    if (i < asRunLog.PlayList.Items.Length - 1)
                                        nextItem = asRunLog.PlayList.Items[i + 1];
                                    lstItem.Add(new HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM()
                                    {
                                        StartTime = asRunLog.PlayList.DateList.AddMilliseconds(currentItem.TimeIn * 40),
                                        Duration = nextItem != null ? nextItem.TimeIn - currentItem.TimeIn : 0,
                                        AssetID = currentItem.AssetID,
                                        Type = currentItem.TypeName,
                                        Title = currentItem.ProgramName,                                        
                                    });
                                }

                                if (!isRun)
                                    break;

                                var adTypes = (Owner.config.AdTypes != null ? Owner.config.AdTypes : "")
                                    .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(s => s.Trim().ToLower()).ToList();

                                for (int i = 0; i < lstItem.Count; i++)
                                {
                                    if (!isRun)
                                        break;

                                    HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM currentItem = lstItem[i];

                                    if (currentItem.Duration <= Owner.config.MaxAdDuration || (currentItem.Type != null && adTypes.Contains(currentItem.Type.ToLower())))
                                    {
                                        lstItem.RemoveAt(i);
                                        i--;
                                        continue;
                                    }

                                    HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM lastItem = null;
                                    int lastItemIndex = 0;
                                    for (int j = i - 1; j >= 0; j--)
                                    {
                                        var item = lstItem[j];
                                        if (item.AssetID == currentItem.AssetID || item.Title.Trim().ToLower() == currentItem.Title.Trim().ToLower())
                                        {
                                            lastItem = currentItem;
                                            lastItemIndex = j;
                                            break;
                                        }
                                        else if (item.Duration > Owner.config.MaxAdDuration && item.Type != null && !adTypes.Contains(item.Type.ToLower()))
                                        {
                                            break;
                                        }
                                    }
                                    if (lastItem != null)
                                    {
                                        for (int n = lastItemIndex + 1; n <= i; n++)
                                            lstItem.RemoveAt(lastItemIndex + 1);
                                        i = lastItemIndex;
                                    }
                                }

                                if (!isRun)
                                    break;

                                for (int i = 0; i < lstItem.Count; i++)
                                {
                                    if (!isRun)
                                        break;

                                    HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM currentItem = lstItem[i];
                                    HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM nextItem = null;
                                    if (i < lstItem.Count - 1)
                                        nextItem = lstItem[i + 1];
                                    currentItem.Duration = nextItem != null ? (long)(nextItem.StartTime - currentItem.StartTime).TotalMilliseconds / 40 : 0;
                                }

                                if (!isRun)
                                    break;

                                var playListDB = db.Query<HDStation.ETERE_AS_RUN_LOG_PLAYLIST>(@"Select * From ETERE_AS_RUN_LOG_PLAYLIST
                                    Where SectionID = @SectionID and DateList = @DateList"
                                    , new
                                    {
                                        SectionID = asRunLog.PlayList.StationId,
                                        DateList = asRunLog.PlayList.DateList
                                    }).FirstOrDefault();

                                if (!isRun)
                                    break;

                                if (playListDB == null)
                                {
                                    playListDB = new HDStation.ETERE_AS_RUN_LOG_PLAYLIST()
                                    {
                                        SectionID = asRunLog.PlayList.StationId,
                                        DateList = asRunLog.PlayList.DateList
                                    };
                                    playListDB.ID = db.Query<int>(@"Insert Into ETERE_AS_RUN_LOG_PLAYLIST(SectionID, DateList)
                                        Values(@SectionID, @DateList)

                                        Select convert(int, SCOPE_IDENTITY())", playListDB).First();
                                }

                                if (!isRun)
                                    break;

                                var lstItemDB = db.Query<HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM>(@"Select *
                                    From ETERE_AS_RUN_LOG_PLAYLIST_ITEM
                                    Where ListID = @ListID"
                                    , new { ListID = playListDB.ID }).ToList();

                                if (!isRun)
                                    break;

                                List<HDStation.ETERE_AS_RUN_LOG_PLAYLIST_ITEM> lstRemove =
                                    lstItemDB.Where(itmOld => lstItem.Where(itmNew => itmNew.StartTime == itmOld.StartTime).FirstOrDefault() == null)
                                    .ToList();

                                if (!isRun)
                                    break;

                                if (lstRemove.Count > 0)
                                {
                                    Owner.SendLogToAll("Xóa " + lstRemove.Count + " item");

                                    string ids = "";
                                    foreach (var item in lstRemove)
                                    {
                                        if (ids != "") ids += ",";
                                        ids += item.ID.ToString();
                                    }
                                    db.Execute(@"Delete From ETERE_AS_RUN_LOG_PLAYLIST_ITEM Where ID in(" + ids + ")");
                                }

                                if (!isRun)
                                    break;

                                foreach (var item in lstItem)
                                {
                                    if (!isRun)
                                        break;

                                    var itemOld = lstItemDB.Where(itm => itm.StartTime == item.StartTime).FirstOrDefault();
                                    if (itemOld == null
                                        || itemOld.Duration != item.Duration
                                        || itemOld.AssetID != item.AssetID
                                        || itemOld.Type != item.Type
                                        || itemOld.Title != item.Title)
                                    {
                                        item.ListID = playListDB.ID;

                                        if (itemOld == null)
                                        {
                                            db.Execute(@"Insert Into ETERE_AS_RUN_LOG_PLAYLIST_ITEM(ListID, StartTime, Duration, AssetID, Type, Title)
                                                Values(@ListID, @StartTime, @Duration, @AssetID, @Type, @Title)", item);
                                        }
                                        else
                                        {
                                            item.ID = itemOld.ID;

                                            db.Execute(@"Update ETERE_AS_RUN_LOG_PLAYLIST_ITEM
                                                Set ListID = @ListID, StartTime = @StartTime, Duration = @Duration
                                                    , AssetID = @AssetID, Type = @Type, Title = @Title
                                                Where ID = @ID", item);
                                        }
                                    }
                                }

                                Owner.SendLogToAll(ThreadName + " import file " + file + "completed");

                                string doneFile = file + ".done";
                                if (File.Exists(doneFile))
                                    File.Delete(doneFile);
                                File.Move(processFile, doneFile);
                            }
                            catch (Exception ex)
                            {
                                Owner.SendLogToAll(ThreadName + " import file " + file + " error:" + ex.Message);
                                exportError = true;
                            }
                        }
                    }

                    if (!exportError)
                    {
                        for (int time = 0; isRun && time < Owner.config.ImportTime * 1000; time += 100)
                            Thread.Sleep(100);
                    }
                    else
                    {
                        for (int time = 0; isRun && time < 1000; time += 100)
                            Thread.Sleep(100);
                    }
                }
                catch(Exception ex)
                {
                    Owner.SendLogToAll(ThreadName + " error:" + ex.Message);

                    for (int time = 0; isRun && time < 1000; time += 100)
                        Thread.Sleep(100);
                }
            }

            isBusy = false;
            if (OnBusyChanged != null)
                OnBusyChanged(this, new EventArgs());
        }
    }
}