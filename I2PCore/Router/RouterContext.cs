﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using I2PCore.Data;
using System.Net;
using System.IO;
using I2PCore.Utils;
using I2PCore.Transport.SSU;
using System.Net.Sockets;
using I2PCore.Transport.SSU.Data;

// Todo list for all of I2PCore
// TODO: I need to test my tunnels, because the cascade of tunnel losses, is probably due to really slow or faulty return/out tunnels.
// TODO: Add leases
// TODO: DestinationStatistics is never deleted
// TODO: NTCP does not close the old listen socket when settings change.
// TODO: Replace FailedToConnectException with return value?
// TODO: Block router delivering A LOT of tunnelbuilds quickly
// TODO: SSU: Add ability to be relay host
// TODO: SSU: Too many MAC check fail in a long session. Am I using the wrong session key from time to time as well?
// TODO: Move DecayingIPBlockFilter to TransportProvider (to be able to use in all transports)
// TODO: IP block lists for incomming connections, NTCP
// TODO: Add transport bandwidth statistics
// TODO: Add tunnel bandwidth statistics
// TODO: Add tunnel bandwidth limiting
// TODO: Replace DateTime.Now with Environment.TickCount for performance reasons where possible
// TODO: Add the cert / key split support for ECDSA_SHA512_P521
// TODO: Add DatabaseLookup query support
// TODO: Add floodfill server support
// TODO: Add SSU PeerTest initiation
// TODO: Change I2PType to stop using List<> as send buffer for I2NP messages. 
//       (Code using ToByteArray() can often use a BufRef or BufRef enum instead.)
// TODO: Implement connection limits (external)
// TODO: Implement bandwidth limits (tunnels)
// TODO: Refactor NTCP state machine and remove Watchdog
// TODO: Add decaying Bloom filters and remove packet duplicates
// TODO: Add IPV6

// Documentation errors:
// SSU introduction: RouterIdentity and running session not needed. itag[0-4] missing on https://geti2p.net/en/docs/transport/ssu

namespace I2PCore.Router
{
    public class RouterContext
    {
        public bool IsFirewalled = true;

        // IP settings
        public IPAddress DefaultExtAddress = null;
        public IPAddress ExtAddress
        {
            get
            {
                if ( UseIpV6 )
                {
                    return Dns.GetHostEntry( Dns.GetHostName() ).AddressList.Where( a => a.AddressFamily == AddressFamily.InterNetworkV6 ).First();
                }
                else
                {
                    if ( UPnpExternalAddressAvailable )
                    {
                        return UPnpExternalAddress;
                    }

                    if ( SSUReportedExternalAddress != null )
                    {
                        return SSUReportedExternalAddress;
                    }

                    if ( DefaultExtAddress != null ) return DefaultExtAddress;

                    return Dns.GetHostEntry( Dns.GetHostName() ).AddressList.Where( a => a.AddressFamily == AddressFamily.InterNetwork ).First();
                }
            }
        }

        public IPAddress Address 
        {
            get
            {
                if ( UseIpV6 )
                {
                    return Dns.GetHostEntry( Dns.GetHostName() ).AddressList.Where( a => a.AddressFamily == AddressFamily.InterNetworkV6 ).First();
                }
                else
                {
                    return Dns.GetHostEntry( Dns.GetHostName() ).AddressList.Where( a => a.AddressFamily == AddressFamily.InterNetwork ).First();
                }
            }
        }

        public int DefaultTCPPort = 12123;
        public int TCPPort
        {
            get
            {
                if ( UPnpExternalTCPPortMapped )
                {
                    return UPnpExternalTCPPort;
                }
                return DefaultTCPPort;
            }
        }

        public int DefaultUDPPort = 12123;
        public int UDPPort
        {
            get
            {
                if ( UPnpExternalUDPPortMapped )
                {
                    return UPnpExternalUDPPort;
                }
                return DefaultUDPPort;
            }
        }

        public bool UPnpExternalAddressAvailable = false;
        public IPAddress UPnpExternalAddress;
        public bool UPnpExternalTCPPortMapped = false;
        public int UPnpExternalTCPPort;
        public bool UPnpExternalUDPPortMapped = false;
        public int UPnpExternalUDPPort;

        public bool UseIpV6 = false;

        public IPAddress SSUReportedExternalAddress;

        public event Action NetworkSettingsChanged;

        // I2P
        public I2PDate Published;
        public I2PCertificate Certificate;
        public I2PPrivateKey PrivateKey;
        public I2PPublicKey PublicKey;

