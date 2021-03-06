﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Org.BouncyCastle.Math;
using I2PCore.Utils;

namespace I2PCore.Data
{
    public abstract class I2PKeyType: I2PType
    {
        public BufLen Key;

        public enum KeyTypes : ushort
        {
            ElGamal2048 = 0
        }

        // Replace when ElGamal is optional
        public static readonly I2PCertificate DefaultAsymetricKeyCert = new I2PCertificate();

        public static readonly I2PCertificate DefaultSigningKeyCert = new I2PCertificate();

        public readonly I2PCertificate Certificate;

        public int KeySizeBits { get { return KeySizeBytes * 8; } }
        public abstract int KeySizeBytes { get; }

        protected I2PKeyType( I2PCertificate cert )
        {
            Certificate = cert;
        }

        public I2PKeyType( BufRef buf, I2PCertificate cert )
        {
            Certificate = cert;
            Key = buf.ReadBufLen( KeySizeBytes );
        }

        public void Write( List<byte> dest )
        {
            dest.AddRange( ToByteArray() );
        }

        public byte[] ToByteArray()
        {
            return Key.ToByteArray();
            //return Key.ToByteArray( KeySizeBytes );
        }

        public BigInteger ToBigInteger()
        {
            return new BigInteger( 1, Key.ToByteArray() );
        }

        public override string ToString()
        {
            var result = new StringBuilder();

            result.AppendLine( "I2PKeyType - " + base.ToString() );
            result.AppendLine( "Key : " + KeySizeBits + " bits, " + KeySizeBytes + " bytes." );
            result.AppendLine( "Key : " + Key.ToString() );

            return result.ToString();
        }
    }
}
