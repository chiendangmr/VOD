using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using HDCPProtocol2;
using HDCPProtocol2.HDCP;
using Dapper;
using HDCore;
using System.IO;
using HDVietNamService;

namespace VODServiceBase
{
    public class MainClass : IMainService
    {
        public event EventHandler<MessageLogArgs> MessageLog;

        private MainService mainService;

        public string configPath = "";
        public Config config = null;

        public void AddLog(string mess)
        {
            mainService.AddLog(mess);
        }

        public void SendTo(HDCPParserArgs command, HDNetWork.HDTCPRemoteHostState state = null)
        {
            if (state == null)
                SendToAll(command);
            else
                mainService.SendTo(command, state);
        }

        public void SendToAll(HDCPParserArgs command)
        {
            mainService.SendToAll(command);
        }

        public void SendLogToAll(string mess)
        {
            mainService.SendLogToAll(mess);
        }

        EmailThread thrEmail;
        public MainClass()
        {
            mainService = new MainService(this);
            mainService.ServiceName = "Sync VOD";
            mainService.MessageLog += mainService_MessageLog;
            mainService.OnGetConfig += mainService_OnGetConfig;
            mainService.OnSetConfig += mainService_OnSetConfig;

            #region Lấy cấu hình
            var location = System.Reflection.Assembly.GetEntryAssembly().Location;
            var directoryPath = Path.GetDirectoryName(location);

            config = new Config();
            configPath = Path.Combine(directoryPath, "VODConfig.xml");
            if (File.Exists(configPath))
                try
                {
                    config = Utils.GetObject<Config>(configPath);
                }
                catch (Exception ex)
                {
                    AddLog("Lỗi đọc cấu hình:" + ex.Message);
                }
            #endregion

            thrEmail = new EmailThread(this);
            thrEmail.OnBusyChanged += thread_OnBusyChanged;

            mainService.AddThread(new ServiceInfo(this));

            mainService.AddThread(new ImportAsRunLog(this));

            mainService.AddThread(new ExportVOD(this));

            Begin();
        }

        public void AddEmail(string mess, string title = "")
        {
            thrEmail.AddEmail(mess, title);
        }

        void thread_OnBusyChanged(object sender, EventArgs e)
        {
            if (OnBusyChanged != null)
                OnBusyChanged(this, new EventArgs());
        }

        public string ServiceName
        {
            get { return mainService.ServiceName; }
        }

        public int ServicePort
        {
            get { return mainService.ServicePort; }
        }

        public void Begin()
        {
            mainService.Start();
        }

        public void End()
        {
            mainService.Stop();
        }

        void mainService_MessageLog(object sender, MessageLogArgs e)
        {
            if (MessageLog != null)
                MessageLog(this, e);
        }

        public bool IsBusy()
        {
            bool busy = thrEmail.IsBusy();
            return busy;
        }

        public event EventHandler OnBusyChanged;

        private bool isRun = false;
        public void Start()
        {
            if (!IsBusy())
            {
                isRun = true;

                thrEmail.Start();

                SendLogToAll("Bắt đầu dịch vụ");
                AddEmail("Bắt đầu dịch vụ");
            }
        }

        public void Restart()
        {
            bool needRestart = isRun;

            Stop();

            while (IsBusy())
                Thread.Sleep(100);

            if (needRestart)
            {
                SendLogToAll("Khởi động lại dịch vụ");
                AddEmail("Khởi động lại dịch vụ");
                Start();
            }
        }

        public void Stop()
        {
            if (isRun)
            {
                SendLogToAll("Dừng dịch vụ");
                AddEmail("Dừng dịch vụ");
            }
            isRun = false;
            thrEmail.Stop();
        }

