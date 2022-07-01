//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using EP.U3D.LIBRARY.BASE;
using EP.U3D.LIBRARY.EVT;
using EP.U3D.LIBRARY.JSON;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace EP.U3D.LIBRARY.NET
{
    public class NetManager : EvtManager
    {
        protected static new NetManager _instance;

        protected static Dictionary<int, NetConnection> connections = new Dictionary<int, NetConnection>();

        public static new NetManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new NetManager();
                }
                return _instance;
            }
        }

        public NetManager() : base() { }

        public static void Initialize()
        {
            connections = new Dictionary<int, NetConnection>();
        }

        public static NetConnection ConnectTo(int type, string host, int post, NetConnection.StatusDelegate onConnected, NetConnection.StatusDelegate onDisconnected, NetConnection.StatusDelegate onReconnected, NetConnection.StatusDelegate onErrorOccurred)
        {
            if (connections.ContainsKey(type))
            {
                DisconnectFrom(type);
            }
            NetConnection connection = new NetConnection(host, post, onConnected, onDisconnected, onReconnected, onErrorOccurred);
            connection.Connect();
            connections.Add(type, connection);
            return connection;
        }

        public static void DisconnectFrom(int type)
        {
            NetConnection connection;
            if (connections.TryGetValue(type, out connection))
            {
                connection.Disconnect();
                connections.Remove(type);
            }
        }

        public static NetConnection GetConnection(int type)
        {
            NetConnection connection;
            connections.TryGetValue(type, out connection);
            return connection;
        }

        public static void DisconnectAll()
        {
            Dictionary<int, NetConnection>.Enumerator it = connections.GetEnumerator();
            for (int i = 0; i < connections.Count; i++)
            {
                it.MoveNext();
                NetConnection connection = it.Current.Value;
                if (connection != null)
                {
                    connection.Disconnect();
                }
            }
            connections.Clear();
        }

        public static void RegMsg(int id, EventHandlerDelegate func)
        {
            Instance.Register(id, func);
        }

        public static void UnregMsg(int id, EventHandlerDelegate func)
        {
            Instance.Unregister(id, func);
        }

        public static void NotifyMsg(Evt evt)
        {
            Instance.Notify(evt);
        }

        public static void SendMsg(int id, object body, int uid = 0, int rid = 0, int server = 0)
        {
            SendMsg(id, EncodePB(body), uid, rid, server);
        }

        public static void SendMsg(int id, byte[] body, int uid = 0, int rid = 0, int server = 0)
        {
            NetConnection connection;
            if (connections.TryGetValue(server, out connection))
            {
                NetPacket packet = new NetPacket(id, body.Length);
                packet.Body = body;
                packet.PlayerID = uid == -1 ? Constants.CONN_SERVER_UID : uid;
                packet.ServerID = rid;
                connection.Send(packet);
            }
        }

        public static object DecodeMsg(Type type, Evt evt)
        {
            return DecodePB(type, evt.Param as byte[]);
        }

        public static object DecodePB(Type type, byte[] buffer)
        {
            using (var ms = new System.IO.MemoryStream(buffer))
            {
                var obj = ProtoBuf.Serializer.NonGeneric.Deserialize(type, ms);
                ms.Close();
                return obj;
            }
        }

        public static byte[] EncodePB(object msg)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                ProtoBuf.Serializer.NonGeneric.Serialize(ms, msg);
                byte[] buf = ms.ToArray();
                ms.Close();
                return buf;
            }
        }

        public static object DecodeJson(string content, Type type)
        {
            return JsonMapper.ToObject(type, content);
        }

        public static string EncodeJson(object obj)
        {
            return JsonMapper.ToJson(obj);
        }

        public static void SendCgi(int id, byte[] body, Action<string, byte[]> callback = null, int uid = 0, int rid = 0, string host = null)
        {
            Loom.StartCR(DoCgi(id, body, callback, uid, rid, host));
        }

        private static IEnumerator DoCgi(int id, byte[] body, Action<string, byte[]> callback = null, int uid = 0, int rid = 0, string host = null)
        {
            if (string.IsNullOrEmpty(host)) host = Constants.CGI_SERVER_URL;
            using (UnityWebRequest request = new UnityWebRequest(host, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/octet-stream");
                if (!string.IsNullOrEmpty(Constants.CGI_ACCESS_TOKEN)) request.SetRequestHeader("AccessToken", Constants.CGI_ACCESS_TOKEN);
                if (!string.IsNullOrEmpty(Constants.CGI_REFRESH_TOKEN)) request.SetRequestHeader("RefreshToken", Constants.CGI_REFRESH_TOKEN);
                request.SetRequestHeader("CID", id.ToString());
                request.SetRequestHeader("UID", uid == -1 ? Constants.CGI_SERVER_UID.ToString() : uid.ToString());
                request.SetRequestHeader("RID", rid.ToString());
                yield return request.SendWebRequest();
                if (request.responseCode == 200)
                {
                    callback?.Invoke(null, request.downloadHandler.data);
                }
                else
                {
                    callback?.Invoke(request.error, request.downloadHandler.data);
                }
            }
        }
    }
}