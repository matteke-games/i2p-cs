﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using I2PCore.Data;
using I2PCore.Router;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using I2PCore.Utils;
using System.Net.Sockets;

namespace I2PCore.Transport.NTCP
{
    public class DHHandshakeContext
    {
        public NTCPClient Client;
        public NTCPRunningContext RunContext;

        public CbcBlockCipher Encryptor;
        public CbcBlockCipher Dectryptor;

        public I2PPrivateKey PrivateKey;

        public BufLen XBuf;
        public I2PPublicKey X;
        public BufLen HXxorHI;

        public BufLen YBuf;
        public I2PPublicKey Y;
        public BufLen HXY;

        public I2PKeysAndCert RemoteRI;
        public uint TimestampA;
        public uint TimestampB;

        public I2PSessionKey SessionKey;

        public DHHandshakeContext( NTCPClient client )
        {
            Client = client;
        }
    }
}