        const long configDBStationID = 1;
        const long configAsRunLogFolderID = 2;
        const long configVODFolderID = 3;
        const long configImportTimeID = 4;
        const long configExportTimeID = 5;
        const long configEmailID = 6;
        const long configAdTypeID = 7;
        const long configAdMaxDurationID = 8;
        const long configLastExportDay = 9;
        const long configLastExportHour = 10;
        const long configVODXmlFolderID = 11;
        const long configSessionStrID = 12;

        void mainService_OnGetConfig(object sender, HDCPProtocol2.HDCP.HDCPParserArgs e)
        {
            try
            {
                HDCPParserArgs Command = new HDCPParserArgs()
                {
                    RemoteHostState = e.RemoteHostState,
                    Command = HDCPCommand.CONFIG,
                    MessageData = new MessageData()
                };

                FieldComponent component = new FieldComponent();

                #region Config
                component.AddComponent(new FieldOne()
                {
                    ID = configDBStationID,
                    DisplayText = "Chuỗi kết nối DB Station",
                    Value = config.DBStationConnectionString
                });

                component.AddComponent(new FieldOne()
                {
                    ID = configAsRunLogFolderID,
                    DisplayText = "As run log xml folder",
                    Value = config.AsRunLogFolder
                });

                component.AddComponent(new FieldOne()
                {
                    ID = configAdTypeID,
                    DisplayText = "Thể loại của quảng cáo(mỗi thể loại cách nhau bằng dấu ;)",
                    Value = config.AdTypes
                });

                component.AddComponent(new FieldOne()
                {
                    ID = configAdMaxDurationID,
                    DisplayText = "Thời lượng tối đa của quảng cáo(frame)",
                    Type = FieldType.Long,
                    EditMark = "n0",
                    Value = config.MaxAdDuration
                });

                component.AddComponent(new FieldOne()
                {
                    ID = configVODFolderID,
                    DisplayText = "VOD folder",
                    Value = config.VODFolder
                });

                component.AddComponent(new FieldOne()
                {
                    ID = configVODXmlFolderID,
                    DisplayText = "VOD XML Folder",
                    Value = config.VODXmlFolder
                });

                component.AddComponent(new FieldOne()
                {
                    ID = configSessionStrID,
                    DisplayText ="Session String",
                    Value = config.sessionStr
                });

                component.AddComponent(new FieldOne()
                {
                    ID = configImportTimeID,
                    DisplayText = "Thời gian quét As run log(giây)",
                    Type = FieldType.Int,
                    EditMark = "n0",
                    MinValue = 10,
                    MaxValue = 36000,
                    Value = config.ImportTime
                });

                component.AddComponent(new FieldOne()
                {
                    ID = configExportTimeID,
                    DisplayText = "Thời gian xuất VOD(giây)",
                    Type = FieldType.Int,
                    EditMark = "n0",
                    MinValue = 10,
                    MaxValue = 36000,
                    Value = config.ExportTime
                });

                component.AddComponent(new FieldOne()
                {
                    ID = configLastExportDay,
                    DisplayText = "Ngày cuối cùng xuất VOD",
                    Type = FieldType.Int,
                    EditMark = "f0",
                    MinValue = -10,
                    MaxValue = 0,
                    Value = config.LastExportDay
                });

                component.AddComponent(new FieldOne()
                {
                    ID = configLastExportHour,
                    DisplayText = "Giờ cuối cùng xuất VOD",
                    Type = FieldType.Int,
                    EditMark = "f0",
                    MinValue = -23,
                    MaxValue = 0,
                    Value = config.LastExportHour
                });

                component.AddComponent(new FieldOne()
                {
                    ID = configEmailID,
                    DisplayText = "Email",
                    Value = config.EmailSend
                });
                #endregion

                Command.MessageData.AddComponent(HDCPCommand.CONFIG.ToString(), component.ToXml());

                SendTo(Command, e.RemoteHostState);
            }
            catch (Exception ex)
            {
                SendLogToAll("Lỗi gửi cấu hình:" + ex.Message);

                HDCPParserArgs Command = new HDCPParserArgs()
                {
                    RemoteHostState = e.RemoteHostState,
                    Command = HDCPCommand.CONFIG,
                    MessageData = new MessageData()
                };
                SendTo(Command, e.RemoteHostState);
            }
        }

