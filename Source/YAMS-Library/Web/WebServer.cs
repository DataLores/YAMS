using System;
using System.Collections.Generic;
using System.Data.SqlServerCe;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using Griffin.WebServer;
using Griffin.WebServer.Files;
using Griffin.WebServer.Modules;

namespace YAMS
{
    public static class WebServer
    {
        private static ModuleManager adminModuleManager;
        private static ModuleManager publicModuleManager;

        private static Thread adminServerThread;
        private static Thread publicServerThread;

        private static int AdminTryCount = 0;
        private static int PublicTryCount = 0;

        //Control
        public static void Init()
        {
            //See if there is a new version of the web files waiting before we start the server
            if (File.Exists(Core.RootFolder + @"\web.zip"))
            {
                if (Directory.Exists(Core.RootFolder + @"\web\")) Directory.Delete(Core.RootFolder + @"\web\", true);
                Directory.CreateDirectory(YAMS.Core.RootFolder + @"\web\");
                AutoUpdate.ExtractZip(YAMS.Core.RootFolder + @"\web.zip", YAMS.Core.RootFolder + @"\web\");
                File.Delete(Core.RootFolder + @"\web.zip");
            }

            //create module manager to add all modules to admin webserver
            adminModuleManager = new ModuleManager();

            //Handle the requests for static files
            var fileService = new DiskFileService("/assets/", YAMS.Core.RootFolder + "\\web\\assets\\");
            var assets = new FileModule(fileService) { AllowFileListing = false };
            adminModuleManager.Add(assets);
            
            //Handle requests to API
            adminModuleManager.Add(new Web.AdminAPI());
            adminServerThread = new Thread(new ThreadStart(StartAdmin));
            adminServerThread.Start();

            //Open firewall ports
            if (Database.GetSetting("EnableOpenFirewall", "YAMS") == "true")
            {
                Networking.OpenFirewallPort(Convert.ToInt32(YAMS.Database.GetSetting("AdminListenPort", "YAMS")), "Admin website");
            }

            if (Database.GetSetting("EnablePortForwarding", "YAMS") == "true")
            {
                Networking.OpenUPnP(Convert.ToInt32(YAMS.Database.GetSetting("AdminListenPort", "YAMS")), "Admin website", YAMS.Database.GetSetting("YAMSListenIP", "YAMS"));
            }

            if (Database.GetSetting("EnablePublicSite", "YAMS") == "true")
            {
                //Add any server specific folders
                publicModuleManager = new ModuleManager();
                publicModuleManager.Add(assets);

                SqlCeDataReader readerServers = YAMS.Database.GetServers();
                while (readerServers.Read())
                {
                    var intServerID = readerServers["ServerID"].ToString();
                    if (!Directory.Exists(Core.StoragePath + intServerID + "\\renders\\")) Directory.CreateDirectory(Core.StoragePath + intServerID + "\\renders\\");
                    publicModuleManager.Add(new FileModule(new DiskFileService("/servers/" + intServerID + "/renders/", Core.StoragePath + intServerID + "\\renders\\")));
                    if (!Directory.Exists(Core.StoragePath + intServerID + "\\backups\\")) Directory.CreateDirectory(Core.StoragePath + intServerID + "\\backups\\");
                    publicModuleManager.Add(new FileModule(new DiskFileService("/servers/" + intServerID + "/backups/", Core.StoragePath + intServerID + "\\backups\\")));
                }

                //Handle requests to API
                publicModuleManager.Add(new Web.PublicAPI());
                //publicServer.Add(HttpListener.Create(IPAddress.Any, Convert.ToInt32(YAMS.Database.GetSetting("PublicListenPort", "YAMS"))));

                publicServerThread = new Thread(new ThreadStart(StartPublic));
                publicServerThread.Start();

                //Open firewall ports
                if (Database.GetSetting("EnableOpenFirewall", "YAMS") == "true")
                {
                    Networking.OpenFirewallPort(Convert.ToInt32(YAMS.Database.GetSetting("PublicListenPort", "YAMS")), "Public website");
                }

                if (Database.GetSetting("EnablePortForwarding", "YAMS") == "true")
                {
                    Networking.OpenUPnP(Convert.ToInt32(YAMS.Database.GetSetting("PublicListenPort", "YAMS")), "Public website", YAMS.Database.GetSetting("YAMSListenIP", "YAMS"));
                }
            }
        }

        public static void StartAdmin()
        {
            try
            {
                while(Util.PortIsBusy(Convert.ToInt32(YAMS.Database.GetSetting("AdminListenPort", "YAMS"))) && AdminTryCount < 120) {
                    AdminTryCount++;
                    Database.AddLog("Admin Web server port still in use, attempt " + AdminTryCount, "web", "warn");
                    Thread.Sleep(5000);
                }

                //start new webserver for admin, session handling built-in
                var server = new Griffin.WebServer.HttpServer(adminModuleManager);
                server.Start(IPAddress.Any, Convert.ToInt32(YAMS.Database.GetSetting("AdminListenPort", "YAMS")));
            }
            catch (System.Net.Sockets.SocketException e)
            {
                //Previous service has not released the port, so hang on and try again.
                Database.AddLog("Admin Web server port still in use, attempt " + AdminTryCount + ": " + e.Message, "web", "warn");
            }
            catch (Exception e) {
                EventLog myLog = new EventLog();
                myLog.Source = "YAMS";
                myLog.WriteEntry("Exception: " + e.Data, EventLogEntryType.Error);
            }
            
        }

        public static void StartPublic()
        {
            try
            {
                while (Util.PortIsBusy(Convert.ToInt32(YAMS.Database.GetSetting("PublicListenPort", "YAMS"))) && PublicTryCount < 120)
                {
                    PublicTryCount++;
                    Database.AddLog("Public Web server port still in use, attempt " + PublicTryCount, "web", "warn");
                    Thread.Sleep(5000);
                }
                var server = new Griffin.WebServer.HttpServer(publicModuleManager);
                server.Start(IPAddress.Any, Convert.ToInt32(YAMS.Database.GetSetting("PublicListenPort", "YAMS")));
            }
            catch (System.Net.Sockets.SocketException e)
            {
                //Previous service has not released the port, so hang on and try again.
                Database.AddLog("Public Web server port still in use, attempt " + PublicTryCount + ": " + e.Message, "web", "warn");
            }
            catch (Exception e)
            {
                EventLog myLog = new EventLog();
                myLog.Source = "YAMS";
                myLog.WriteEntry("Exception: " + e.Data, EventLogEntryType.Error);
            }
        }

        public static void Stop()
        {
            //Close firewall ports and forward via UPnP
            if (Database.GetSetting("EnableOpenFirewall", "YAMS") == "true")
            {
                Networking.CloseFirewallPort(Convert.ToInt32(YAMS.Database.GetSetting("AdminListenPort", "YAMS")));
                Networking.CloseFirewallPort(Convert.ToInt32(YAMS.Database.GetSetting("PublicListenPort", "YAMS")));
            }
            if (Database.GetSetting("EnablePortForwarding", "YAMS") == "true")
            {
                Networking.CloseUPnP(Convert.ToInt32(YAMS.Database.GetSetting("AdminListenPort", "YAMS")));
                Networking.CloseUPnP(Convert.ToInt32(YAMS.Database.GetSetting("PublicListenPort", "YAMS")));
            }
            
            adminServerThread.Abort();
            if (publicServerThread != null) publicServerThread.Abort();
        }

    }

}
