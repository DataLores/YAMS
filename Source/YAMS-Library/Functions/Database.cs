﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlServerCe;
using System.IO;
using System.Text;

namespace YAMS
{
    public class Database
    {
        private static SqlCeConnection connLocal;

        private static DateTime defaultDateTime = new DateTime(1900, 1, 1);

        public static void init()
        {
            //Open our DB connection for use all over the place
            connLocal = GetConnection();
            connLocal.Open();
            UpdateDB();
        }

        private static SqlCeConnection GetConnection()
        {
            string dbfile = YAMS.Core.RootFolder + "\\db\\dbYAMS.sdf";
            SqlCeConnection connection = new SqlCeConnection("datasource=" + dbfile + ";max database size=2048");
            return connection;
        }

        public static DataSet ReturnLogRows(int intStartID = 0, int intNumRows = 0, string strLevels = "all", int intServerID = -1)
        {
            DataSet ds = new DataSet();
            SqlCeCommand command = connLocal.CreateCommand();

            //We need to limit the number of rows or requests take an age and crash browsers
            if (intNumRows == 0) intNumRows = 1000;

            //Build our SQL
            StringBuilder strSQL = new StringBuilder();
            strSQL.Append("SELECT ");
            if (intNumRows > 0) strSQL.Append("TOP(" + intNumRows.ToString() + ") ");
            strSQL.Append("* FROM Log ");
            strSQL.Append("WHERE 1=1 ");
            if (intStartID > 0) strSQL.Append("AND LogID > " + intStartID.ToString() + " ");
            if (strLevels != "all") strSQL.Append("AND LogLevel = '" + strLevels + "' ");
            if (intServerID > -1) strSQL.Append("AND ServerID = " + intServerID.ToString() + " ");
            strSQL.Append("ORDER BY LogDateTime DESC, LogID ASC");

            command.CommandText = strSQL.ToString();
            SqlCeDataAdapter adapter = new SqlCeDataAdapter(command);
            adapter.Fill(ds);
            return ds;
        }

        public static DataSet ReturnSettings()
        {
            DataSet ds = new DataSet();
            SqlCeCommand command = connLocal.CreateCommand();

            command.CommandText = "SELECT * FROM YAMSSettings";
            SqlCeDataAdapter adapter = new SqlCeDataAdapter(command);
            adapter.Fill(ds);
            return ds;
        }