        void mainService_OnSetConfig(object sender, HDCPProtocol2.HDCP.HDCPParserArgs e)
        {
            try
            {
                var configPro = e.MessageData.Components.Where(p => p.Name == HDCPCommand.SET_CONFIG.ToString()).FirstOrDefault();
                if (configPro != null)
                {
                    FieldComponent component = new FieldComponent();
                    component.FromXml(configPro.Value);

                    bool needRestart = false;

                    #region Config
                    var proDBStation = component.Components.Where(c => c.ID == configDBStationID).FirstOrDefault() as FieldOne;
                    if (proDBStation != null && proDBStation.Value != null && proDBStation.Value.ToString() != config.DBStationConnectionString)
                    {
                        config.DBStationConnectionString = proDBStation.Value.ToString();
                        needRestart = true;
                    }

                    var proAsRunLogFolder = component.Components.Where(c => c.ID == configAsRunLogFolderID).FirstOrDefault() as FieldOne;
                    if (proAsRunLogFolder != null)
                        config.AsRunLogFolder = proAsRunLogFolder.Value as string;

                    var proAdTypes = component.Components.Where(c => c.ID == configAdTypeID).FirstOrDefault() as FieldOne;
                    if (proAdTypes != null)
                        config.AdTypes = proAdTypes.Value as string;

                    var proAdMaxDuration = component.Components.Where(c => c.ID == configAdMaxDurationID).FirstOrDefault() as FieldOne;
                    if (proAdMaxDuration != null)
                        config.MaxAdDuration = (long)proAdMaxDuration.Value;

                    var proVODFolder = component.Components.Where(c => c.ID == configVODFolderID).FirstOrDefault() as FieldOne;
                    if (proVODFolder != null)
                        config.VODFolder = proVODFolder.Value as string;

                    var proVODXmlFolder = component.Components.Where(c => c.ID == configVODXmlFolderID).FirstOrDefault() as FieldOne;
                    if (proVODXmlFolder != null)
                        config.VODXmlFolder = proVODXmlFolder.Value as string;

                    var proSessionStr = component.Components.Where(c => c.ID == configSessionStrID).FirstOrDefault() as FieldOne;
                    if (proSessionStr != null)
                        config.sessionStr = proSessionStr.Value as string;

                    var proImportTime = component.Components.Where(c => c.ID == configImportTimeID).FirstOrDefault() as FieldOne;
                    if (proImportTime != null)
                        config.ImportTime = (int)proImportTime.Value;

                    var proExportTime = component.Components.Where(c => c.ID == configExportTimeID).FirstOrDefault() as FieldOne;
                    if (proExportTime != null)
                        config.ExportTime = (int)proExportTime.Value;

                    var proLastExportDay = component.Components.Where(c => c.ID == configLastExportDay).FirstOrDefault() as FieldOne;
                    if (proLastExportDay != null)
                        config.LastExportDay = (int)proLastExportDay.Value;

                    var proLastExportHour = component.Components.Where(c => c.ID == configLastExportHour).FirstOrDefault() as FieldOne;
                    if (proLastExportHour != null)
                        config.LastExportHour = (int)proLastExportHour.Value;

                    var proEmail = component.Components.Where(c => c.ID == configEmailID).FirstOrDefault() as FieldOne;
                    if (proEmail != null)
                        config.EmailSend = proEmail.Value as string;

                    config.SaveObject(configPath);
                    #endregion

                    if (needRestart)
                        mainService.Restart();

                    SendLogToAll("Đặt cấu hình thành công");
                }
            }
            catch (Exception ex)
            {
                SendLogToAll("Lỗi đặt cấu hình:" + ex.Message);
            }
        }
    }
}
