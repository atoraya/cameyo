﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;
using System.Runtime.Serialization.Json;

namespace Cameyo.Player
{
    // ServerLib
    public class ServerLib
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string ListMode { get; set; }
    }

    // AccountInfo
    public class AccountInfo
    {
        public List<ServerLib> libs { get; set; }
        public string StorageProviderName { get; set; }
        public string StorageProviderUserID { get; set; }
        public string LicData { get; set; }
    }

    // ServerApp
    public class ServerApp
    {
        public string PkgId { get; set; }
        public string AppID{ get; set; }
        public string InfoStr { get; set; }
        public string IconUrl { get; set; }
        public string Version { get; set; }
        public string Category { get; set; }
        public long Size { get; set; }
    }

    // ServerAppDetails
    public class ServerAppDetails
    {
        public string AppID { get; set; }
        public string PkgId { get; set; }
        public string DownloadUrl { get; set; }
        public string FriendlyName { get; set; }
        public int Public { get; set; }
        public DateTime WhenCreated { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; }
        public DateTime ExpirationDate { get; set; }
        public string EngineVer { get; set; }
        public string BuildUid { get; set; }
        public int StreamingAvailable { get; set; }
        public long StreamedSize { get; set; }
        public string Streamer { get; set; }
        public string Publisher { get; set; }
        public string Version { get; set; }
        public string Category { get; set; }
        public int DownloadCounts { get; set; }
        public int PopularityIndex { get; set; }
        public int ThumbUp { get; set; }
        public int ThumbDown { get; set; }
        public string ShortDesc { get; set; }
        public string Description { get; set; }
        public string Comments { get; set; }
        public string[] ScreenshotUrls { get; set; }
        public string IconUrl { get; set; }
    }

    // ServerClient
    public class ServerClient
    {
        public string Login { get; set; }
        private string Password { get; set; }
        public string ServerHost { get { return "online.cameyo.com"; } }
        private int ServerPort { get { return 443; } }
        private bool IsHttps { get { return true; } }
        private string LoginFile { get { return Utils.MyPath("Login.dat"); } }
        private string PasswordFile { get { return Utils.MyPath("Pwd.dat"); } }
        public AccountInfo AccountInfo;
        public License License = new License();

        public void SetCredentials(string login, string password)
        {
            Login = login;
            Password = password;
        }

        public string ServerUrl()
        {
            string url = string.Format("{0}://{1}:{2}", IsHttps ? "https" : "http", ServerHost, ServerPort);
            return url;
        }

        private string SendRequest(string op, bool credentials, string extraParams)
        {
            var webClient = new WebClient();
            string url = string.Format("{0}/packager.aspx?op={1}", ServerUrl(), op);
            if (credentials)
                url += string.Format("&user={0}&pass={1}", Login, Password);
            if (!string.IsNullOrEmpty(extraParams))
                url += extraParams;
            return webClient.DownloadString(url);
        }

        private string SendRequest(string op, bool credentials)
        {
            return SendRequest(op, credentials, null);
        }

        private string CacheOrWeb(string op, bool credentials, string extraParams, string cacheIdentifier)
        {
            string cacheDir = CacheDir(string.Format("{0} - {1}", ServerHost, Login));
            Directory.CreateDirectory(cacheDir);
            string libCacheFile = cacheDir + "\\" + cacheIdentifier + ".txt";
            string json;
            if (File.Exists(libCacheFile))
                json = File.ReadAllText(libCacheFile);
            else
            {
                json = SendRequest(op, credentials, extraParams);
                File.WriteAllText(libCacheFile, json);
            }
            return json;
        }

        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static bool DeserializeJson<T>(string json, ref T t)
        {
            using (var memoryStream = new MemoryStream())
            {
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
                memoryStream.Write(jsonBytes, 0, jsonBytes.Length);
                memoryStream.Seek(0, SeekOrigin.Begin);
                using (var jsonReader = JsonReaderWriterFactory.CreateJsonReader(memoryStream, Encoding.UTF8, XmlDictionaryReaderQuotas.Max, null))
                {
                    var serializer = new DataContractJsonSerializer(typeof(T));
                    try
                    {
                        t = (T)serializer.ReadObject(jsonReader);
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public bool AuthCached(ref string login, ref string password)
        {
            if (!File.Exists(LoginFile) || !File.Exists(PasswordFile))
                return false;

            var loginEncrypted = File.ReadAllText(LoginFile);
            var passwordEncrypted = File.ReadAllText(PasswordFile);
            string description;

            try
            {
                login = DPAPI.Decrypt(loginEncrypted, "=EQW*", out description);
                password = DPAPI.Decrypt(passwordEncrypted, "M#$!", out description);
            }
            catch
            {
                return false;
            }

            return (!string.IsNullOrEmpty(login) && !string.IsNullOrEmpty(password));
        }

        public bool Auth(string login, string password, bool cache)
        {
            SetCredentials(login, password);
            var json = SendRequest("AccountAuth", true, null);
            if (DeserializeJson<AccountInfo>(json, ref AccountInfo))
            {
                if (cache)
                {
                    File.WriteAllText(LoginFile, DPAPI.Encrypt(DPAPI.KeyType.UserKey, login, "=EQW*"));
                    File.WriteAllText(PasswordFile, DPAPI.Encrypt(DPAPI.KeyType.UserKey, password, "M#$!"));
                }
                return true;
            }
            else
                return false;
        }

        public List<ServerApp> PkgList(string libId)
        {
            var json = CacheOrWeb("PkgList", true, "&lib=" + libId + "&detail=Player", "Lib." + libId);
            List<ServerApp> retVal = null;
            if (DeserializeJson<List<ServerApp>>(json, ref retVal))
                return retVal;
            else
                return new List<ServerApp>();   // Return empty list on error?
        }

        public ServerAppDetails AppDetails(string pkgId)
        {
            var json = CacheOrWeb("PkgInfo", true, "&pkgId=" + pkgId + "&detail=Player", pkgId);
            ServerAppDetails retVal = null;
            if (DeserializeJson<ServerAppDetails>(json, ref retVal))
                return retVal;
            else
                return new ServerAppDetails();   // Return empty item on error?
        }

        public bool DownloadIcon(string iconUrl, string pkgId)
        {
            try
            {
                var webClient = new WebClient();
                webClient.DownloadFile(iconUrl, LocalIconFile(pkgId));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string LocalIconFile(string pkgId)
        {
            string cacheDir = ImgCacheDir();
            string libCacheFile = cacheDir + "\\" + pkgId + ".png";
            return libCacheFile;
        }

        private string CacheDir(string subDir)
        {
            return Path.Combine(Utils.MyPath("Cache"), subDir);
        }

        public string ImgCacheDir()
        {
            return CacheDir(string.Format("{0}", ServerHost));
        }

        public void DeleteUserCache()
        {
            Utils.KillFile(this.LoginFile);
            Utils.KillFile(this.PasswordFile);
            Utils.KillFile(Utils.MyPath("License.dat"));
            if (!string.IsNullOrEmpty(ServerHost) && !string.IsNullOrEmpty(Login))
            {
                Utils.Deltree(CacheDir(string.Format("{0} - {1}", ServerHost, Login)));
                //Utils.Deltree(ImgCacheDir());   // More as a troubleshooting helper
            }
        }
    }

    // ServerSingleton
    public class ServerSingleton
    {
        public ServerClient ServerClient;
        private static volatile ServerSingleton instance;
        private static object syncRoot = new Object();

        private ServerSingleton()
        {
            ServerClient = new ServerClient();
        }

        public static ServerSingleton Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                        {
                            instance = new ServerSingleton();
                        }
                    }
                }
                return instance;
            }
        }
    }

    // License
    public class License
    {
        public enum LicenseType
        {
            Corrupted = -3,
            Expired = -2,
            RevalidationRequired = -1,
            None = 0,
            Basic = 1,
            Pro = 2,
            Dev = 3,
            Edu = 4,
            Ent = 5,
            Corp = 6
        }
        public LicenseType Type { get; set; }

        public string ProdTypeStr()
        {
            switch (Type)
            {
                case LicenseType.Pro:
                    return "Pro";
                case LicenseType.Dev:
                    return "Dev";
                case LicenseType.Edu:
                case LicenseType.Ent:
                case LicenseType.Corp:
                    return "Ent";
                default:
                    return "Free";
            }
        }

        public string StatusStr()
        {
            switch (Type)
            {
                case LicenseType.Corrupted:
                    return "Corrupted";
                case LicenseType.Expired:
                    return "Expired";
                case LicenseType.RevalidationRequired:
                    return "RevalidationRequired";
                case LicenseType.None:
                    return "None";
                case LicenseType.Basic:
                    return "Basic";
                case LicenseType.Pro:
                    return "Pro";
                case LicenseType.Dev:
                    return "Developer";
                case LicenseType.Edu:
                    return "Education";
                case LicenseType.Ent:
                    return "Enterprise";
                case LicenseType.Corp:
                    return "Corporate";
                default:
                    return "Unknown";
            }
        }
    }
}