        public I2PSigningPrivateKey PrivateSigningKey;
        public I2PSigningPublicKey PublicSigningKey;

        public I2PRouterIdentity MyRouterIdentity;

        public bool FloodfillEnabled = false;

        // SSU
        public BufLen IntroKey = new BufLen( new byte[32] );

        // Store

        public static string RouterPath
        {
            get
            {
                return Path.GetFullPath( StreamUtils.AppPath );
            }
        }

        public static string GetFullPath( string filename )
        {
            return Path.Combine( RouterPath, filename );
        }

        const string RouterSettingsFile = "Router.bin";

        static RouterContext StaticInstance;
        public static RouterContext Inst
        {
            get
            {
                if ( StaticInstance != null ) return StaticInstance;
                StaticInstance = new RouterContext( RouterSettingsFile );
                return StaticInstance;
            }
        }

        public RouterContext(): this( (I2PCertificate)null )
        {
        }

        public RouterContext( I2PCertificate cert )
        {
            NewIdentity( cert );
        }

        public RouterContext( string filename )
        {
            try
            {
                DebugUtils.LogInformation( "RouterContext: Path: " + RouterPath );
                Load( GetFullPath( filename ) );
            }
            catch ( Exception ex )
            {
                DebugUtils.Log( ex );
                NewIdentity( null );
                Save( RouterSettingsFile );
            }
        }

        private void NewIdentity( I2PCertificate cert )
        {
            Published = new I2PDate( DateTime.UtcNow.AddMinutes( -1 ) );
            Certificate = cert != null ? cert : new I2PCertificate( I2PSigningKey.SigningKeyTypes.EdDSA_SHA512_Ed25519 );
            //Certificate = new I2PCertificate( I2PSigningKey.SigningKeyTypes.EdDSA_SHA512_Ed25519 );
            //Certificate = new I2PCertificate( I2PSigningKey.SigningKeyTypes.ECDSA_SHA256_P256 );
            //Certificate = new I2PCertificate( I2PSigningKey.SigningKeyTypes.ECDSA_SHA384_P384 );
            //Certificate = new I2PCertificate( I2PSigningKey.SigningKeyTypes.DSA_SHA1 );
            PrivateSigningKey = new I2PSigningPrivateKey( Certificate );
            PublicSigningKey = new I2PSigningPublicKey( PrivateSigningKey );

            var keys = I2PPrivateKey.GetNewKeyPair();
            PrivateKey = keys.PrivateKey;
            PublicKey = keys.PublicKey;

            MyRouterIdentity = new I2PRouterIdentity( PublicKey, PublicSigningKey );
            IntroKey.Randomize();
        }

        private void Load( string filename )
        {
            using ( var fs = new FileStream( filename, FileMode.Open, FileAccess.Read ) )
            {
                using ( var ms = new MemoryStream() )
                {
                    byte[] buf = new byte[8192];
                    int len;
                    while ( ( len = fs.Read( buf, 0, buf.Length ) ) != 0 ) ms.Write( buf, 0, len );

                    var reader = new BufRefLen( ms.ToArray() );

                    Certificate = new I2PCertificate( reader );
                    PrivateSigningKey = new I2PSigningPrivateKey( reader, Certificate );
                    PublicSigningKey = new I2PSigningPublicKey( reader, Certificate );

                    PrivateKey = new I2PPrivateKey( reader, Certificate );
                    PublicKey = new I2PPublicKey( reader, Certificate );

                    MyRouterIdentity = new I2PRouterIdentity( reader );
                    Published = new I2PDate( reader );
                    IntroKey = reader.ReadBufLen( 32 );
                }
            }
        }

        public void Save( string filename )
        {
            var fullpath = GetFullPath( filename );

            using ( var fs = new FileStream( fullpath, FileMode.Create, FileAccess.Write ) )
            {
                var dest = new List<byte>();

                Certificate.Write( dest );
                PrivateSigningKey.Write( dest );
                PublicSigningKey.Write( dest );

                PrivateKey.Write( dest );
                PublicKey.Write( dest );

                MyRouterIdentity.Write( dest );
                Published.Write( dest );
                IntroKey.WriteTo( dest );

                var ar = dest.ToArray();
                fs.Write( ar, 0, ar.Length );
            }
        }

        TickCounter MyRouterInfoCacheCreated = TickCounter.MaxDelta;
        I2PRouterInfo MyRouterInfoCache = null;

