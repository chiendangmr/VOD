using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using HDCore;
using HDCPProtocol2;
using Dapper;
using VODServiceBase;

namespace HDVietNamService
{
    public class EmailThread : IThreadClass
    {
        private MainClass Owner;
        public EmailThread(MainClass owner)
        {
            this.Owner = owner;
        }

        /// <summary>
        /// Tên tiến trình
        /// </summary>
        public string ThreadName { get { return "Send Email"; } }

        bool isBusy = false;
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

        bool isRun = false;
        /// <summary>
        /// Bắt đầu tiến trình
        /// </summary>
        public void Start()
        {
            if (!isBusy)
            {
                isRun = true;

                Thread thrEmail = new Thread(SendEmail);
                thrEmail.IsBackground = true;
                thrEmail.Start();
            }
        }

        /// <summary>
        /// Khởi động lại tiến trình
        /// </summary>
        public void Restart()
        {
            bool needRestart = isRun;

            if (isBusy)
            {
                this.Stop();
                while (isBusy)
                    Thread.Sleep(100);
            }

            if (needRestart)
                this.Start();
        }

        /// <summary>
        /// Dừng tiến trình
        /// </summary>
        public void Stop()
        {
            isRun = false;

            Monitor.Enter(lockMail);
            Monitor.PulseAll(lockMail);
            Monitor.Exit(lockMail);
        }

        /// <summary>
        /// Dừng tiến trình khi kết thúc
        /// </summary>
        public void StopWhenFinish()
        {
            Stop();
        }

        /// <summary>
        /// Sự kiện khi tiến trình bắt đầu hoặc dừng
        /// </summary>
        public event EventHandler OnBusyChanged;

        private Queue<HDStation.SYSTEM_MAIL> lstMail = new Queue<HDStation.SYSTEM_MAIL>();
        object lockMail = new object();
        public void AddEmail(string mess, string title = "")
        {
            try
            {
                if (Owner.config.EmailSend != null && Owner.config.EmailSend != "")
                {
                    mess = "[" + DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss") + "]:" + mess + " (Máy " + Utils.GetCurrentIP() + ")";
                    Monitor.Enter(lockMail);
                    foreach (var mail in Owner.config.EmailSend.Split(';'))
                        lstMail.Enqueue(new HDStation.SYSTEM_MAIL()
                        {
                            TARGET_MAIL = mail.Trim(),
                            CONTENT = mess,
                            DATECREATE = DateTime.Now,
                            TITLE = Owner.ServiceName + (title == "" ? "" : "_" + title)
                        });
                    Monitor.PulseAll(lockMail);
                    Monitor.Exit(lockMail);
                }
            }
            catch { }
        }

        private void SendEmail()
        {
            isBusy = true;
            if (OnBusyChanged != null)
                OnBusyChanged(this, new EventArgs());

            SqlConnection dbEmail = null;

            #region Thread
            while (isRun)
            {
                try
                {
                    HDStation.SYSTEM_MAIL firstMail = null;
                    Monitor.Enter(lockMail);
                    while (isRun && lstMail.Count <= 0)
                        Monitor.Wait(lockMail);
                    if (isRun)
                        firstMail = lstMail.Dequeue();
                    Monitor.Exit(lockMail);

                    if (firstMail != null)
                    {
                        if (dbEmail == null && Owner.config.DBStationConnectionString != null && Owner.config.DBStationConnectionString != "")
                            dbEmail = new SqlConnection(Owner.config.DBStationConnectionString);

                        if (dbEmail != null)
                        {
                            try
                            {
                                string mess = firstMail.CONTENT;
                                while (mess.Length > 0)
                                {
                                    string mess1 = "";
                                    if (mess.Length > 2000)
                                    {
                                        mess1 = mess.Substring(0, 2000);
                                        mess = mess.Substring(2000);
                                    }
                                    else
                                    {
                                        mess1 = mess;
                                        mess = "";
                                    }

                                    dbEmail.Execute(@"Insert Into SYSTEM_MAIL(TARGET_MAIL, CONTENT, DATECREATE, TITLE)
                                        Values(@TARGET_MAIL, @CONTENT, @DATECREATE, @TITLE)",
                                        new
                                        {
                                            TARGET_MAIL = firstMail.TARGET_MAIL,
                                            CONTENT = mess1,
                                            DATECREATE = firstMail.DATECREATE,
                                            TITLE = firstMail.TITLE
                                        });
                                }
                            }
                            catch(Exception ex)
                            {
                                Owner.SendLogToAll("Email thread error:" + ex.Message);
                                if (!isRun)
                                    break;

                                Thread.Sleep(100);
                            }
                        }
                    }
                }
                catch { }
            }
            #endregion

            isBusy = false;
            if (OnBusyChanged != null)
                OnBusyChanged(this, new EventArgs());
        }
    }
}
