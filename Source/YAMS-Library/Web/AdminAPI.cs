using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
using Griffin.WebServer;
using Griffin.WebServer.Files;
using Griffin.WebServer.Modules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;


namespace YAMS.Web
{
    public class AdminAPI : IWorkerModule
    {
        private CustomPrincipal customPrincipal;

        public void BeginRequest(IHttpContext context)
        {

        }

        public void EndRequest(IHttpContext context)
        {

        }

        public void HandleRequestAsync(IHttpContext context, Action<IAsyncModuleResult> callback)
        {
            // Since this module only supports sync
            callback(new AsyncModuleResult(context, HandleRequest(context)));
        }

        public ModuleResult HandleRequest(IHttpContext context)
        {
            int intServerID = 0;
            MCServer s;
            string json;
            JObject jProps;
            var ip = context.Request.RemoteEndPoint as System.Net.IPEndPoint;

            if (context.Request.Uri.AbsoluteUri.Contains(@"/api/"))
            {
                //must be authenticated

                if (context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) 
                    && customPrincipal.Identity.Name.Equals("admin") 
                    && customPrincipal.Identity.IPAddress.Equals(ip.Address.ToString()))
                {
                    String strResponse = "";
                    Griffin.Net.Protocols.Http.IParameterCollection param = context.Request.Form;
                    //what is the action?
                    switch (context.Request.Form["action"])
                    {
                        case "log":
                            //grabs lines from the log.
                            int intStartID = Convert.ToInt32(context.Request.Form["start"]);
                            int intNumRows = Convert.ToInt32(context.Request.Form["rows"]);
                            int intServer = Convert.ToInt32(context.Request.Form["serverid"]);
                            string strLevel = context.Request.Form["level"];

                            DataSet ds = Database.ReturnLogRows(intStartID, intNumRows, strLevel, intServer);

                            strResponse = JsonConvert.SerializeObject(ds, Formatting.Indented);
                            break;
                        case "list":
                            //List available servers
                            strResponse = "{ \"servers\" : [";
                            foreach (KeyValuePair<int, MCServer> kvp in Core.Servers)
                            {
                                strResponse += "{ \"id\" : " + kvp.Value.ServerID + ", " +
                                                 "\"title\" : \"" + kvp.Value.ServerTitle + "\", " +
                                                 "\"ver\" : \"" + kvp.Value.ServerVersion + "\" } ,";
                            };
                            strResponse = strResponse.Remove(strResponse.Length - 1);
                            strResponse += "]}";
                            break;
                        case "status":
                            //Get status of a server
                            s = Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])];
                            strResponse = "{ \"serverid\" : " + s.ServerID + "," +
                                            "\"status\" : \"" + s.Running + "\"," +
                                            "\"ram\" : " + s.GetMemory() + "," +
                                            "\"vm\" : " + s.GetVMemory() + "," +
                                            "\"restartneeded\" : \"" + s.RestartNeeded + "\"," +
                                            "\"restartwhenfree\" : \"" + s.RestartWhenFree + "\"," +
                                            "\"gamemode\" : \"" + s.GameMode + "\"," +
                                            "\"players\" : [";
                            if (s.Players.Count > 0)
                            {
                                foreach (KeyValuePair<string, Player> kvp in s.Players)
                                {
                                    Vector playerPos = kvp.Value.Position;
                                    strResponse += " { \"name\": \"" + kvp.Value.Username + "\", " +
                                                      "\"level\": \"" + kvp.Value.Level + "\", " +
                                                      "\"x\": \"" + playerPos.x.ToString("0.##") + "\", " +
                                                      "\"y\": \"" + playerPos.y.ToString("0.##") + "\", " +
                                                      "\"z\": \"" + playerPos.z.ToString("0.##") + "\" },";
                                };
                                strResponse = strResponse.Remove(strResponse.Length - 1);
                            }
                            strResponse += "]}";
                            break;
                        case "get-players":
                            DataSet dsPlayers = Database.GetPlayers(Convert.ToInt32(context.Request.Form["serverid"]));
                            JsonConvert.SerializeObject(dsPlayers, Formatting.Indented);
                            break;
                        case "overviewer":
                            //Maps a server
                            s = Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])];
                            string strRenderModes = "";
                            if (param["normal"] == "true") strRenderModes += "normal";
                            if (param["lighting"] == "true")
                            {
                                if (strRenderModes != "") strRenderModes += ",";
                                strRenderModes += "lighting";
                            }
                            if (param["night"] == "true")
                            {
                                if (strRenderModes != "") strRenderModes += ",";
                                strRenderModes += "night";
                            }
                            if (param["spawn"] == "true")
                            {
                                if (strRenderModes != "") strRenderModes += ",";
                                strRenderModes += "spawn";
                            }
                            if (param["cave"] == "true")
                            {
                                if (strRenderModes != "") strRenderModes += ",";
                                strRenderModes += "cave";
                            }
                            AddOns.Overviewer over = new AddOns.Overviewer(s, "rendermodes=" + strRenderModes);
                            over.Start();
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        case "c10t":
                            //Images a server
                            s = Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])];
                            AddOns.c10t c10t = new AddOns.c10t(s, "night=" + param["night"] + "&mode=" + param["mode"]);
                            c10t.Start();
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        case "tectonicus":
                            //Maps a server
                            s = Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])];
                            AddOns.Tectonicus tecton = new AddOns.Tectonicus(s, "lighting=" + param["lighting"] + "&night=" + param["night"] + "&delete=" + param["delete"]);
                            tecton.Start();
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        case "start":
                            //Starts a server
                            Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].Start();
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        case "stop":
                            //Stops a server
                            Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].Stop();
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        case "forcestop":
                            //Force stops a server
                            Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].ForceStop();
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        case "restart":
                            //Restarts a server
                            Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].Restart();
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        case "delayed-restart":
                            //Restarts a server after a specified time and warns players
                            Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].DelayedRestart(Convert.ToInt32(param["delay"]));
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        case "restart-when-free":
                            Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].RestartIfEmpty();
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        case "command":
                            //Sends literal command to a server
                            Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].Send(context.Request.Form["message"]);
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        case "get-yams-settings":
                            DataSet dsSettings = Database.ReturnSettings();
                            JsonConvert.SerializeObject(dsSettings, Formatting.Indented);
                            break;
                        case "save-yams-settings":
                            //Settings update
                            foreach (Griffin.Net.Protocols.Http.Messages.Parameter p in param)
                            {
                                if (p.Name != "action") Database.SaveSetting(p.Name, p.Value);
                            }
                            break;
                        case "get-server-settings":
                            //retrieve all server settings as JSON
                            List<string> listIPsMC = new List<string>();
                            IPHostEntry ipListenMC = Dns.GetHostEntry("");
                            foreach (IPAddress ipaddress in ipListenMC.AddressList)
                            {
                                if (ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) listIPsMC.Add(ipaddress.ToString());
                            }

                            intServerID = Convert.ToInt32(param["serverid"]);
                            strResponse = "{ \"serverid\" : " + intServerID + "," +
                                              "\"title\" : \"" + Database.GetSetting(intServerID, "ServerTitle") + "\"," +
                                              "\"optimisations\" : \"" + Database.GetSetting(intServerID, "ServerEnableOptimisations") + "\"," +
                                              "\"memory\" : \"" + Database.GetSetting(intServerID, "ServerAssignedMemory") + "\"," +
                                              "\"autostart\" : \"" + Database.GetSetting(intServerID, "ServerAutoStart") + "\"," +
                                              "\"type\" : \"" + Database.GetSetting(intServerID, "ServerType") + "\"," +
                                              "\"custom\" : \"" + Database.GetSetting(intServerID, "ServerCustomJAR").ToString().Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"," +
                                              "\"motd\" : \"" + Database.GetSetting("motd", "MC", intServerID) + "\"," +
                                              "\"listen\" : \"" + Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].GetProperty("server-ip") + "\"," +
                                              "\"port\" : \"" + Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].GetProperty("server-port") + "\"," +
                                              "\"IPs\": " + JsonConvert.SerializeObject(listIPsMC, Formatting.None);
                            strResponse += "}";
                            break;
                        case "get-server-connections":
                            intServerID = Convert.ToInt32(param["serverid"]);
                            strResponse = "{ \"dnsname\" : \"" + Database.GetSetting("DNSName", "YAMS") + "\", " +
                                            "\"externalip\" : \"" + Networking.GetExternalIP().ToString() + "\", " +
                                            "\"internalip\" : \"" + Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].GetProperty("server-ip") + "\", " +
                                            "\"mcport\" : " + Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].GetProperty("server-port") + ", " +
                                            "\"publicport\" : " + Database.GetSetting("PublicListenPort", "YAMS") + ", " +
                                            "\"message\" : " + JsonConvert.SerializeObject(Database.GetSetting(intServerID, "ServerWebBody"), Formatting.None) + "}";
                            break;
                        case "save-website-settings":
                            intServerID = Convert.ToInt32(param["serverid"]);
                            Database.UpdateServer(intServerID, "ServerWebBody", param["message"]);
                            break;
                        case "get-mc-settings":
                            //retrieve all server settings as JSON
                            intServerID = Convert.ToInt32(param["serverid"]);
                            
                            json = File.ReadAllText(YAMS.Core.RootFolder + @"\lib\properties.json");
                            jProps = JObject.Parse(json);

                            strResponse = "";
                            
                            foreach(JObject option in jProps["options"]) {
                                strResponse += "<p><label for=\"" + (string)option["key"] + "\" title=\"" + (string)option["description"] + "\">" + (string)option["name"] + "</label>";

                                string strValue = Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].GetProperty((string)option["key"]);

                                switch ((string)option["type"])
                                {
                                    case "string":
                                        strResponse += "<input type=\"text\" name=\"" + (string)option["key"] + "\" value=\"" + strValue + "\" />";
                                        break;
                                    case "boolean":
                                        strResponse += "<select name=\"" + (string)option["key"] + "\">";
                                        strResponse += "<option value=\"true\"";
                                        if (strValue == "true") strResponse += " selected";
                                        strResponse += ">True</option>";
                                        strResponse += "<option value=\"false\"";
                                        if (strValue == "false") strResponse += " selected";
                                        strResponse += ">False</option>";
                                        strResponse += "</select>";
                                        break;
                                    case "integer":
                                        strResponse += "<select name=\"" + (string)option["key"] + "\">";
                                        int intValue = Convert.ToInt32(strValue);
                                        for (var i = Convert.ToInt32((string)option["min"]); i <= Convert.ToInt32((string)option["max"]); i++)
                                        {
                                            strResponse += "<option value=\"" + i.ToString() + "\"";
                                            if (intValue == i) strResponse += " selected";
                                            strResponse += ">" + i.ToString() + "</option>";
                                        }
                                        strResponse += "</select>";
                                        break;
                                    case "array":
                                        strResponse += "<select name=\"" + (string)option["key"] + "\">";
                                        string strValues = (string)option["values"];
                                        string[] elements = strValues.Split(',');
                                        foreach (string values in elements)
                                        {
                                            string[] options = values.Split('|');
                                            strResponse += "<option value=\"" + options[0] + "\"";
                                            if (strValue == options[0]) strResponse += " selected";
                                            strResponse += ">" + options[1] + "</option>";
                                        }
                                        strResponse += "</select>";
                                        break;
                                }

                                strResponse += "</p>";
                            }

                            break;
                        case "save-server-settings":
                            intServerID = Convert.ToInt32(param["serverid"]);
                            Database.UpdateServer(intServerID, "ServerTitle", param["title"]);
                            Database.UpdateServer(intServerID, "ServerType", param["type"]);
                            Database.UpdateServer(intServerID, "ServerCustomJAR", param["custom"]);
                            Database.UpdateServer(intServerID, "ServerAssignedMemory", Convert.ToInt32(param["memory"]));
                            if (param["optimisations"] == "true") Database.UpdateServer(intServerID, "ServerEnableOptimisations", true);
                            else Database.UpdateServer(intServerID, "ServerEnableOptimisations", false);
                            if (param["autostart"] == "true") Database.UpdateServer(intServerID, "ServerAutoStart", true);
                            else Database.UpdateServer(intServerID, "ServerAutoStart", false);
                            Database.SaveSetting(intServerID, "motd", param["message"]);

                            //Save the server's MC settings
                            MCServer thisServer = Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])];
                            thisServer.SaveProperty("server-ip", param["cfg_listen-ip"]);
                            thisServer.SaveProperty("server-port", param["cfg_port"]);

                            json = File.ReadAllText(YAMS.Core.RootFolder + @"\lib\properties.json");
                            jProps = JObject.Parse(json);

                            strResponse = "";

                            foreach (JObject option in jProps["options"])
                            {
                                thisServer.SaveProperty((string)option["key"], param[(string)option["key"]]);
                            }

                            //If they've selected a Bukkit but not chosen to have them installed, do it for them
                            if (param["type"] == "bukkit")
                            {
                                if (Database.GetSetting("BukkitInstalled", "YAMS") == "false")
                                {
                                    Database.SaveSetting("BukkitInstalled", "true");
                                    AutoUpdate.CheckUpdates();
                                }
                            } else if (param["type"] == "bukkit-beta")
                            {
                                if (Database.GetSetting("BukkitBetaInstalled", "YAMS") == "false")
                                {
                                    Database.SaveSetting("BukkitBetaInstalled", "true");
                                    AutoUpdate.CheckUpdates();
                                }
                            } else if(param["type"] == "bukkit-dev")
                            {
                                if (Database.GetSetting("BukkitDevInstalled", "YAMS") == "false")
                                {
                                    Database.SaveSetting("BukkitDevInstalled", "true");
                                    AutoUpdate.CheckUpdates();
                                }
                            }

                            if (thisServer.Running) thisServer.RestartIfEmpty();

                            break;
                        case "get-config-file":
                            List<string> listConfig = Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].ReadConfig(param["file"]);
                            strResponse = JsonConvert.SerializeObject(listConfig, Formatting.Indented);
                            break;
                        case "get-server-whitelist":
                            strResponse = "{ \"enabled\" : " + Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].GetProperty("white-list") + " }";
                            break;
                        case "upload-world":
                            var test = context.Request.Files["new-world"];
                            break;
                        case "delete-world":
                            bool bolRandomSeed = false;
                            if (param["randomseed"] == "true") bolRandomSeed = true;
                            Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].ClearWorld(bolRandomSeed);
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        case "remove-server":
                            Core.Servers[Convert.ToInt32(context.Request.Form["serverid"])].Stop();
                            Core.Servers.Remove(Convert.ToInt32(context.Request.Form["serverid"]));
                            Database.DeleteServer(Convert.ToInt32(context.Request.Form["serverid"]));
                            strResponse = "{ \"result\" : \"removed\" }";
                            break;
                        case "about":
                            Dictionary<string, string> dicAbout = new Dictionary<string, string> {
                                { "dll" , FileVersionInfo.GetVersionInfo(Path.Combine(Core.RootFolder, "YAMS-Library.dll")).FileVersion },
                                { "svc" , FileVersionInfo.GetVersionInfo(Path.Combine(Core.RootFolder, "YAMS-Service.exe")).FileVersion },
                                { "gui" , FileVersionInfo.GetVersionInfo(Path.Combine(Core.RootFolder, "YAMS-Updater.exe")).FileVersion },
                                { "db" , Database.GetSetting("DBSchema", "YAMS") }
                            };
                            strResponse = JsonConvert.SerializeObject(dicAbout, Formatting.Indented);
                            break;
                        case "installed-apps":
                            Dictionary<string, string> dicApps = new Dictionary<string, string> {
                                { "bukkit" , Database.GetSetting("BukkitInstalled", "YAMS") },
                                { "bukkitBeta" , Database.GetSetting("BukkitBetaInstalled", "YAMS") },
                                { "bukkitDev" , Database.GetSetting("BukkitDevInstalled", "YAMS") },
                                { "overviewer" , Database.GetSetting("OverviewerInstalled", "YAMS") },
                                { "c10t" , Database.GetSetting("C10tInstalled", "YAMS") },
                                { "biomeextractor" , Database.GetSetting("BiomeExtractorInstalled", "YAMS") },
                                { "tectonicus" , Database.GetSetting("TectonicusInstalled", "YAMS") },
                                { "nbtoolkit" , Database.GetSetting("NBToolkitInstalled", "YAMS") }
                            };
                            strResponse = JsonConvert.SerializeObject(dicApps, Formatting.Indented);
                            break;
                        case "update-apps":
                            Database.SaveSetting("OverviewerInstalled", param["overviewer"]);
                            Database.SaveSetting("C10tInstalled", param["c10t"]);
                            Database.SaveSetting("BiomeExtractorInstalled", param["biomeextractor"]);
                            Database.SaveSetting("BukkitInstalled", param["bukkit"]);
                            Database.SaveSetting("BukkitBetaInstalled", param["bukkitBeta"]);
                            Database.SaveSetting("BukkitDevInstalled", param["bukkitDev"]);
                            strResponse = "done";
                            break;
                        case "force-autoupdate":
                            AutoUpdate.CheckUpdates(false, true);
                            break;
                        case "network-settings":
                            List<string> listIPs = new List<string>();
                            IPHostEntry ipListen = Dns.GetHostEntry("");
                            foreach (IPAddress ipaddress in ipListen.AddressList)
                            {
                                if (ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) listIPs.Add(ipaddress.ToString());
                            }

                            Dictionary<string, string> dicNetwork = new Dictionary<string, string> {
                                { "portForwarding" , Database.GetSetting("EnablePortForwarding", "YAMS") },
                                { "openFirewall" , Database.GetSetting("EnableOpenFirewall", "YAMS") },
                                { "publicEnable" , Database.GetSetting("EnablePublicSite", "YAMS") },
                                { "adminPort" , Database.GetSetting("AdminListenPort", "YAMS") },
                                { "publicPort" , Database.GetSetting("PublicListenPort", "YAMS") },
                                { "currentIP" , Database.GetSetting("YAMSListenIP", "YAMS") },
                                { "telnetEnable" , Database.GetSetting("EnableTelnet", "YAMS") },
                                { "telnetPort" , Database.GetSetting("TelnetPort", "YAMS") },
                                { "IPs" , JsonConvert.SerializeObject(listIPs, Formatting.None) }
                            };
                            strResponse = JsonConvert.SerializeObject(dicNetwork, Formatting.Indented).Replace(@"\","").Replace("\"[", "[").Replace("]\"", "]");
                            break;
                        case "save-network-settings":
                            int intTester = 0;
                            try
                            {
                                //Try to convert to integers to make sure something silly isn't put in. TODO: Javascript validation
                                intTester = Convert.ToInt32(param["adminPort"]);
                                intTester = Convert.ToInt32(param["publicPort"]);
                                IPAddress ipTest = IPAddress.Parse(param["listenIp"]);
                            }
                            catch (Exception e)
                            {
                                YAMS.Database.AddLog("Invalid input on network settings", "web", "warn");
                                return ModuleResult.Stop;
                            }

                            Database.SaveSetting("EnablePortForwarding", param["portForwarding"]);
                            Database.SaveSetting("EnableOpenFirewall", param["openFirewall"]);
                            Database.SaveSetting("EnablePublicSite", param["publicEnable"]);
                            Database.SaveSetting("AdminListenPort", param["adminPort"]);
                            Database.SaveSetting("PublicListenPort", param["publicPort"]);
                            Database.SaveSetting("YAMSListenIP", param["listenIp"]);
                            Database.SaveSetting("EnableTelnet", param["telnetEnable"]);
                            Database.SaveSetting("TelnetPort", param["telnetPort"]);

                            Database.AddLog("Network settings have been saved, to apply changes a service restart is required. Please check they are correct before restarting", "web", "warn");
                            break;
                        case "job-list":
                            DataSet rdJobs = Database.ListJobs();
                            strResponse = JsonConvert.SerializeObject(rdJobs, Formatting.Indented);
                            break;
                        case "delete-job":
                            string strJobID = param["jobid"];
                            Database.DeleteJob(strJobID);
                            strResponse = "done";
                            break;
                        case "add-job":
                            intServerID = Convert.ToInt32(param["job-server"]);
                            int intHour = Convert.ToInt32(param["job-hour"]);
                            int intMinute = Convert.ToInt32(param["job-minute"]);
                            Database.AddJob(param["job-type"], intHour, intMinute, param["job-params"], intServerID);
                            break;
                        case "logout":
                            customPrincipal.Identity = new AnonymousIdentity();
                            break;
                        case "newserver":
                            var NewServer = new List<KeyValuePair<string, string>>();
                            NewServer.Add(new KeyValuePair<string, string>("motd", "Welcome to a YAMS server!"));
                            NewServer.Add(new KeyValuePair<string, string>("server-ip", Networking.GetListenIP().ToString()));
                            NewServer.Add(new KeyValuePair<string, string>("server-name", param["name"]));
                            NewServer.Add(new KeyValuePair<string, string>("server-port", Networking.TcpPort.FindNextAvailablePort(25565).ToString()));
                            Database.NewServerWeb(NewServer, param["name"], 1024);
                            strResponse = "done";
                            break;
                        case "updateDNS":
                            Database.SaveSetting("DNSName", param["dns-name"]);
                            Database.SaveSetting("DNSSecret", param["dns-secret"]);
                            Database.SaveSetting("LastExternalIP", param["dns-external"]);
                            strResponse = "done";
                            break;
                        case "getDNS":
                            strResponse = "{ \"name\":\"" + Database.GetSetting("DNSName", "YAMS") + "\", \"secret\": \"" + Database.GetSetting("DNSSecret", "YAMS") + "\", \"external\" : \"" + Networking.GetExternalIP().ToString() + "\" }";
                            break;
                        case "backup-now":
                            Backup.BackupNow(Core.Servers[Convert.ToInt32(param["serverid"])], param["title"]);
                            strResponse = "{ \"result\" : \"sent\" }";
                            break;
                        default:
                            return ModuleResult.Stop;
                    }

                    return writeContent(context, "Completed - YAMS (API)", "text/javascript", strResponse);
                }
                else
                {
                    // not a post, so say bye bye!
                    return ModuleResult.Stop;
                }

            }
            else if (context.Request.Uri.AbsoluteUri.Contains(@"/admin"))
            {
                try
                {
                    if (customPrincipal == null)
                    {
                        Thread.CurrentPrincipal = new CustomPrincipal();
                        customPrincipal = Thread.CurrentPrincipal as CustomPrincipal;
                        customPrincipal.Identity = new AnonymousIdentity();
                    } 
                }
                catch (Exception e)
                {
                    return writeContent(context, "Completed - YAMS", "text/html", e.ToString());
                }

                if (!customPrincipal.Identity.Name.Equals("admin") || !customPrincipal.Identity.IPAddress.Equals(ip.Address.ToString()))
                {
                    return writeContent(context, "Completed - YAMS", "text/html", File.ReadAllText(YAMS.Core.RootFolder + @"\web\admin\login.html"));
                }
                else
                {
                    return writeContent(context, "Completed - YAMS", "text/html", File.ReadAllText(YAMS.Core.RootFolder + @"\web\admin\index.html"));
                }
            }
            else if (context.Request.Uri.AbsoluteUri.Contains(@"/login"))
            {
                //This is a login request, check it's legit
                string userName = context.Request.Form["strUsername"];
                string password = context.Request.Form["strPassword"];

                if (userName == "admin" && password == Database.GetSetting("AdminPassword", "YAMS"))
                {
                    try
                    {
                        var ip_store = context.Request.RemoteEndPoint as System.Net.IPEndPoint;
                        Thread.CurrentPrincipal = new CustomPrincipal();
                        customPrincipal = Thread.CurrentPrincipal as CustomPrincipal;
                        customPrincipal.Identity = new CustomIdentity("admin", ip_store.Address.ToString());
                        return writeContent(context, "Completed - YAMS", "text/html", File.ReadAllText(YAMS.Core.RootFolder + @"\web\admin\index.html"));
                    }
                    catch (Exception e)
                    {
                        return writeContent(context, "Completed - YAMS", "text/html", e.ToString());
                    }
                }
                else
                {
                    return writeContent(context, "Completed - YAMS", "text/html", File.ReadAllText(YAMS.Core.RootFolder + @"\web\admin\login.html"));
                }
            }
            else if (context.Request.Uri.AbsoluteUri.Contains(@"/")) {
                return writeContent(context, "Completed - YAMS", "text/html", File.ReadAllText(YAMS.Core.RootFolder + @"\web\admin\login.html"));
            }
            else
            {
                return writeContent(context, "Completed - YAMS", "text/html", context.Request.Uri.AbsoluteUri); //not a mapped URI, but we will show the server is at least alive
            }

        }

        /// <summary>
        /// For a valid HTTP 200 result, take the reason, type of data, and actual data and render to user.
        /// </summary>
        /// <param name="context">Current HTTP Context of Webserver</param>
        /// <param name="reason">Reason Phrase</param>
        /// <param name="type">HTML data type, i.e. text/html or text/javascript</param>
        /// <param name="data">The data to send back</param>
        /// <returns>A ModuleResult.Continue</returns>
        public ModuleResult writeContent(IHttpContext context, String reason, String type, String data)
        {
            context.Response.ReasonPhrase = reason;
            byte[] buffer = Encoding.UTF8.GetBytes(data);
            context.Response.ContentType = type;
            context.Response.StatusCode = 200;
            MemoryStream stream = new MemoryStream();
            stream.Write(buffer, 0, buffer.Length);
            context.Response.Body = stream;
            context.Response.Body.Position = 0;
            return ModuleResult.Continue;
        }

    }

    /// <summary>
    /// Use a custom Identity Class to Handle Authentication in New Framework / .NET 4.5 Standards
    /// </summary>
    public class CustomIdentity : System.Security.Principal.IIdentity
    {
        public CustomIdentity(string name, string ipaddr)
        {
            Name = name;
            IPAddress = ipaddr;
        }

        public string Name { get; private set; }

        public string IPAddress { get; private set; }

        #region IIdentity Members
        public string AuthenticationType { get { return "Custom authentication"; } }

        public bool IsAuthenticated { get { return !Name.Equals("anonymous"); } }
        #endregion
    }

    public class AnonymousIdentity : CustomIdentity
    {
        public AnonymousIdentity()
            : base("anonymous", "0.0.0.0")
        { }
    }

    public class CustomPrincipal : System.Security.Principal.IPrincipal
    {
        private CustomIdentity _identity;

        public CustomIdentity Identity
        {
            get { return _identity ?? new AnonymousIdentity(); }
            set { _identity = value; }
        }

        #region IPrincipal Members
        System.Security.Principal.IIdentity System.Security.Principal.IPrincipal.Identity
        {
            get { return this.Identity; }
        }
        #endregion

        public bool IsInRole(string role)
        {
            return false;
        }
    }
}