        public I2PRouterInfo MyRouterInfo
        {
            get
            {
                var cache = MyRouterInfoCache;
                if ( cache != null &&
                    MyRouterInfoCacheCreated.DeltaToNowSeconds < NetDb.RouterInfoExpiryTimeSeconds / 15 )
                {
                    return cache;
                }

                MyRouterInfoCacheCreated.SetNow();

                var caps = new I2PMapping();

                var capsstring = "LPR";
                if ( FloodfillEnabled ) capsstring += "f";

                caps["caps"] = capsstring;

                caps["netId"] = I2PConstants.I2P_NETWORK_ID.ToString();
                caps["coreVersion"] = I2PConstants.PROTOCOL_VERSION;
                caps["router.version"] = I2PConstants.PROTOCOL_VERSION;
                caps["stat_uptime"] = "90m";

                var ntcp = new I2PRouterAddress( ExtAddress, TCPPort, 11, "NTCP" );
                var ssu = new I2PRouterAddress( ExtAddress, UDPPort, 5, "SSU" );
                var addr = new I2PRouterAddress[] { ntcp, ssu };

                var ssucaps = "";
                if ( SSUHost.PeerTestSupported ) ssucaps += "B";
                if ( SSUHost.IntroductionSupported ) ssucaps += "C";

                ssu.Options["caps"] = ssucaps;
                ssu.Options["key"] = FreenetBase64.Encode( IntroKey );
                foreach( var intro in SSUIntroducersInfo )
                {
                    ssu.Options[intro.Key] = intro.Value;
                }

                var result = new I2PRouterInfo(
                    MyRouterIdentity,
                    new I2PDate( DateTime.UtcNow.AddMinutes( -1 ) ), 
                    addr,
                    caps,
                    PrivateSigningKey );

                MyRouterInfoCache = result;

                DebugUtils.Log( "RouterContext: New settings: " + result.ToString() );

                return result;
            }
        }

        public void SSUReportedAddr( IPAddress extaddr )
        {
            if ( extaddr == null ) return;
            if ( SSUReportedExternalAddress != null && SSUReportedExternalAddress.Equals( extaddr ) ) return;

            SSUReportedExternalAddress = extaddr;
            MyRouterInfoCache = null;
        }

        internal void UpnpReportedAddr( string addr )
        {
            if ( UPnpExternalAddressAvailable && UPnpExternalAddress.Equals( IPAddress.Parse( addr ) ) ) return;

            UPnpExternalAddress = IPAddress.Parse( addr );
            UPnpExternalAddressAvailable = true;
            MyRouterInfoCache = null;
        }

        public void ApplyNewSettings()
        {
            MyRouterInfoCache = null;
            if ( NetworkSettingsChanged != null ) NetworkSettingsChanged();
        }

        internal void UpnpNATPortMapAdded( IPAddress addr, string protocol, int port )
        {
            if ( protocol == "TCP" && UPnpExternalTCPPortMapped && UPnpExternalTCPPort == port ) return;
            if ( protocol == "UDP" && UPnpExternalUDPPortMapped && UPnpExternalUDPPort == port ) return;

            if ( protocol == "TCP" )
            {
                UPnpExternalTCPPortMapped = true;
                UPnpExternalTCPPort = port;
            }
            else
            {
                UPnpExternalUDPPortMapped = true;
                UPnpExternalUDPPort = port;
            }
            UPnpExternalAddressAvailable = true;
            MyRouterInfoCache = null;

            ApplyNewSettings();
        }

        List<KeyValuePair<string,string>> SSUIntroducersInfo = new List<KeyValuePair<string, string>>();

        internal void NoIntroducers()
        {
            SSUIntroducersInfo = new List<KeyValuePair<string, string>>();
        }

        internal void SetIntroducers( IEnumerable<IntroducerInfo> introducers )
        {
            var result = new List<KeyValuePair<string, string>>();
            var ix = 0;

            foreach( var one in introducers )
            {
                result.Add( new KeyValuePair<string, string>( $"ihost{ix}", one.Host.ToString() ) );
                result.Add( new KeyValuePair<string, string>( $"iport{ix}", one.Port.ToString() ) );
                result.Add( new KeyValuePair<string, string>( $"ikey{ix}", FreenetBase64.Encode( one.IntroKey ) ) );
                result.Add( new KeyValuePair<string, string>( $"itag{ix}", one.IntroTag.ToString() ) );
                ++ix;
            }

            SSUIntroducersInfo = result;
            MyRouterInfoCache = null;
        }
    }
}