        public static void AddLog(string strMessage, string strSource = "app", string strLevel = "info", bool bolSendToAdmin = false, int intServerID = 0)
        {
            if (strMessage == null) strMessage = "Null message received";

            string sqlIns = "INSERT INTO Log (LogSource, LogMessage, LogLevel, ServerID) VALUES (@source, @msg, @level, @serverid)";
            try
            {
                if (strMessage.Length > 255) strMessage = strMessage.Substring(0, 255);
                SqlCeCommand cmdIns = new SqlCeCommand(sqlIns, connLocal);
                cmdIns.Parameters.Add("@source", strSource);
                cmdIns.Parameters.Add("@msg", Util.Left(strMessage, 255));
                cmdIns.Parameters.Add("@level", strLevel);
                cmdIns.Parameters.Add("@serverid", intServerID);
                cmdIns.ExecuteNonQuery();
                cmdIns.Dispose();
                cmdIns = null;
                
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString(), ex);
            }
        }
        public static void AddLog(DateTime datTimeStamp, string strMessage, string strSource = "app", string strLevel = "info", bool bolSendToAdmin = false, int intServerID = 0)
        {
            if (strMessage == null) strMessage = "Null message received";

            string sqlIns = "INSERT INTO Log (LogSource, LogMessage, LogLevel, ServerID, LogDateTime) VALUES (@source, @msg, @level, @serverid, @timestamp)";
            try
            {
                SqlCeCommand cmdIns = new SqlCeCommand(sqlIns, connLocal);
                cmdIns.Parameters.Add("@source", strSource);
                cmdIns.Parameters.Add("@msg", Util.Left(strMessage, 255));
                cmdIns.Parameters.Add("@level", strLevel);
                cmdIns.Parameters.Add("@serverid", intServerID);
                cmdIns.Parameters.Add("@timestamp", datTimeStamp);

                cmdIns.ExecuteNonQuery();
                cmdIns.Dispose();
                cmdIns = null;

            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString(), ex);
            }
        }

        // Returns the stored etag for the specified URL or blank string if no etag saved
        public static string GetEtag(string strURL)
        {
            try
            {
                SqlCeCommand cmd = new SqlCeCommand("SELECT VersionETag FROM FileVersions WHERE VersionURL = @url", connLocal);
                cmd.Parameters.Add("@url", strURL);
                string eTag = (string)cmd.ExecuteScalar();
                return eTag;
            }
            catch (Exception ex)
            {
                AddLog("YAMS.Database.GetEtag Exception: " + ex.Message, "database", "error");
                return "";
            }
        }

        //Sets the Etag for a URL, replacing or adding the URL as needed
        public static bool SaveEtag(string strUrl, string strEtag)
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;
            if (GetEtag(strUrl) == null)
            {
                //Doesn't exist in DB already, so insert
                cmd.CommandText = "INSERT INTO FileVersions (VersionURL, VersionETag) VALUES (@url, @etag);";
            }
            else
            {
                //Exists, so need to update
                cmd.CommandText = "UPDATE FileVersions SET VersionETag=@etag WHERE VersionURL=@url;";
            }
            cmd.Parameters.Add("@url", strUrl);
            cmd.Parameters.Add("@etag", strEtag);
            cmd.ExecuteNonQuery();
            return true;
        }

        //Builds the server.properties file from current settings
        public static void BuildServerProperties(int intServerID)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("#Minecraft server properties: built by YAMS\n");
            //sb.Append("#Sun Nov 28 19:26:26 GMT 2010\n");

            SqlCeCommand comProperties = new SqlCeCommand("SELECT * FROM MCSettings WHERE ServerID = @serverid", connLocal);
            comProperties.Parameters.Add("@serverid", intServerID);
            SqlCeDataReader readerProperties = null;
            readerProperties = comProperties.ExecuteReader();
            while (readerProperties.Read())
            {
                sb.Append(readerProperties["SettingName"].ToString() + "=" + readerProperties["SettingValue"].ToString() + "\n");
            }

            //Save it as our update file in case the current is in use
            string strFile = @"\server.properties";
            if (File.Exists(Core.StoragePath + intServerID.ToString() + strFile)) strFile = @"\server.properties.UPDATE";
            File.WriteAllText(Core.StoragePath + intServerID.ToString() + strFile, sb.ToString());
        }

        //Get and set settings
        public static bool SaveSetting(string strSettingName, string strSettingValue)
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;

            if (GetSetting(strSettingName, "YAMS") == null)
            {
                //Doesn't exist in DB already, so insert
                cmd.CommandText = "INSERT INTO YAMSSettings (SettingName, SettingValue) VALUES (@name, @value);";
            }
            else
            {
                //Exists, so need to update
                cmd.CommandText = "UPDATE YAMSSettings SET SettingValue=@value WHERE SettingName=@name;";
            }
            cmd.Parameters.Add("@name", strSettingName);
            cmd.Parameters.Add("@value", strSettingValue);
            cmd.ExecuteNonQuery();
            return true;
        }
        public static bool SaveSetting(int intServerID, string strSettingName, string strSettingValue)
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;

            if (GetSetting(strSettingName, "MC", intServerID) == null)
            {
                //Doesn't exist in DB already, so insert
                cmd.CommandText = "INSERT INTO MCSettings (SettingName, SettingValue, ServerID) VALUES (@name, @value, @id);";
            }
            else
            {
                //Exists, so need to update
                cmd.CommandText = "UPDATE MCSettings SET SettingValue=@value WHERE SettingName=@name AND ServerID=@id;";
            }
            cmd.Parameters.Add("@name", strSettingName);
            cmd.Parameters.Add("@value", strSettingValue);
            cmd.Parameters.Add("@id", intServerID);
            cmd.ExecuteNonQuery();
            return true;
        }

        public static string GetSetting(string strSettingName, string strType, int intServerID = 0)
        {
            String strTableName = "";
            String strServerIDQuery = "";

            switch (strType)
            {
                case "YAMS":
                    strTableName = "YAMSSettings";
                    break;
                case "MC":
                    strTableName = "MCSettings";
                    if (intServerID != 0) strServerIDQuery = " and ServerID = @id";
                    break;
            }

            try
            {
                SqlCeCommand cmd = new SqlCeCommand("SELECT SettingValue FROM " + strTableName + " WHERE SettingName = @name" + strServerIDQuery, connLocal);
                cmd.Parameters.Add("@name", strSettingName);
                if (intServerID != 0) cmd.Parameters.Add("@id", intServerID);
                string strSettingValue = (string)cmd.ExecuteScalar();
                return strSettingValue;
            }
            catch (Exception ex)
            {
                AddLog("YAMS.Database.GetSetting Exception: " + ex.Message, "database", "error");
                return "";
            }
        }

        public static object GetSetting(int intServerID, string strSettingName)
        {

            try
            {
                SqlCeCommand cmd = new SqlCeCommand("SELECT " + strSettingName + " FROM MCServers WHERE ServerID = @id", connLocal);
                cmd.Parameters.Add("@id", intServerID);
                var strSettingValue = cmd.ExecuteScalar();
                return strSettingValue;
            }
            catch (Exception ex)
            {
                AddLog("YAMS.Database.GetSetting Exception: " + ex.Message, "database", "error");
                return "";
            }
        }


        public static int NewServer(List<KeyValuePair<string, string>> listServer, string strServerTitle, int intServerMemory = 1024)
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;

            //Create the server and get an ID
            cmd.CommandText = "INSERT INTO MCServers (ServerTitle, ServerWrapperMode, ServerAssignedMemory) VALUES (@title, 0, @mem)";
            cmd.Parameters.Add("@title", strServerTitle);
            cmd.Parameters.Add("@mem", intServerMemory);
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT @@IDENTITY";
            int intNewID = Convert.ToInt32(cmd.ExecuteScalar());

            //Insert the settings into the DB for this server
            foreach (var element in listServer)
            {
                cmd.Parameters.Clear();
                cmd.CommandText = "INSERT INTO MCSettings (ServerID, SettingName, SettingValue) VALUES (@id, @name, @value);";
                cmd.Parameters.Add("@id", intNewID);
                cmd.Parameters.Add("@name", element.Key);
                cmd.Parameters.Add("@value", element.Value);
                cmd.ExecuteNonQuery();
            }

            //Set up Files + Folders
            if (!Directory.Exists(Core.StoragePath + intNewID.ToString())) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString());
            //if (!Directory.Exists(Core.StoragePath + intNewID.ToString() + @"\config\")) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString() + @"\config\");
            if (!Directory.Exists(Core.StoragePath + intNewID.ToString() + @"\world\")) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString() + @"\world\");
            if (!Directory.Exists(Core.StoragePath + intNewID.ToString() + @"\renders\")) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString() + @"\renders\");
            if (!Directory.Exists(Core.StoragePath + intNewID.ToString() + @"\renders\overviewer\")) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString() + @"\renders\overviewer\");
            if (!Directory.Exists(Core.StoragePath + intNewID.ToString() + @"\renders\overviewer\output\")) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString() + @"\renders\overviewer\output\");
            if (!File.Exists(Core.StoragePath + intNewID.ToString() + @"\banned-ips.txt")) File.Create(Core.StoragePath + intNewID.ToString() + @"\banned-ips.txt");
            if (!File.Exists(Core.StoragePath + intNewID.ToString() + @"\banned-players.txt")) File.Create(Core.StoragePath + intNewID.ToString() + @"\banned-players.txt");
            if (!File.Exists(Core.StoragePath + intNewID.ToString() + @"\ops.txt")) File.Create(Core.StoragePath + intNewID.ToString() + @"\ops.txt");
            if (!File.Exists(Core.StoragePath + intNewID.ToString() + @"\white-list.txt")) File.Create(Core.StoragePath + intNewID.ToString() + @"\white-list.txt");

            //Create default config files
            BuildServerProperties(intNewID);

            return intNewID;
        }

        public static void DeleteServer(int intServerID)
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;
            cmd.CommandText = "DELETE FROM MCServers WHERE ServerID = @id;";
            cmd.Parameters.Add("@id", intServerID);

            cmd.ExecuteNonQuery();
        }

        public static int NewServerWeb(List<KeyValuePair<string, string>> listServer, string strServerTitle, int intServerMemory = 1024)
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;

            //Create the server and get an ID
            cmd.CommandText = "INSERT INTO MCServers (ServerTitle, ServerWrapperMode, ServerAssignedMemory, ServerAutostart) VALUES (@title, 0, @mem, 0)";
            cmd.Parameters.Add("@title", strServerTitle);
            cmd.Parameters.Add("@mem", intServerMemory);
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT @@IDENTITY";
            int intNewID = Convert.ToInt32(cmd.ExecuteScalar());

            //Set up Files + Folders
            if (!Directory.Exists(Core.StoragePath + intNewID.ToString())) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString());
            //if (!Directory.Exists(Core.StoragePath + intNewID.ToString() + @"\config\")) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString() + @"\config\");
            if (!Directory.Exists(Core.StoragePath + intNewID.ToString() + @"\world\")) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString() + @"\world\");
            if (!Directory.Exists(Core.StoragePath + intNewID.ToString() + @"\backups\")) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString() + @"\backups\");
            if (!Directory.Exists(Core.StoragePath + intNewID.ToString() + @"\renders\")) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString() + @"\renders\");
            if (!Directory.Exists(Core.StoragePath + intNewID.ToString() + @"\renders\overviewer\")) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString() + @"\renders\overviewer\");
            if (!Directory.Exists(Core.StoragePath + intNewID.ToString() + @"\renders\overviewer\output\")) Directory.CreateDirectory(Core.StoragePath + intNewID.ToString() + @"\renders\overviewer\output\");
            if (!File.Exists(Core.StoragePath + intNewID.ToString() + @"\banned-ips.txt")) File.Create(Core.StoragePath + intNewID.ToString() + @"\banned-ips.txt");
            if (!File.Exists(Core.StoragePath + intNewID.ToString() + @"\banned-players.txt")) File.Create(Core.StoragePath + intNewID.ToString() + @"\banned-players.txt");
            if (!File.Exists(Core.StoragePath + intNewID.ToString() + @"\ops.txt")) File.Create(Core.StoragePath + intNewID.ToString() + @"\ops.txt");
            if (!File.Exists(Core.StoragePath + intNewID.ToString() + @"\white-list.txt")) File.Create(Core.StoragePath + intNewID.ToString() + @"\white-list.txt");

            //Insert the settings into the DB for this server
            foreach (var element in listServer)
            {
                cmd.Parameters.Clear();
                cmd.CommandText = "INSERT INTO MCSettings (ServerID, SettingName, SettingValue) VALUES (@id, @name, @value);";
                cmd.Parameters.Add("@id", intNewID);
                cmd.Parameters.Add("@name", element.Key);
                cmd.Parameters.Add("@value", element.Value);
                cmd.ExecuteNonQuery();
            }

            //Create default config files
            BuildServerProperties(intNewID);

            //Add the server to the collection
            MCServer myServer = new MCServer(intNewID);
            Core.Servers.Add(intNewID, myServer);

            return intNewID;
        }

        public static bool UpdateServer(int intServerID, string strSettingName, object strSettingValue)
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;

            cmd.CommandText = "UPDATE MCServers SET " + strSettingName + "=@value WHERE ServerID=@id;";
            cmd.Parameters.Add("@value", strSettingValue);
            cmd.Parameters.Add("@id", intServerID);
            cmd.ExecuteNonQuery();
            return true;
        }

        public static void UpdateDB()
        {
            switch (Convert.ToInt32(GetSetting("DBSchema", "YAMS")))
            {
                case 1:
                    //Update from Schema 1
                    Database.SaveSetting("StoragePath", Core.RootFolder + @"\servers\");
                    Database.SaveSetting("DBSchema", "2");
                    goto case 2;
                case 2:
                    //Update from Schema 2
                    Database.SaveSetting("UsageData", "true");
                    Database.SaveSetting("DBSchema", "3");
                    goto case 3;
                case 3:
                    Database.SaveSetting("EnablePortForwarding", "true");
                    Database.SaveSetting("EnableOpenFirewall", "true");
                    Database.SaveSetting("YAMSListenIP", Networking.GetListenIP().ToString());
                    AddJob("update", -1, 0, "", 0);
                    AddJob("backup", -1, 30, "", 1);
                    Database.SaveSetting("DBSchema", "4");
                    goto case 4;
                    //goto case 3; //etc
                case 4:
                    Database.SaveSetting("DNSName", "");
                    Database.SaveSetting("DNSSecret", "");
                    Database.SaveSetting("LastExternalIP", "");
                    Database.SaveSetting("DBSchema", "5");
                    goto case 5;
                case 5:
                    Database.SaveSetting("EnablePublicSite", "true");
                    Database.SaveSetting("DBSchema", "6");
                    goto case 6;
                case 6:
                    Database.SaveSetting("EnableTelnet", "false");
                    Database.SaveSetting("TelnetPort", "56553");
                    Database.SaveSetting("DBSchema", "7");
                    goto case 7;
                case 7:
                    Database.SaveSetting("BukkitBetaInstalled", "false");
                    Database.SaveSetting("BukkitDevInstalled", "false");
                    Database.SaveSetting("DBSchema", "8");
                    goto case 8;
                case 8:
                    Database.ExecuteSQL("ALTER TABLE MCServers ADD COLUMN ServerCustomJAR ntext");
                    Database.ExecuteSQL("ALTER TABLE MCServers ADD COLUMN ServerWebBody ntext");
                    Database.SaveSetting("DBSchema", "9");
                    goto case 9;
                case 9:
                    break;
                default:
                    break;
            }

        }

        public static SqlCeDataReader GetServers()
        {
            SqlCeCommand comServers = new SqlCeCommand("SELECT * FROM MCServers", connLocal);
            SqlCeDataReader readerServers = null;
            readerServers = comServers.ExecuteReader();
            return readerServers;
        }

        //User Functions
        public static bool AddUser(string strUsername, int intServerID, string strLevel = "guest")
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;
            cmd.CommandText = "INSERT INTO Players (PlayerName, PlayerServer, PlayerLevel) VALUES (@name, @server, @level);";
            cmd.Parameters.Add("@name", strUsername);
            cmd.Parameters.Add("@server", intServerID);
            cmd.Parameters.Add("@level", strLevel);
            cmd.ExecuteNonQuery();
            return true;
        }

        public static string GetPlayerLevel(string strName, int intServerID)
        {
            try
            {
                SqlCeCommand cmd = new SqlCeCommand("SELECT PlayerLevel FROM Players WHERE PlayerName = @name AND PlayerServer = @id", connLocal);
                cmd.Parameters.Add("@id", intServerID);
                cmd.Parameters.Add("@name", strName);
                var strSettingValue = (string)cmd.ExecuteScalar();
                return strSettingValue;
            }
            catch (Exception ex)
            {
                AddLog("YAMS.Database.GetPlayerLevel Exception: " + ex.Message, "database", "error");
                return "";
            }
        }

        public static int GetPlayerCount(int intServerID)
        {
            try
            {
                SqlCeCommand cmd = new SqlCeCommand("SELECT COUNT(PlayerID) AS Counter FROM Players WHERE PlayerServer = @id", connLocal);
                cmd.Parameters.Add("@id", intServerID);
                var intSettingValue = (int)cmd.ExecuteScalar();
                return intSettingValue;
            }
            catch (Exception ex)
            {
                AddLog("YAMS.Database.GetPlayerCount Exception: " + ex.Message, "database", "error");
                return 0;
            }
        }

        public static DataSet GetPlayers(int intServerID)
        {
            DataSet ds = new DataSet();
            SqlCeCommand comPlayers = new SqlCeCommand("SELECT * FROM Players WHERE PlayerServer = @id", connLocal);
            comPlayers.Parameters.Add("@id", intServerID);
            SqlCeDataAdapter adapter = new SqlCeDataAdapter(comPlayers);
            adapter.Fill(ds);
            return ds;
        }

        //Job Engine
        public static SqlCeDataReader GetJobs(int intHour, int intMinute)
        {
            SqlCeCommand cmd = new SqlCeCommand("SELECT * FROM Jobs WHERE (JobHour = -1 AND JobMinute = @minute) OR (JobHour = @hour AND JobMinute = @minute)", connLocal);
            cmd.Parameters.Add("@minute", intMinute);
            cmd.Parameters.Add("@hour", intHour);
            SqlCeDataReader readerJobs = null;
            readerJobs = cmd.ExecuteReader();
            return readerJobs;
        }

        public static bool AddJob(string strAction, int intHour, int intMinute, string strParams, int intServerID)
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;
            cmd.CommandText = "INSERT INTO Jobs (JobAction, JobHour, JobMinute, JobParams, JobServer) VALUES (@action, @hour, @minute, @params, @server);";
            cmd.Parameters.Add("@action", strAction);
            cmd.Parameters.Add("@hour", intHour);
            cmd.Parameters.Add("@minute", intMinute);
            cmd.Parameters.Add("@params", strParams);
            cmd.Parameters.Add("@server", intServerID);
            cmd.ExecuteNonQuery();
            return true;
        }

        public static DataSet ListJobs()
        {
            DataSet ds = new DataSet();
            SqlCeCommand cmd = new SqlCeCommand("SELECT Jobs.*, MCServers.ServerTitle FROM Jobs LEFT JOIN MCServers ON Jobs.JobServer = MCServers.ServerID", connLocal);
            SqlCeDataAdapter adapter = new SqlCeDataAdapter(cmd);
            adapter.Fill(ds);
            return ds;
        }

        public static void DeleteJob(string strJobID)
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;
            cmd.CommandText = "DELETE FROM Jobs WHERE JobID = @jobid;";
            cmd.Parameters.Add("@jobid", Convert.ToInt32(strJobID));

            cmd.ExecuteNonQuery();
        }

        public static void ClearLogs(string strPeriod, int intAmount)
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;
            cmd.CommandText = "DELETE FROM Log WHERE LogDateTime < DATEADD(" + strPeriod + ", -" + intAmount + ", GETDATE());";
            cmd.ExecuteNonQuery();
        }

        public static void ExecuteSQL(string strSQL)
        {
            SqlCeCommand cmd = new SqlCeCommand();
            cmd.Connection = connLocal;
            cmd.CommandText = strSQL;
            cmd.ExecuteNonQuery();
        }

        ~Database()
        {
            connLocal.Close();
        }

    }
}
