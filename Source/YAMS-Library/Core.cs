﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlServerCe;
using YAMS;

namespace YAMS
{
    public static class Core
    {
        public static string RootFolder = new System.IO.FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName;

        public static List<MCServer> Servers = new List<MCServer> { };

        public static void StartUp()
        {
            //Start DB Connection
            Database.init();
            Database.AddLog("Starting Up");

            //Is this the first run?
            if (Database.GetSetting("FirstRun", "YAMS") != "true") YAMS.Util.FirstRun();

            //Fill up some vars
            AutoUpdate.bolUpdateAddons = Convert.ToBoolean(Database.GetSetting("UpdateAddons", "YAMS"));
            AutoUpdate.bolUpdateGUI = Convert.ToBoolean(Database.GetSetting("UpdateGUI", "YAMS"));
            AutoUpdate.bolUpdateJAR = Convert.ToBoolean(Database.GetSetting("UpdateJAR", "YAMS"));
            AutoUpdate.bolUpdateSVC = Convert.ToBoolean(Database.GetSetting("bolUpdateSVC", "YAMS"));
            AutoUpdate.bolUpdateWeb = Convert.ToBoolean(Database.GetSetting("UpdateWeb", "YAMS"));

            //Check for updates
            AutoUpdate.CheckUpdates();

            //Load any servers
            SqlCeDataReader readerServers = YAMS.Database.GetServers();
            while (readerServers.Read())
            {
                MCServer myServer = new MCServer(Convert.ToInt32(readerServers["ServerID"]));
                if (Convert.ToBoolean(readerServers["ServerAutostart"])) myServer.Start();
                Servers.Add(myServer);
            }

            //Start Webserver
            WebServer.Init();
            WebServer.Start();

        }

        public static void ShutDown()
        {
            Core.Servers.ForEach(delegate(MCServer s)
            {
               s.Stop();
            });
            YAMS.Database.AddLog("Shutting Down");
        }
    }
}
