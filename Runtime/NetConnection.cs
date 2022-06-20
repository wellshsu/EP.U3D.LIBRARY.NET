//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EP.U3D.LIBRARY.EVT;
using EP.U3D.LIBRARY.BASE;

namespace EP.U3D.LIBRARY.NET
{
    public class NetConnection
    {
        public enum CallbackType
        {
            OnConnected,
            OnDisconnected,
            OnReconnected,
            OnErrorrOccurred,
        }

        public delegate void StatusDelegate(NetConnection conn, object param);

        protected string host;
        protected int port;
        protected Socket socket;
        protected Thread connThread;
        protected Thread reconnThread;
        protected string error;
        protected int reconnectInterval = 2000;
        protected byte[] receiveHeader = new byte[NetPacket.HEAD_LENGTH];
        protected bool sigSocketReleased = false;
        protected bool sigReconnectOneDone = false;
        protected bool sigReconnecting = false;
        protected bool sigReconnectOneTimeout = false;
        protected int reconnectOneElapse = 0;

        protected StatusDelegate onConnected;
        protected StatusDelegate onDisconnected;
        protected StatusDelegate onReconnected;
        protected StatusDelegate onErrorOccurred;

        public NetConnection() { }

        public NetConnection(string host, int port, StatusDelegate onConnected, StatusDelegate onDisconnected, StatusDelegate onReconnected, StatusDelegate onErrorOccurred)
        {
            this.host = host;
            this.port = port;
            this.onConnected = onConnected;
            this.onDisconnected = onDisconnected;
            this.onReconnected = onReconnected;
            this.onErrorOccurred = onErrorOccurred;
        }

        public bool IsConnected
        {
            get
            {
                if (socket == null)
                {
                    return false;
                }
                else
                {
                    return socket.Connected;
                }
            }
        }

        public virtual void Connect()
        {
            if (connThread != null)
            {
                connThread.Abort();
            }
            connThread = new Thread(() =>
            {
                try
                {
                    IPAddress[] addrs = Dns.GetHostAddresses(host);
                    System.Random r = new System.Random();
                    IPAddress addr = addrs[r.Next(0, addrs.Length)];
                    IPEndPoint rep = new IPEndPoint(addr, port);
                    if (socket == null)
                    {
                        socket = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    }
                    socket.BeginConnect(rep, new AsyncCallback(ConnCallback), this);
                }
                catch (Exception e)
                {
                    connThread = null;
                    error = e.Message;
                    ErrorOccurred();
                }
            });
            connThread.Start();
        }

        public virtual void Reconnect()
        {
            if (sigReconnecting == false)
            {
                sigReconnecting = true;
                ReleaseSocket();
                reconnThread = new Thread(() =>
                {
                    while (IsConnected == false)
                    {
                        sigReconnectOneDone = false;
                        sigReconnectOneTimeout = false;
                        reconnectOneElapse = 0;
                        try
                        {
                            IPAddress[] addrs = Dns.GetHostAddresses(host);
                            System.Random r = new System.Random();
                            IPAddress addr = addrs[r.Next(0, addrs.Length)];
                            IPEndPoint rep = new IPEndPoint(addr, port);
                            if (socket == null)
                            {
                                socket = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                            }
                            socket.BeginConnect(rep, new AsyncCallback(ReconnCallback), this);
                        }
                        catch { }
                        while (sigReconnectOneDone == false && sigReconnectOneTimeout == false)
                        {
                            Thread.Sleep(100);
                            reconnectOneElapse += 100;
                            if (reconnectOneElapse > 3000)
                            {
                                sigReconnectOneTimeout = true;
                                break;
                            }
                        }
                        if (IsConnected)
                        {
                            sigReconnecting = false;
                            reconnThread = null;
                            break;
                        }
                        else
                        {
                            int waitTime = reconnectInterval - reconnectOneElapse;
                            waitTime = waitTime <= 0 ? 0 : waitTime;
                            if (waitTime > 0)
                            {
                                Thread.Sleep(waitTime);
                            }
                        }
                    }
                });
                reconnThread.Start();
            }
        }

        public virtual void Disconnect()
        {
            ReleaseSocket();
            Callback(CallbackType.OnDisconnected);
        }

        public virtual void Send(NetPacket packet)
        {
            Loom.QueueInMainThread(() =>
            {
                if (sigSocketReleased)
                {
                    if (!sigReconnecting)
                    {
                        error = "Socket has already been released.";
                        ErrorOccurred();
                    }
                }
                else
                {
                    try
                    {
                        socket.BeginSend(packet.Bytes, 0, packet.Length, SocketFlags.None, new AsyncCallback(SendCallback), packet);
                    }
                    catch (Exception e)
                    {
                        error = e.Message;
                        ErrorOccurred();
                    }
                }
            });
        }

