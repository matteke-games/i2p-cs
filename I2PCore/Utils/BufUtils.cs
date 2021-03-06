﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Math;
using I2PCore.Data;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace I2PCore.Utils
{
    public static class BufUtils
    {
        #region Endian conversion
        public static ulong Flip64( ulong src )
        {
            ulong result;
            result = src & 0xff;
            result = ( result << 8 ) | ( ( src & 0xff00 ) >> 8 );
            result = ( result << 8 ) | ( ( src & 0xff0000 ) >> 16 );
            result = ( result << 8 ) | ( ( src & 0xff000000 ) >> 24 );
            result = ( result << 8 ) | ( ( src & 0xff00000000 ) >> 32 );
            result = ( result << 8 ) | ( ( src & 0xff0000000000 ) >> 40 );
            result = ( result << 8 ) | ( ( src & 0xff000000000000 ) >> 48 );
            result = ( result << 8 ) | ( src >> 56 );
            return result;
        }

        public static uint Flip32( uint src )
        {
            uint result;
            result = src & 0xff;
            result = ( result << 8 ) | ( ( src & 0xff00 ) >> 8 );
            result = ( result << 8 ) | ( ( src & 0xff0000 ) >> 16 );
            result = ( result << 8 ) | ( src >> 24 );
            return result;
        }

        public static uint Flip32( byte[] buf, int offset )
        {
            return Flip32( BitConverter.ToUInt32( buf, offset ) );
        }


        public static ushort Flip16( ushort src )
        {
            return (ushort)( ( ( src & 0xff ) << 8 ) | ( src >> 8 ) );
        }

        public static ushort Flip16( byte[] buf, int offset )
        {
            return Flip16( BitConverter.ToUInt16( buf, offset ) );
        }

        public static byte[] Flip64B( ulong src )
        {
            return BitConverter.GetBytes( Flip64( src ) );
        }

        public static byte[] Flip32B( uint src )
        {
            return BitConverter.GetBytes( Flip32( src ) );
        }

        public static BufLen Flip32BL( uint src )
        {
            return new BufLen( Flip32B( src ) );
        }

        public static byte[] Flip16B( ushort src )
        {
            return BitConverter.GetBytes( Flip16( src ) );
        }

        public static BufLen Flip16BL( ushort src )
        {
            return new BufLen( Flip16B( src ) );
        }
        #endregion

        #region Data structures to byte array

        public static byte[] DHI2PToByteArray( BigInteger bi )
        {
            var result = new List<byte>();

            if ( bi == BigInteger.Zero ) throw new FormatException( "BigInteger == 0 for DH key!" );

            result.AddRange( bi.ToByteArray() );

            if ( ( result[0] & 0x80 ) != 0 )
            {
                result.Insert( 0, 0 );
            }

            while ( result.Count < 32 ) result.Add( 0 );

            return result.Take( 32 ).ToArray();
        }

        public static void DHI2PToSessionAndMAC( out BufLen sessionkey, out BufLen mackey, BigInteger bi )
        {
            var result = new List<byte>();

            if ( bi == BigInteger.Zero ) throw new FormatException( "BigInteger == 0 for DH key!" );

            result.AddRange( bi.ToByteArray() );

            if ( ( result[0] & 0x80 ) != 0 )
            {
                result.Insert( 0, 0 );
            }

            while ( result.Count < 32 ) result.Add( 0 );

            sessionkey = new BufLen( result.Take( 32 ).ToArray() );

            if ( result.Count >= 64 )
            {
                mackey = new BufLen( result.Skip( 32 ).Take( 32 ).ToArray() );
            }
            else
            {
                mackey = new BufLen( I2PHashSHA256.GetHash( result.ToArray() ) );
            }
        }

        public static byte[] ToByteArray( this BigInteger bi, int length )
        {
            var ar = bi.ToByteArrayUnsigned();
            if ( ar.Length == length )
            {
                return ar;
            }

            var result = new byte[length];
            if ( ar.Length == 0 ) return result;

            if ( ar.Length > length ) throw new OverflowException( "BigInteger does not fit in buffer!" );
            Array.Copy( ar, 0, result, length - ar.Length, ar.Length );

            return result;
        }

        public static byte[] ToByteArray( this I2PType data )
        {
            var buf = new List<byte>();
            data.Write( buf );
            return buf.ToArray();
        }

        public static byte[] ToByteArray( params I2PType[] fields )
        {
            var buf = new List<byte>();
            foreach( var one in fields ) one.Write( buf );
            return buf.ToArray();
        }
        #endregion

        #region Byte array operations

        public static bool Equal( byte[] b1, byte[] b2 )
        {
            if ( b1.Length != b2.Length ) return false;

            for ( int i = 0; i < b1.Length; ++i ) if ( b1[i] != b2[i] ) return false;
            return true;
        }

        public static bool Equal( byte[] b1, int b1offset, byte[] b2, int b2offset, int length )
        {
            for ( int i = 0; i < length; ++i ) if ( b1[i+b1offset] != b2[i+b2offset] ) return false;
            return true;
        }

        public static byte[] Copy( this byte[] b1, int offset, int length )
        {
            var buf = new byte[length];
            Array.Copy( b1, offset, buf, 0, length );
            return buf;
        }

        public static byte[] Copy( this byte[] b1, ref int offset, int length )
        {
            var result = b1.Copy( offset, length );
            offset += length;
            return result;
        }

        #endregion

        #region Crypto

        static BufferedBlockCipher AesEcbCipher = new BufferedBlockCipher( new AesEngine() );

        public static void AesEcbEncrypt( this BufLen buf, byte[] key )
        {
            AesEcbCipher.Init( true, new KeyParameter( key ) );
            AesEcbCipher.ProcessBytes( buf );
        }

        public static void AesEcbEncrypt( this BufLen buf, BufLen key )
        {
            AesEcbCipher.Init( true, new KeyParameter( key.BaseArray, key.BaseArrayOffset, key.Length ) );
            AesEcbCipher.ProcessBytes( buf );
        }

        public static void AesEcbDecrypt( this BufLen buf, byte[] key )
        {
            AesEcbCipher.Init( false, new KeyParameter( key ) );
            AesEcbCipher.ProcessBytes( buf );
        }

        public static void AesEcbDecrypt( this BufLen buf, BufLen key )
        {
            AesEcbCipher.Init( false, new KeyParameter( key.BaseArray, key.BaseArrayOffset, key.Length ) );
            AesEcbCipher.ProcessBytes( buf );
        }

        public static void Encrypt( this BufferedBlockCipher cipher, byte[] key, BufLen iv, BufLen data )
        {
            cipher.Init( true, new ParametersWithIV( new KeyParameter( key ), iv.BaseArray, iv.BaseArrayOffset, iv.Length ) );
            cipher.ProcessBytes( data );
        }

        public static void Decrypt( this BufferedBlockCipher cipher, byte[] key, BufLen iv, BufLen data )
        {
            cipher.Init( false, new ParametersWithIV( new KeyParameter( key ), iv.BaseArray, iv.BaseArrayOffset, iv.Length ) );
            cipher.ProcessBytes( data );
        }

        public static void Encrypt( this BufferedBlockCipher cipher, BufLen key, BufLen iv, BufLen data )
        {
            cipher.Init( true, new ParametersWithIV( new KeyParameter( key.BaseArray, key.BaseArrayOffset, key.Length ), iv.BaseArray, iv.BaseArrayOffset, iv.Length ) );
            cipher.ProcessBytes( data );
        }

        public static void Decrypt( this BufferedBlockCipher cipher, BufLen key, BufLen iv, BufLen data )
        {
            cipher.Init( false, new ParametersWithIV( new KeyParameter( key.BaseArray, key.BaseArrayOffset, key.Length ), iv.BaseArray, iv.BaseArrayOffset, iv.Length ) );
            cipher.ProcessBytes( data  );
        }

        public static void Encrypt( this CbcBlockCipher cipher, BufLen key, BufLen iv, BufLen data )
        {
            cipher.Init( true, key.ToParametersWithIV( iv ) );
            cipher.ProcessBytes( data );
        }

        public static void Decrypt( this CbcBlockCipher cipher, BufLen key, BufLen iv, BufLen data )
        {
            cipher.Init( false, key.ToParametersWithIV( iv ) );
            cipher.ProcessBytes( data );
        }

        public static void ProcessBytes( this BufferedBlockCipher cipher, BufLen data )
        {
            cipher.ProcessBytes( data.BaseArray, data.BaseArrayOffset, data.Length, data.BaseArray, data.BaseArrayOffset );
        }

        public static void ProcessBytes( this CbcBlockCipher cipher, BufLen data )
        {
            if ( Get16BytePadding( data.Length ) != 0 ) throw new ArgumentException( "Cbc needs blocks of 16 bytes!" );

            var end = data.Length / 16;
            for ( int i = 0; i < end; ++i ) cipher.ProcessBlock(
                data.BaseArray, data.BaseArrayOffset + i * 16,
                data.BaseArray, data.BaseArrayOffset + i * 16 );
        }

        public static KeyParameter ToKeyParameter( this BufLen key )
        {
            return new KeyParameter( key.BaseArray, key.BaseArrayOffset, key.Length );
        }

        public static ParametersWithIV ToParametersWithIV( this BufLen key, BufLen iv )
        {
            return new ParametersWithIV( key.ToKeyParameter(), iv.BaseArray, iv.BaseArrayOffset, iv.Length );
        }
        #endregion

        #region Misc often needed utils

        static SecureRandom Rnd = new SecureRandom();

        public static void Randomize( this byte[] buf, int offset, int length )
        {
            Rnd.NextBytes( buf, offset, length );
        }

        public static void Randomize( this byte[] buf )
        {
            Rnd.NextBytes( buf );
        }

        public static void Randomize( this BufRefLen buf )
        {
            Rnd.NextBytes( buf.BaseArray, buf.BaseArrayOffset, buf.Length );
        }

        public static void Randomize( this BufLen buf )
        {
            Rnd.NextBytes( buf.BaseArray, buf.BaseArrayOffset, buf.Length );
        }

        public static V Random<K,V>( this IDictionary<K,V> dic )
        {
            return dic[dic.Keys.Skip( BufUtils.RandomInt( dic.Keys.Count - 1 ) ).Take( 1 ).SingleOrDefault()];
        }

        public static T Random<T>( this IEnumerable<T> src )
        {
            return src.Skip( RandomInt( src.Count() ) ).Take( 1 ).SingleOrDefault();
        }

        static readonly byte[] ZeroArray = new byte[0];
        public static byte[] Random( int bytes )
        {
            if ( bytes == 0 ) return ZeroArray;

            byte[] buf = new byte[bytes];
            Rnd.NextBytes( buf );
            return buf;
        }

        public static byte[] RandomNZ( int bytes )
        {
            byte[] buf = new byte[bytes];
            Rnd.NextBytes( buf );
            for ( int i = 0; i < buf.Length; ++i ) if ( buf[i] == 0 ) buf[i] = (byte)( ( RandomUint() | 0x01 ) & 0xFF );
            return buf;
        }

        public static uint RandomUint()
        {
            return (uint)Rnd.Next();
        }

        public static int RandomInt( int max )
        {
            return Rnd.Next( max );
        }

        public static float RandomFloat( float max )
        {
            return (float)( Rnd.NextDouble() * max );
        }

        public static void Populate<T>( this IList<T> list, T value )
        {
            for ( int i = 0; i < list.Count; ++i ) list[i] = value;
        }

        public static IEnumerable<T> Populate<T>( T value, int count )
        {
            for ( int i = 0; i < count; ++i ) yield return value;
        }

        public static IEnumerable<T> Populate<T>( Func<T> gen, int count )
        {
            for ( int i = 0; i < count; ++i ) yield return gen();
        }

        static IEnumerable<IEnumerable<T>> CartesianProduct<T>(
          this IEnumerable<IEnumerable<T>> sequences )
        {
            IEnumerable<IEnumerable<T>> Empty = new[] { Enumerable.Empty<T>() };
            return sequences.Aggregate(
              Empty,
              ( accumulator, sequence ) =>
                from accseq in accumulator
                from item in sequence
                select accseq.Concat( new[] { item } ) );
        }

        public static void Shuffle<T>( this IList<T> list )
        {
            for ( int i = 0; i < list.Count; i++ )
            {
                var tmp = list[i];
                var ix = i + RandomInt( list.Count - i );
                list[i] = list[ix];
                list[ix] = tmp;
            } 
        }

        public static IEnumerable<T> Shuffle<T>( this IEnumerable<T> source )
        {
            var list = source.ToList();
            for ( int i = 0; i < list.Count; ++i )
            {
                var ix = i + RandomInt( list.Count - i );
                yield return list[ix];
                list[ix] = list[i];
            }
        }
 
        public static float StdDev<T>( this IEnumerable<T> list, Func<T,float> select )
        {
            var result = 0f;
            var avg = list.Average( v => select( v ) );
            var count = 0;
            foreach ( var one in list )
            {
                var v = select( one ) - avg;
                result += v * v;
                ++count;
            }
            return (float)Math.Sqrt( ( 1f / ( count - 1 ) ) * result );
        }

        public static float CentMovem3<T>( this IEnumerable<T> list, Func<T, float> select )
        {
            var result = 0f;
            var avg = list.Average( v => select( v ) );
            var count = 0;
            foreach ( var one in list )
            {
                var v = select( one ) - avg;
                result += v * v * v;
                ++count;
            }
            return (float)( ( 1f / count ) * result );
        }

        public static float Skew<T>( this IEnumerable<T> list, Func<T, float> select )
        {
            return (float)( list.CentMovem3( v => select( v ) ) / Math.Pow( list.StdDev( v => select( v ) ), 3 ) );
        }

        public struct HistogramBin
        {
            public float Start;
            public float Count;
        }

        public static HistogramBin[] Histogram<T>( this IEnumerable<T> list, Func<T, float> select, int bins )
        {
            var result = new HistogramBin[bins];
            var min = list.Min( v => select( v ) );
            var max = list.Max( v => select( v ) );
            if ( min == max ) return result;

            for ( int i = 0; i < bins; ++i ) result[i].Start = i * ( ( max - min ) / bins ) + min;

            foreach ( var one in list )
            {
                var ix = Math.Min( result.Length - 1, (int)( ( bins * ( select( one ) - min ) ) / ( max - min ) ) );
                ++result[ix].Count;
            }
            return result;
        }

        public static int ComputeHash( this byte[] data )
        {
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                for ( int i = 0; i < data.Length; ++i )
                    hash = ( hash ^ data[i] ) * p;

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        public static int Get16BytePadding( int responselength )
        {
            return 0xF & ( 0x10 - ( responselength & 0xF ) );
        }

        public static void RemoveAll<T>( this LinkedList<T> list, Func<T, bool> deleteeval )
        {
            var one = list.First;
            while ( one != null )
            {
                var next = one.Next;

                if ( deleteeval( one.Value ) )
                {
                    list.Remove( one );
                }

                one = next;
            }
        }

        #endregion

        #region Encodings

        public static byte[] Base32ToByteArray( string input )
        {
            if ( string.IsNullOrEmpty( input ) )
            {
                throw new ArgumentException( "Null input" );
            }

            input = input.TrimEnd( '=' );
            int bytecount = input.Length * 5 / 8;
            byte[] result = new byte[bytecount];

            byte curbyte = 0, bitsremaining = 8;
            int mask = 0, ix = 0;

            foreach( char c in input )
            {
                int cval = Base32CharToInt( c );

                if ( bitsremaining > 5 )
                {
                    mask = cval << ( bitsremaining - 5 );
                    curbyte = (byte)( curbyte | mask );
                    bitsremaining -= 5;
                }
                else
                {
                    mask = cval >> ( 5 - bitsremaining );
                    curbyte = (byte)( curbyte | mask );
                    result[ix++] = curbyte;
                    curbyte = (byte)( cval << ( 3 + bitsremaining ) );
                    bitsremaining += 3;
                }
            }

            if ( ix != bytecount )
            {
                result[ix] = curbyte;
            }

            return result;
        }

        public static string ToBase32String( byte[] input )
        {
            return ToBase32String( new BufLen( input ) );
        }

        public static string ToBase32String( BufLen input )
        {
            if ( input == null || input.Length == 0 )
            {
                return "";
            }

            StringBuilder result = new StringBuilder();
            int charcount = (int)Math.Ceiling( input.Length / 5d ) * 8;

            byte nextchar = 0;
            byte bitsremaining = 5;

            foreach ( byte b in input )
            {
                nextchar = (byte)( nextchar | ( b >> ( 8 - bitsremaining ) ) );
                result.Append( ToBase32Char( nextchar ) );

                if ( bitsremaining < 4 )
                {
                    nextchar = (byte)( ( b >> ( 3 - bitsremaining ) ) & 31 );
                    result.Append( ToBase32Char( nextchar ) );
                    bitsremaining += 5;
                }

                bitsremaining -= 3;
                nextchar = (byte)( ( b << bitsremaining ) & 31 );
            }

            if ( result.Length != charcount )
            {
                result.Append( ToBase32Char( nextchar ) );
                //while ( result.Length != charcount ) result.Append( '=' );
            }

            return result.ToString();
        }

        private static int Base32CharToInt( char c )
        {
            int value = (int)c;

            // 65-90 == uppercase letters
            if ( value < 91 && value > 64 )
            {
                return value - 65;
            }
            // 50-55 == numbers 2-7
            if ( value < 56 && value > 49 )
            {
                return value - 24;
            }
            // 97-122 == lowercase letters
            if ( value < 123 && value > 96 )
            {
                return value - 97;
            }

            throw new ArgumentException( "Character is not a Base32 character.", "c" );
        }

        private static char ToBase32Char( byte b )
        {
            if ( b < 26 )
            {
                return (char)( b + 97 );
            }

            if ( b < 32 )
            {
                return (char)( b + 24 );
            }

            throw new ArgumentException( "Byte is not a value Base32 value.", "b" );
        }
        #endregion
    }
}
