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

namespace EP.U3D.LIBRARY.NET
{
    public class NetPacket
    {
        private int id;
        private byte[] head;
        private byte[] body;

        public const int VERSION = 1;
        public const int HEAD_LENGTH = 19;

        public const int VERSION_OFFSET = 2;
        public const int LENGTH_OFFSET = 3;
        public const int ID_OFFSET = 7;
        public const int PLAYERID_OFFSET = 11;
        public const int SERVERID_OFFSET = 15;
        public const int MESSAGE_OFFSET = 19;

        public NetPacket(int id, int bodyLength)
        {
            this.id = id;
            head = new byte[HEAD_LENGTH];
            head[0] = 8;
            head[1] = 8;

            byte[] bytes = BitConverter.GetBytes(VERSION);
            Array.Copy(bytes, 0, head, VERSION_OFFSET, bytes.Length);

            bytes = BitConverter.GetBytes(bodyLength + HEAD_LENGTH);
            Array.Copy(bytes, 0, head, LENGTH_OFFSET, bytes.Length);

            bytes = BitConverter.GetBytes(this.id);
            Array.Copy(bytes, 0, head, ID_OFFSET, bytes.Length);

            if (bodyLength < 0) bodyLength = 0;
            body = new byte[bodyLength];
        }

        public static bool Validate(byte[] bytes)
        {
            if (bytes.Length != HEAD_LENGTH)
            {
                return false;
            }
            if (bytes[0] != 8 || bytes[1] != 8)
            {
                return false;
            }
            return true;
        }

        public int ID { get { return id; } }

        public int PlayerID
        {
            set
            {
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Copy(bytes, 0, head, PLAYERID_OFFSET, bytes.Length);
            }
            get
            {
                int data = BitConverter.ToInt32(head, PLAYERID_OFFSET);
                return data;
            }
        }

        public int ServerID
        {
            get
            {
                int data = BitConverter.ToInt32(head, PLAYERID_OFFSET);
                return data;
            }
            set
            {
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Copy(bytes, 0, head, SERVERID_OFFSET, bytes.Length);
            }
        }

        public byte[] Head
        {
            set
            {
                if (value == null || value.Length < HEAD_LENGTH)
                {
                    return;
                }
                else
                {
                    Array.Copy(value, 0, head, 0, HEAD_LENGTH);
                }
            }
            get
            {
                return head;
            }
        }

        public byte[] Body
        {
            set
            {
                body = value;
            }
            get
            {
                return body;
            }
        }

        public byte[] Bytes
        {
            get
            {
                byte[] buffer = new byte[Length];
                Array.Copy(head, 0, buffer, 0, head.Length);
                Array.Copy(body, 0, buffer, head.Length, body.Length);
                return buffer;
            }
        }

        public int Length
        {
            get
            {
                if (body == null)
                {
                    return HEAD_LENGTH;
                }
                else
                {
                    return body.Length + HEAD_LENGTH;
                }
            }
        }

        public int BodyLength
        {
            get
            {
                if (body == null)
                {
                    return 0;
                }
                else
                {
                    return body.Length;
                }
            }
        }

    }
}