        protected virtual void Callback(CallbackType type, object param = null)
        {
            Loom.QueueInMainThread(() =>
            {
                StatusDelegate func = null;
                switch (type)
                {
                    case CallbackType.OnConnected:
                        func = onConnected;
                        break;
                    case CallbackType.OnDisconnected:
                        func = onDisconnected;
                        break;
                    case CallbackType.OnReconnected:
                        func = onReconnected;
                        break;
                    case CallbackType.OnErrorrOccurred:
                        func = onErrorOccurred;
                        break;
                    default:
                        break;
                }
                if (func != null)
                {
                    func(this, param);
                }
            });
        }

        protected virtual void ReleaseSocket()
        {
            sigSocketReleased = true;
            try
            {
                if (socket != null)
                {
                    if (IsConnected)
                    {
                        socket.Shutdown(SocketShutdown.Both);
                    }
                    socket.Close();
                }
                if (connThread != null)
                {
                    connThread.Abort();
                    connThread = null;
                }
                if (reconnThread != null)
                {
                    reconnThread.Abort();
                    reconnThread = null;
                }
            }
            catch { }
            finally
            {
                socket = null;
            }
        }

        protected virtual void ErrorOccurred()
        {
            ReleaseSocket();
            if (sigReconnecting == false)
            {
                Callback(CallbackType.OnErrorrOccurred, error);
            }
        }

        protected virtual void ConnCallback(IAsyncResult result)
        {
            connThread = null;
            try
            {
                socket.EndConnect(result);
                sigSocketReleased = false;
                StartReceive();
                Callback(CallbackType.OnConnected);
            }
            catch (Exception e)
            {
                error = e.Message;
                ErrorOccurred();
            }
        }

        protected virtual void ReconnCallback(IAsyncResult result)
        {
            sigReconnectOneDone = true;
            try
            {
                socket.EndConnect(result);
                sigSocketReleased = false;
                StartReceive();
                Callback(CallbackType.OnReconnected);
            }
            catch { }
        }

        protected virtual void StartReceive()
        {
            socket.BeginReceive(receiveHeader, 0, NetPacket.HEAD_LENGTH, SocketFlags.None, new AsyncCallback(RecvCallback), this);
        }

        protected virtual void RecvCallback(IAsyncResult ret)
        {
            if (sigSocketReleased == false)
            {
                if (socket != null)
                {
                    try
                    {
                        int bytesRead = socket.EndReceive(ret);
                        while (bytesRead < NetPacket.HEAD_LENGTH)
                        {
                            bytesRead += socket.Receive(receiveHeader, bytesRead, NetPacket.HEAD_LENGTH - bytesRead, SocketFlags.None);
                        }
                        if (bytesRead == NetPacket.HEAD_LENGTH)
                        {
                            if (NetPacket.Validate(receiveHeader))
                            {
                                int id = BitConverter.ToInt32(receiveHeader, NetPacket.ID_OFFSET);
                                int packetLength = BitConverter.ToInt32(receiveHeader, NetPacket.LENGTH_OFFSET);
                                int bodyLength = packetLength - NetPacket.HEAD_LENGTH;
                                NetPacket packet = new NetPacket(id, bodyLength);
                                packet.Head = receiveHeader;
                                if (bodyLength > 0)
                                {
                                    bytesRead = socket.Receive(packet.Body, 0, packet.BodyLength, SocketFlags.None);
                                    while (bytesRead < packet.BodyLength)
                                    {
                                        bytesRead += socket.Receive(packet.Body, bytesRead, packet.BodyLength - bytesRead, SocketFlags.None);
                                    }
                                }
                                Loom.QueueInMainThread(() =>
                                {
                                    NetManager.NotifyMsg(new Evt() { ID = packet.ID, Param = packet.Body });
                                });
                                StartReceive();
                            }
                            else
                            {
                                error = "packet is not validity";
                                ErrorOccurred();
                            }
                        }
                        else
                        {
                            error = "packet header read error,size is " + bytesRead;
                            ErrorOccurred();
                        }
                    }
                    catch (Exception e)
                    {
                        error = e.Message;
                        ErrorOccurred();
                    }
                }
                else
                {
                    // socket has been released.
                }
            }
        }

        protected virtual void SendCallback(IAsyncResult result)
        {
            if (sigSocketReleased == false)
            {
                int bytesSent = 0;
                try
                {
                    bytesSent = socket.EndSend(result);
                }
                catch (Exception e)
                {
                    error = e.Message;
                    error += ",bytes sent = " + bytesSent;
                    ErrorOccurred();
                }
            }
        }
    }
}