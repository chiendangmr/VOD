using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using HDCPProtocol2;
using Dapper;
using HDCore;
using VODServiceBase;

namespace HDVietNamService
{
    public class ServiceInfo : IThreadClass
    {
        MainClass Owner = null;
        public ServiceInfo(MainClass owner)
        {
            this.Owner = owner;
        }

        /// <summary>
        /// Tên tiến trình
        /// </summary>
        public string ThreadName { get { return "Service Info"; } }

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

                Thread thr = new Thread(UpdateThread);
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

        private void UpdateThread()
        {
            isBusy = true;
            if (OnBusyChanged != null)
                OnBusyChanged(this, new EventArgs());

            SqlConnection db = null;
            while (isRun)
            {
                try
                {
                    if (db == null)
                    {
                        if (Owner.config.DBStationConnectionString != null && Owner.config.DBStationConnectionString != "")
                            db = new SqlConnection(Owner.config.DBStationConnectionString);
                    }

                    if (db != null)
                    {
                        db.Execute(@"If Not Exists(Select ID From Service Where Name = @Name and IP = @IP and Port = @Port)
                            Insert Into Service(Name, IP, Port, LastTime)
                            Values(@Name, @IP, @Port, getdate())
                        Else
                            Update Service Set LastTime = getdate()
                            Where Name = @Name and IP = @IP and Port = @Port",
                             new
                             {
                                 Name = Owner.ServiceName,
                                 IP = Utils.GetCurrentIP(),
                                 Port = Owner.ServicePort
                             });
                    }
                }
                catch { }

                for (int time = 0; isRun && time < 10000; time += 100)
                    Thread.Sleep(100);
            }

            isBusy = false;
            if (OnBusyChanged != null)
                OnBusyChanged(this, new EventArgs());
        }
    }
}
