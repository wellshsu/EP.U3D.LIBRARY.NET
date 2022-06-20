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
using System.Collections;
using System.Net;
using System.Text;
using UnityEngine;

namespace EP.U3D.LIBRARY.NET
{
    public class HttpListener
    {
        public enum Status
        {
            None = 0,
            OK = 1,
            NetworkError = 2,
            HostError = 3,
        }

        public delegate void StatusDelegate(Status last, Status current, WWW www);

        public Status LastStatus;
        public Status CurrentStatus;

        private StatusDelegate handler;
        private string url;
        private float interval;
        private Coroutine cr;

        public HttpListener(string url, float interval, StatusDelegate handler)
        {
            this.url = url;
            this.interval = interval;
            this.handler = handler;
            LastStatus = Status.None;
            CurrentStatus = Status.None;
        }

        public void Start()
        {
            cr = Loom.StartCR(ProcessCheck());
        }

        public void Stop()
        {
            Loom.StopCR(cr);
        }

        private IEnumerator ProcessCheck()
        {
            while (true)
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    SwitchStatus(Status.NetworkError, null);
                    yield return null;
                }
                else
                {
                    int elapseTime = 0;
                    using (WWW www = new WWW(url))
                    {
                        while (www.isDone == false)
                        {
                            yield return new WaitForSeconds(0.1f);
                            elapseTime++;
                            if (elapseTime > 50) // time out. 
                            {
                                SwitchStatus(Status.HostError, null);
                                yield return null;
                            }
                        }
                        yield return new WaitUntil(() => { return www.isDone; });
                        if (string.IsNullOrEmpty(www.error))
                        {
                            SwitchStatus(Status.OK, www);
                        }
                        else
                        {
                            SwitchStatus(Status.HostError, null);
                        }
                    }
                }
                yield return new WaitForSeconds(interval);
            }
        }

        public void SwitchStatus(Status status, WWW www)
        {
            if (status != CurrentStatus)
            {
                LastStatus = CurrentStatus;
                CurrentStatus = status;
                if (handler != null)
                {
                    handler(LastStatus, CurrentStatus, www);
                }
            }
        }
    }
}

