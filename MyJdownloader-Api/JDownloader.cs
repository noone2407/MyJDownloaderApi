﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyJdownloader_Api
{
    public class JDownloader
    {
        public class JdDevice
        {
            [JsonProperty(PropertyName = "id")]
            public string Id;
            [JsonProperty(PropertyName = "name")]
            public string Name;
            [JsonProperty(PropertyName = "type")]
            public string Type;
        }
        public class DownloadPackageQuery
        {
            [JsonProperty(PropertyName = "bytesTotal")]
            public string BytesTotal;
            [JsonProperty(PropertyName = "enabled")]
            public string Enabled;
            [JsonProperty(PropertyName = "finished")]
            public string Finished;
            [JsonProperty(PropertyName = "name")]
            public string Name;
            [JsonProperty(PropertyName = "packageUUID")]
            public string PackageUuiD;
            [JsonProperty(PropertyName = "running")]
            public string Running;
            [JsonProperty(PropertyName = "status")]
            public string Status;
            [JsonProperty(PropertyName = "url")]
            public string Url;
            [JsonProperty(PropertyName = "uuid")]
            public string Uuid;
            [JsonProperty(PropertyName = "comment")]
            public string comment;
            [JsonProperty(PropertyName = "host")]
            public string host;
        }
        private const string ApiUrl = "http://api.jdownloader.org";
        private const string Version = "1.0.0";
        private const string ServerDomain = "server";
        private const string DeviceDomain = "device";
        private const string Appkey = "MyJDAPI_CSharp";
        private const int ApiVer = 1;

        private int _ridCounter;
        private string _sessiontoken;
        private string _regaintoken;

        private byte[] _loginSecret;
        private byte[] _deviceSecret;
        private byte[] _serverEncryptionToken;
        private byte[] _deviceEncryptionToken;

        public List<JdDevice> Devices = new List<JdDevice>();

        #region public method
        public JDownloader(string email = "", string password = "")
        {
            _ridCounter = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
                Connect(email, password);
        }

        public Version GetVersion()
        {
            return new Version(Version);
        }
        public bool Connect(string email, string password)
        {
            _loginSecret = CreateSecret(email, password, ServerDomain);
            _deviceSecret = CreateSecret(email, password, DeviceDomain);
            string query = "/my/connect?email=" + HttpUtility.UrlEncode(email) + "&appkey=" + HttpUtility.UrlEncode(Appkey);
            var response = CallServer(query, _loginSecret);
            if (string.IsNullOrEmpty(response))
                return false;
            dynamic jsonContent = JObject.Parse(response);
            _sessiontoken = jsonContent.sessiontoken;
            _regaintoken = jsonContent.regaintoken;
            _serverEncryptionToken = UpdateEncryptionToken(_loginSecret, _sessiontoken);
            _deviceEncryptionToken = UpdateEncryptionToken(_deviceSecret, _sessiontoken);
            return true;
        }
        public bool Reconnect()
        {
            string query = "/my/reconnect?appkey=" + HttpUtility.UrlEncode(Appkey) + "&sessiontoken=" + HttpUtility.UrlEncode(_sessiontoken) + "&regaintoken=" + HttpUtility.UrlEncode(_regaintoken);
            var response = CallServer(query, _serverEncryptionToken);
            if (string.IsNullOrEmpty(response))
                return false;
            dynamic jsonContent = JObject.Parse(response);
            _sessiontoken = jsonContent.sessiontoken;
            _regaintoken = jsonContent.regaintoken;
            _serverEncryptionToken = UpdateEncryptionToken(_loginSecret, _sessiontoken);
            _deviceEncryptionToken = UpdateEncryptionToken(_deviceSecret, _sessiontoken);
            return true;
        }
        public bool Disconnect()
        {
            string query = "/my/disconnect?sessiontoken=" + HttpUtility.UrlEncode(_sessiontoken);
            var response = CallServer(query, _serverEncryptionToken);
            if (string.IsNullOrEmpty(response))
                return false;
            _sessiontoken = string.Empty;
            _regaintoken = string.Empty;
            _serverEncryptionToken = null;
            _deviceEncryptionToken = null;
            return true;
        }
        public bool EnumerateDevices()
        {
            string query = "/my/listdevices?sessiontoken=" + HttpUtility.UrlEncode(_sessiontoken);
            var response = CallServer(query, _serverEncryptionToken);
            if (string.IsNullOrEmpty(response))
                return false;
            dynamic jsonContent = JObject.Parse(response);
            Devices = jsonContent.list.ToObject<List<JdDevice>>();
            return true;
        }
        public bool GetDirectConnectionInfos()
        {
            foreach (var device in Devices)
            {
                string result = CallAction(device, "/device/getDirectConnectionInfos", null);
                if (string.IsNullOrEmpty(result))
                {
                    return false;
                }
                dynamic jsonContent = JObject.Parse(result);
            }
            return true;
        }

        public bool AddLink(JdDevice device, string link, string package)
        {
            dynamic obj = new ExpandoObject();
            obj.priority = "DEFAULT";
            obj.links = link;
            obj.autostart = true;
            obj.packageName = package;
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(obj);
            var param = new[] { json };
            string result = CallAction(device, "/linkgrabberv2/addLinks", param);
            if (string.IsNullOrEmpty(result))
                return false;
            return true;
        }

        public bool Stop(JdDevice device)
        {
            string result = CallAction(device, "/downloadcontroller/stop", null);
            if (string.IsNullOrEmpty(result))
            {
                return false;
            }
            return true;
        }
        public bool Start(JdDevice device)
        {
            string result = CallAction(device, "/downloadcontroller/start", null);
            if (string.IsNullOrEmpty(result))
            {
                return false;
            }
            return true;
        }

        public string GetCurrentState(JdDevice device)
        {
            string response = CallAction(device, "/downloadcontroller/getCurrentState", null);
            if (!string.IsNullOrEmpty(response))
            {
                dynamic jsonContent = JObject.Parse(response);
                return jsonContent.data;
            }
            return "";
        }

        public string GetSpeedInBps(JdDevice device)
        {
            string response = CallAction(device, "/downloadcontroller/getSpeedInBps", null);
            if (!string.IsNullOrEmpty(response))
            {
                dynamic jsonContent = JObject.Parse(response);
                return jsonContent.data;
            }
            return "";
        }

        public string PackageCount(JdDevice device)
        {
            string response = CallAction(device, "/downloadsV2/packageCount", null);
            if (!string.IsNullOrEmpty(response))
            {
                dynamic jsonContent = JObject.Parse(response);
                return jsonContent.data;
            }
            return "";
        }

        public void ExitJd(JdDevice device)
        {
            string response = CallAction(device, "/system/exitJD", null);
            if (!string.IsNullOrEmpty(response))
            {
                dynamic jsonContent = JObject.Parse(response);
                string data = jsonContent.data;
            }
        }

        public void RestartJd(JdDevice device)
        {
            string response = CallAction(device, "/system/restartJD", null);
            if (!string.IsNullOrEmpty(response))
            {
                dynamic jsonContent = JObject.Parse(response);
                string data = jsonContent.data;
            }
        }

        public IEnumerable<DownloadPackageQuery> QueryLinks(JdDevice device)
        {
            dynamic obj = new ExpandoObject();
            obj.bytesTotal = true;
            obj.comment = true;
            obj.status = true;
            obj.enabled = true;
            obj.packageUUIDs = null;
            obj.host = true;
            obj.url = true;
            obj.finished = true;
            obj.running = true;
            string json = JsonConvert.SerializeObject(obj);
            var param = new[] { json };
            string result = CallAction(device, "/downloadsV2/queryLinks", param);
            dynamic jsonObject = JObject.Parse(result);
            dynamic links = jsonObject.data.ToObject<List<DownloadPackageQuery>>();
            if (string.IsNullOrEmpty(result))
                return null;
            return links;
        }
        #endregion

        #region private method
        private string CallServer(string query, byte[] key, string param = "")
        {
            string rid;
            if (!string.IsNullOrEmpty(param))
            {
                if (key != null)
                {
                    param = Encrypt(param, key);
                }
                rid = _ridCounter.ToString();
            }
            else
            {
                rid = GetUniqueRid().ToString();
            }
            if (query.Contains("?"))
                query += "&";
            else
                query += "?";
            query += "rid=" + rid;
            string signature = Sign(query, key);
            query += "&signature=" + signature;
            string url = ApiUrl + query;
            if (!string.IsNullOrWhiteSpace(param))
                param = string.Empty;
            string response = PostQuery(url, param, key);
            if (string.IsNullOrEmpty(response))
                return null;
            dynamic jsonContent = JObject.Parse(response);
            int jsonContentRid = jsonContent.rid;
            if (!jsonContentRid.Equals(_ridCounter) && jsonContentRid > 0)
            {
                Debug.WriteLine("error: rid mismatch!\n");
                return null;
            }
            Debug.WriteLine("url=" + url);
            Debug.WriteLine("response=" + response);
            return response;
        }

        private string CallAction(JdDevice device, string action, dynamic param)
        {
            if (Devices == null || Devices.Count == 0)
            {
                Debug.WriteLine("No device or not enumerate device list yet");
                return null;
            }

            if (!Devices.Contains(device))
            {
                Debug.WriteLine("No device with the given name");
                return null;
            }
            if (string.IsNullOrEmpty(device.Id))
            {
                Debug.WriteLine("Device is found with empty id");
                return null;
            }
            string query = "/t_" + HttpUtility.UrlEncode(_sessiontoken) + "_" + HttpUtility.UrlEncode(device.Id) + action;
            dynamic p = new ExpandoObject();
            p.url = action;
            if (param != null)
            {
                p.@params = param;
            }
            p.rid = GetUniqueRid();
            p.ApiVer = ApiVer;
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(p);
            File.WriteAllText("json.txt", json);

            json = Encrypt(json, _deviceEncryptionToken);
            string url = ApiUrl + query;
            string response = PostQuery(url, json, _deviceEncryptionToken);
            if (string.IsNullOrEmpty(response))
                return null;
            dynamic jsonContent = JObject.Parse(response);
            int jsonContentRid = jsonContent.rid;
            if (!jsonContentRid.Equals(_ridCounter) && jsonContentRid > 0)
            {
                Debug.WriteLine("error: rid mismatch!\n");
                return null;
            }
            Debug.WriteLine("url=" + url);
            Debug.WriteLine("response=" + response);
            return response;
        }
        private string PostQuery(string url, string postfields = "", byte[] ivKey = null)
        {

            var request = (HttpWebRequest)WebRequest.Create(url);
            if (!string.IsNullOrEmpty(postfields))
            {
                request.Method = "POST";
                request.ContentType = "application/aesjson-jd; charset=utf-8";
                byte[] postByteArray = Encoding.UTF8.GetBytes(postfields);
                request.ContentLength = postByteArray.Length;
                Stream postStream = request.GetRequestStream();
                postStream.Write(postByteArray, 0, postByteArray.Length);
                postStream.Close();
            }
            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse) request.GetResponse();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    response.Close();
                    return null;
                }
                Stream responseStream = response.GetResponseStream();
                if (responseStream != null)
                {
                    var myStreamReader = new StreamReader(responseStream);
                    string result = myStreamReader.ReadToEnd();
                    response.Close();
                    if (ivKey != null)
                    {
                        result = Decypt(result, ivKey);
                    }
                    return result;
                }
            }
            catch (WebException exception)
            {
                if (exception.Response != null)
                {
                    Debug.WriteLine(exception.Message);
                    if (exception.Response != null)
                    {
                        Stream respsone = exception.Response.GetResponseStream();
                        if (respsone != null)
                        {
                            string resp = new StreamReader(respsone).ReadToEnd();
                            Debug.WriteLine(resp);
                        }
                    }
                    return null;
                }

            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                return null;

            }
            return null;
        }

        private string Sign(string data, byte[] key)
        {
            if (key == null)
            {
                throw new Exception("Null ivKey, maybe not logged in yet or disconnected");
            }
            var dataByte = Encoding.UTF8.GetBytes(data);
            var hmacsha256 = new HMACSHA256(key);
            hmacsha256.ComputeHash(dataByte);
            var hash = hmacsha256.Hash;
            string sbinary = hash.Aggregate("", (current, t) => current + t.ToString("X2"));
            return sbinary.ToLower();
        }
        private int GetUniqueRid()
        {
            _ridCounter++;
            return _ridCounter;
        }

        private string Encrypt(string data, byte[] ivKey)
        {
            if (ivKey == null)
            {
                throw new Exception("Null ivKey, maybe not logged in yet or disconnected");
            }
            var iv = new byte[16];
            var key = new byte[16];
            for (int i = 0; i < 32; i++)
            {
                if (i < 16)
                {
                    iv[i] = ivKey[i];
                }
                else
                {
                    key[i - 16] = ivKey[i];
                }
            }
            var rj = new RijndaelManaged
            {
                Key = key,
                IV = iv,
                Mode = CipherMode.CBC,
                BlockSize = 128
            };
            ICryptoTransform encryptor = rj.CreateEncryptor();
            var msEncrypt = new MemoryStream();
            var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(data);
            }
            byte[] encrypted = msEncrypt.ToArray();
            return Convert.ToBase64String(encrypted);
        }

        private string Decypt(string data, byte[] ivKey)
        {
            if (ivKey == null)
            {
                throw new Exception("Null ivKey, maybe not logged in yet or disconnected");
            }
            var iv = new byte[16];
            var key = new byte[16];
            for (int i = 0; i < 32; i++)
            {
                if (i < 16)
                {
                    iv[i] = ivKey[i];
                }
                else
                {
                    key[i - 16] = ivKey[i];
                }
            }
            byte[] cypher = Convert.FromBase64String(data);
            var rj = new RijndaelManaged
            {
                BlockSize = 128,
                Mode = CipherMode.CBC,
                IV = iv,
                Key = key
            };
            var ms = new MemoryStream(cypher);
            string result;
            using (var cs = new CryptoStream(ms, rj.CreateDecryptor(), CryptoStreamMode.Read))
            {
                using (var sr = new StreamReader(cs))
                {
                    result = sr.ReadToEnd();
                }
            }
            return result;
        }

        private byte[] UpdateEncryptionToken(byte[] oldToken, string updateToken)
        {
            byte[] newtoken = FromHex(updateToken);
            var newhash = new byte[oldToken.Length + newtoken.Length];
            oldToken.CopyTo(newhash, 0);
            newtoken.CopyTo(newhash, 32);
            var hashString = new SHA256Managed();
            hashString.ComputeHash(newhash);
            return hashString.Hash;
        }

        private byte[] CreateSecret(string email, string password, string domain)
        {
            string plaintext = email.ToLower() + password + domain.ToLower();
            return GetSHA256(plaintext);
        }
        private byte[] GetSHA256(string text)
        {
            var hashString = new SHA256Managed();
            return hashString.ComputeHash(Encoding.UTF8.GetBytes(text));

        }

        private byte[] FromHex(string hex)
        {
            hex = hex.Replace("-", "");
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return raw;
        }
        #endregion
  
    }
}
