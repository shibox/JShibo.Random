using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JShibo.Random
{
    /// <summary>
    ///     高性能的随机数生成类，平均比系统的要快8倍
    ///     
    ///                                     System.Random               FastRandom
    ///     方法                            (millions calls/sec)       (millions calls/sec)       Speed increase
    ///     Next()                          103.252                     220.750                   2.14x  
    ///     Next(int)                       51.826                      142.247                   2.14x
    ///     Next(int,int)                   34.506                      87.680                    2.54x
    ///     Next(int,int)                   16.182                      30.261                    1.87x
    ///     NextDouble()                    87.680                      185.528                   2.12x
    ///     NextBytes() 1024byte            0.105                       0.927                     8.83x   
    ///     NextUInt()                      n/a                         261.437                   n/a
    ///     NextInt()                       n/a                         256.081                   n/a
    ///     NextBool()                      n/a                         312.500                   n/a
    /// 
    /// </summary>
    public sealed class ShiboRandom
    {
        #region 字段

        const string RAND_LETTER = "abcdefghijklmnopqrstuvwxyz_ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        const double DOUBLE_UNIT_INT = 1.0 / ((double)int.MaxValue + 1.0);
        const double DOUBLE_UNIT_UINT = 1.0 / ((double)uint.MaxValue + 1.0);
        const float FLOAT_UNIT_INT = 1.0F / ((float)int.MaxValue + 1.0F);
        const float FLOAT_UNIT_UINT = 1.0F / ((float)uint.MaxValue + 1.0F);
        const uint Y = 842502087, Z = 3579807591, W = 273326509;
        static int rotateSeed = 0;
        static object locker = new object();
        static ShiboRandom Instance = new ShiboRandom();

        uint x, y, z, w;
        uint bitBuffer;
        uint bitMask = 1;

        #endregion

        #region 构造函数

        public ShiboRandom()
            : this(Guid.NewGuid().GetHashCode())
        {
        }

        /// <summary>
        /// 随机数的种子
        /// </summary>
        /// <param name="seed"></param>
        public ShiboRandom(int seed)
        {
            x = (uint)seed;
            y = Y;
            z = Z;
            w = W;
        }

        #endregion

        #region 内部用的

        internal int InternalNext(int maxValue)
        {
            //if (maxValue < 0)
            //    throw new ArgumentOutOfRangeException("maxValue", maxValue, "maxValue must be >=0");

            uint t = (x ^ (x << 11));
            x = y; y = z; z = w;
            return (int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * maxValue);
        }

        internal int InternalNext(int minValue, int maxValue)
        {
            //if (minValue > maxValue)
            //    throw new ArgumentOutOfRangeException("upperBound", maxValue, "upperBound must be >=lowerBound");

            uint t = (x ^ (x << 11));
            x = y; y = z; z = w;

            // The explicit int cast before the first multiplication gives better performance.
            // See comments in NextDouble.
            int range = maxValue - minValue;
            if (range < 0)
            {	// If range is <0 then an overflow has occured and must resort to using long integer arithmetic instead (slower).
                // We also must use all 32 bits of precision, instead of the normal 31, which again is slower.	
                return minValue + (int)((DOUBLE_UNIT_UINT * (double)(w = (w ^ (w >> 19)) ^ (t ^ (t >> 8)))) * (double)((long)maxValue - (long)minValue));
            }

            // 31 bits of precision will suffice if range<=int.MaxValue. This allows us to cast to an int and gain
            // a little more performance.
            return minValue + (int)((DOUBLE_UNIT_INT * (double)(int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * (double)range);
        }

        /// <summary>
        /// 用随机数填充指定字节数组的元素。
        /// </summary>
        /// <param name="buffer">包含随机数的字节数组。</param>
        private void InternalNextBytes(byte[] buffer)
        {
            // Fill up the bulk of the buffer in chunks of 4 bytes at a time.
            uint x = this.x, y = this.y, z = this.z, w = this.w;
            int i = 0;
            uint t;
            for (int bound = buffer.Length - 3; i < bound;)
            {
                // Generate 4 bytes. 
                // Increased performance is achieved by generating 4 random bytes per loop.
                // Also note that no mask needs to be applied to zero out the higher order bytes before
                // casting because the cast ignores thos bytes. Thanks to Stefan Trosch黷z for pointing this out.
                t = (x ^ (x << 11));
                x = y; y = z; z = w;
                w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                buffer[i++] = (byte)w;
                buffer[i++] = (byte)(w >> 8);
                buffer[i++] = (byte)(w >> 16);
                buffer[i++] = (byte)(w >> 24);
            }

            // Fill up any remaining bytes in the buffer.
            if (i < buffer.Length)
            {
                // Generate 4 bytes.
                t = (x ^ (x << 11));
                x = y; y = z; z = w;
                w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                buffer[i++] = (byte)w;
                if (i < buffer.Length)
                {
                    buffer[i++] = (byte)(w >> 8);
                    if (i < buffer.Length)
                    {
                        buffer[i++] = (byte)(w >> 16);
                        if (i < buffer.Length)
                        {
                            buffer[i] = (byte)(w >> 24);
                        }
                    }
                }
            }
            this.x = x; this.y = y; this.z = z; this.w = w;
        }

        private void InternalNextBytes(byte[] buffer, int offset, int count)
        {
            // Fill up the bulk of the buffer in chunks of 4 bytes at a time.
            uint x = this.x, y = this.y, z = this.z, w = this.w;
            int i = offset;
            uint t;
            for (int bound = offset + count - 3; i < bound;)
            {
                // Generate 4 bytes. 
                // Increased performance is achieved by generating 4 random bytes per loop.
                // Also note that no mask needs to be applied to zero out the higher order bytes before
                // casting because the cast ignores thos bytes. Thanks to Stefan Trosch黷z for pointing this out.
                t = (x ^ (x << 11));
                x = y; y = z; z = w;
                w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                buffer[i++] = (byte)w;
                buffer[i++] = (byte)(w >> 8);
                buffer[i++] = (byte)(w >> 16);
                buffer[i++] = (byte)(w >> 24);
            }

            // Fill up any remaining bytes in the buffer.
            if (i < offset + count)
            {
                // Generate 4 bytes.
                t = (x ^ (x << 11));
                x = y; y = z; z = w;
                w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                buffer[i++] = (byte)w;
                if (i < buffer.Length)
                {
                    buffer[i++] = (byte)(w >> 8);
                    if (i < buffer.Length)
                    {
                        buffer[i++] = (byte)(w >> 16);
                        if (i < buffer.Length)
                        {
                            buffer[i] = (byte)(w >> 24);
                        }
                    }
                }
            }
            this.x = x; this.y = y; this.z = z; this.w = w;
        }

        #endregion

        #region 获得随机数 基本类型

        /// <summary>
        /// 返回非负随机数。
        /// </summary>
        /// <returns></returns>
        public int Next()
        {
            uint t = (x ^ (x << 11));
            x = y; y = z; z = w;
            w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

            uint rtn = w & 0x7FFFFFFF;
            if (rtn == 0x7FFFFFFF)
                return Next();
            return (int)rtn;
        }

        /// <summary>
        ///  返回一个小于所指定最大值的非负随机数。
        /// </summary>
        /// <param name="maxValue">要生成的随机数的上限（随机数不能取该上限值）。maxValue 必须大于或等于零。</param>
        /// <returns>大于等于零且小于 maxValue 的 32 位带符号整数，即：返回值的范围通常包括零但不包括 maxValue。不过，如果 maxValue 等于零，则返回maxValue。</returns>
        public int Next(int maxValue)
        {
            //if (maxValue < 0)
            //    throw new ArgumentOutOfRangeException("maxValue", maxValue, "maxValue must be >=0");

            uint t = (x ^ (x << 11));
            x = y; y = z; z = w;
            return (int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * maxValue);
        }

        /// <summary>
        /// 返回一个指定范围内的随机数。
        /// </summary>
        /// <param name="minValue">返回的随机数的下界（随机数可取该下界值）。</param>
        /// <param name="maxValue">返回的随机数的上界（随机数不能取该上界值）。maxValue 必须大于或等于 minValue。</param>
        /// <returns>一个大于等于 minValue 且小于 maxValue 的 32 位带符号整数，即：返回的值范围包括 minValue 但不包括 maxValue。如果minValue 等于 maxValue，则返回 minValue。</returns>
        public int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException("upperBound", maxValue, "upperBound must be >=lowerBound");

            uint t = (x ^ (x << 11));
            x = y; y = z; z = w;

            // The explicit int cast before the first multiplication gives better performance.
            // See comments in NextDouble.
            int range = maxValue - minValue;
            if (range < 0)
            {	// If range is <0 then an overflow has occured and must resort to using long integer arithmetic instead (slower).
                // We also must use all 32 bits of precision, instead of the normal 31, which again is slower.	
                return minValue + (int)((DOUBLE_UNIT_UINT * (double)(w = (w ^ (w >> 19)) ^ (t ^ (t >> 8)))) * (double)((long)maxValue - (long)minValue));
            }

            // 31 bits of precision will suffice if range<=int.MaxValue. This allows us to cast to an int and gain
            // a little more performance.
            return minValue + (int)((DOUBLE_UNIT_INT * (double)(int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * (double)range);
        }

        public long NextLong()
        {
            return (((long)Next()) << 32) | Next();
        }

        public ulong NextULong()
        {
            return (((ulong)NextUInt()) << 32) | NextUInt();
        }

        public float NextFloat()
        {
            uint t = (x ^ (x << 11));
            x = y; y = z; z = w;
            return (FLOAT_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8)))));
        }

        /// <summary>
        /// 返回一个介于 0.0 和 1.0 之间的随机数。
        /// </summary>
        /// <returns>大于等于 0.0 并且小于 1.0 的双精度浮点数。</returns>
        public double NextDouble()
        {
            uint t = (x ^ (x << 11));
            x = y; y = z; z = w;

            // Here we can gain a 2x speed improvement by generating a value that can be cast to 
            // an int instead of the more easily available uint. If we then explicitly cast to an 
            // int the compiler will then cast the int to a double to perform the multiplication, 
            // this final cast is a lot faster than casting from a uint to a double. The extra cast
            // to an int is very fast (the allocated bits remain the same) and so the overall effect 
            // of the extra cast is a significant performance improvement.
            //
            // Also note that the loss of one bit of precision is equivalent to what occurs within 
            // System.Random.
            return (DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8)))));
        }

        public string NextString()
        {
            return NextString(Next(0, 200));
        }

        public string NextString(int size)
        {
            int[] ints = new int[size];
            NextInts(ints, 0, ints.Length);
            char[] chars = new char[ints.Length];
            for (int i = 0; i < ints.Length; i++)
                chars[i] = RAND_LETTER[(ints[i] % RAND_LETTER.Length)];
            return new string(chars);
        }

        public string NextAsciiString()
        {
            return NextString();
        }

        public string NextAsciiString(int size)
        {
            return NextString(size);
        }

        public string NextString(string baseString, int size)
        {
            return string.Empty;
        }

        /// <summary>
        /// 全中文的字符串
        /// </summary>
        /// <returns></returns>
        public string NextChineseString()
        {
            return NextString();
        }

        /// <summary>
        /// 随机字符串中只包含数字和字母随机数
        /// </summary>
        /// <returns></returns>
        public string NextNumberLetterString()
        {
            return NextString();
        }

        /// <summary>
        /// 返回非负随机数。
        /// 如果转换成int，产生的随机数既包含正数，也包含负数，性能比Next方法提高了20%
        /// </summary>
        /// <returns>大于等于零且小于 System.UInt32.MaxValue 的 32 位带符号整数。</returns>
        public uint NextUInt()
        {
            uint t = (x ^ (x << 11));
            x = y; y = z; z = w;
            return (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8)));
        }

        /// <summary>
        /// Generates a random int over the range 0 to int.MaxValue, inclusive. 
        /// This method differs from Next() only in that the range is 0 to int.MaxValue
        /// and not 0 to int.MaxValue-1.
        /// 
        /// The slight difference in range means this method is slightly faster than Next()
        /// but is not functionally equivalent to System.Random.Next().
        /// </summary>
        /// <returns></returns>
        public int NextInt()
        {
            uint t = (x ^ (x << 11));
            x = y; y = z; z = w;
            return (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))));
        }

        /// <summary>
        /// Generates a single random bit.
        /// This method's performance is improved by generating 32 bits in one operation and storing them
        /// ready for future calls.
        /// </summary>
        /// <returns></returns>
        public bool NextBool()
        {
            if (bitMask == 1)
            {
                // Generate 32 more bits.
                uint t = (x ^ (x << 11));
                x = y; y = z; z = w;
                bitBuffer = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                // Reset the bitMask that tells us which bit to read next.
                bitMask = 0x80000000;
                return (bitBuffer & bitMask) == 0;
            }

            return (bitBuffer & (bitMask >>= 1)) == 0;
        }

        public unsafe void GetRandomCharsFast(char[] buffer)
        {
            int len = RAND_LETTER.Length - 1;
            uint x = this.x, y = this.y, z = this.z, w = this.w;
            fixed (char* pd = &buffer[0])
            {
                char* pint = pd;
                int i = 0, c = buffer.Length >> 2;
                for (; i < c; i++)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = RAND_LETTER[(int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * len)];

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = RAND_LETTER[(int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * len)];

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = RAND_LETTER[(int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * len)];

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = RAND_LETTER[(int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * len)];
                }
                int leave = buffer.Length - (c << 2);
                for (i = 0; i < leave; i++)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = RAND_LETTER[(int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * len)];
                }
            }
            this.x = x; this.y = y; this.z = z; this.w = w;
        }

        #endregion

        #region 基本类型 数组

        public unsafe byte[] NextBytes(int size)
        {
            byte[] buffer = new byte[size];
            NextBytes(buffer, 0, size);
            return buffer;
        }

        public unsafe void NextBytes(byte[] buffer)
        {
            NextBytes(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// 用随机数填充指定字节数组的元素。
        /// 使用非安全代码获得随机数据
        /// 通过一个测试获得比安全代码高一倍的性能，通过每次移动四个字节，这个可能在不同的CPU上有显著的差别
        /// 通过采用每次移动填充四个数据，性能大约提升15%
        /// </summary>
        /// <param name="buffer">包含随机数的字节数组。</param>
        public unsafe void NextBytes(byte[] buffer, int offset, int count)
        {
            uint x = this.x, y = this.y, z = this.z, w = this.w;

            fixed (byte* pd = &buffer[offset])
            {
                byte* tpd = pd;
                uint* pint = (uint*)pd;
                int i = 0, len = count >> 4;
                for (; i < len; i++)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));
                }

                int length = count - (i << 4);
                tpd += (i << 4);
                if ((length & 8) != 0)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                    tpd += 8;
                }
                if ((length & 4) != 0)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                    tpd += 4;
                }
                if ((length & 2) != 0)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));
                    *((short*)tpd) = (short)w;
                    tpd += 2;
                }
                if ((length & 1) != 0)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));
                    *tpd++ = (byte)w;
                }
            }
            this.x = x; this.y = y; this.z = z; this.w = w;
        }

        public unsafe void NextShorts(short[] buffer, int offset, int count)
        {

        }

        public unsafe void NextUShorts(ushort[] buffer, int offset, int count)
        {

        }

        /// <summary>
        /// 10亿条记录的生成大约耗时1928ms
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public unsafe void NextInts(int[] buffer, int offset, int count)
        {
            uint x = this.x, y = this.y, z = this.z, w = this.w;
            fixed (int* pd = &buffer[offset])
            {
                int* pint = pd;
                int i = 0, len = count >> 2;
                for (; i < len; i++)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))));

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))));

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))));

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))));
                }
                int leave = count - (len << 2);
                for (i = 0; i < leave; i++)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))));
                }
            }
            this.x = x; this.y = y; this.z = z; this.w = w;
        }

        /// <summary>
        /// 10亿条记录的生成大约耗时2828ms
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        public unsafe void NextInts(int[] buffer, int offset, int count, int minValue, int maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException("upperBound", maxValue, "upperBound must be >=lowerBound");
            int diff = maxValue - minValue;
            uint x = this.x, y = this.y, z = this.z, w = this.w;
            fixed (int* pd = &buffer[offset])
            {
                int* pint = pd;
                int i = 0, len = count >> 2;
                for (; i < len; i++)
                {
                    //uint t = (x ^ (x << 11));
                    //x = y; y = z; z = w;
                    //*pint++ = (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))));

                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = minValue + (int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * diff);

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = minValue + (int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * diff);

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = minValue + (int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * diff);

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = minValue + (int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * diff);
                }
                int leave = count - (len << 2);
                for (i = 0; i < leave; i++)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = minValue + (int)((DOUBLE_UNIT_INT * (int)(0x7FFFFFFF & (w = (w ^ (w >> 19)) ^ (t ^ (t >> 8))))) * diff);
                }
            }
            this.x = x; this.y = y; this.z = z; this.w = w;
        }

        public unsafe void NextUInts(uint[] buffer, int offset, int count)
        {
            uint x = this.x, y = this.y, z = this.z, w = this.w;
            fixed (uint* pd = &buffer[offset])
            {
                uint* pint = pd;
                int i = 0, len = count >> 2;
                for (; i < len; i++)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));

                    t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));
                }
                int leave = count - (len << 2);
                for (i = 0; i < leave; i++)
                {
                    uint t = (x ^ (x << 11));
                    x = y; y = z; z = w;
                    *pint++ = w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));
                }
            }
            this.x = x; this.y = y; this.z = z; this.w = w;
        }

        public unsafe void NextLongs(long[] buffer, int offset, int count)
        {

        }

        public unsafe void NextULongs(ulong[] buffer, int offset, int count)
        {

        }

        public unsafe void NextFloats(float[] buffer, int offset, int count)
        {

        }

        public unsafe void NextDoubles(double[] buffer, int offset, int count)
        {

        }

        #endregion

        #region 静态方法

        /// <summary>
        /// 获得随机产生的Int数组数据。
        /// </summary>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static int[] GetRandomInts(int minValue, int maxValue, int count)
        {
            return GetRandomInts(minValue, maxValue, count, Guid.NewGuid().GetHashCode());
        }

        public static int[] GetRandomInts(int minValue, int maxValue, int count, int seed)
        {
            ShiboRandom rd = new ShiboRandom(seed);
            int[] result = new int[count];
            rd.NextInts(result, 0, result.Length, minValue, maxValue);
            return result;
        }

        public static int[] GetRandomIntsNoRepeat(int minValue, int maxValue, int count)
        {
            return GetRandomIntsNoRepeat(minValue, maxValue, count, Guid.NewGuid().GetHashCode());
        }

        /// <summary>
        /// 在0-214748364之间产生1000w个不重复的数据，实际需要运行10240415次随机数生成
        /// 总体冲突率不太高
        /// </summary>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        /// <param name="count"></param>
        /// <param name="seed"></param>
        /// <returns></returns>
        public static int[] GetRandomIntsNoRepeat(int minValue, int maxValue, int count, int seed)
        {
            ShiboRandom rd = new ShiboRandom(seed);
            int[] result = new int[count];
            BitArray bit = new BitArray(maxValue - minValue);
            int ecount = 0;
            int v = 0;
            while (true)
            {
                v = rd.Next(minValue, maxValue);
                if (bit[v - minValue] == false)
                {
                    bit[v - minValue] = true;
                    result[ecount] = v;
                    ecount++;
                }
                if (ecount == count)
                    break;
            }
            return result;
        }

        public static long[] GetRandomLongs(int minValue, int maxValue, int count)
        {
            ShiboRandom rd = new ShiboRandom(Guid.NewGuid().GetHashCode());
            long[] result = new long[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = rd.Next(minValue, maxValue);
            }
            return result;
        }

        /// <summary>
        /// 获得随机长度的字节数组，数组的最小长度是1，而不是0.
        /// </summary>
        /// <param name="sizeRange"></param>
        /// <returns></returns>
        public static byte[] GetRandomBytes(int sizeRange)
        {
            return GetRandomBytes(sizeRange, Guid.NewGuid().GetHashCode());
        }

        /// <summary>
        /// 获得随机长度的字节数组，数组的最小长度是1，而不是0.
        /// </summary>
        /// <param name="sizeRange"></param>
        /// <param name="seed"></param>
        /// <returns></returns>
        public static byte[] GetRandomBytes(int sizeRange, int seed)
        {
            ShiboRandom rd = new ShiboRandom(seed);
            int len = rd.Next(sizeRange);
            while (len == 0)
                len = rd.Next(sizeRange);
            byte[] result = new byte[len];
            rd.NextBytes(result);
            return result;
        }

        /// <summary>
        /// 获得一个随机数,使用的是哈希算法
        /// </summary>
        /// <param name="maxValue">数字的范围</param>
        /// <param name="seed">生成随机数的种子</param>
        /// <returns></returns>
        public static int GetRandomInt(int minValue, int maxValue)
        {
            return Instance.Next(minValue, maxValue);
            //uint result = HashAlgorithmsLib32.FNVHash1((seed * 1315423911).ToString());
            //return Math.Abs((int)result % maxValue);
        }

        public unsafe static string GetRandomString(int length)
        {
            return GetRandomString(length, rotateSeed++);
        }

        /// <summary>
        /// 获得一个随机字符串
        /// </summary>
        /// <param name="length">随机字符串的长度</param>
        /// <returns></returns>
        public unsafe static string GetRandomString(int length, int seed)
        {
            return GetRandomString(length, seed, RAND_LETTER);
        }

        /// <summary>
        /// 目前在大量长度小于4的时候会出现fatalexecutionengineerror异常，还没找到原因
        /// 使用微软Random类没问题
        /// </summary>
        /// <param name="length"></param>
        /// <param name="seed"></param>
        /// <param name="randomStr"></param>
        /// <returns></returns>
        public unsafe static string GetRandomString(int length, int seed, string randomStr)
        {
            int seedLength = randomStr.Length - 1;
            char[] ch = new char[length];
            ShiboRandom rd = new ShiboRandom(seed);
            fixed (char* pc = &ch[0])
            {
                char* pd = pc;
                for (int i = 0; i < length; i++)
                {
                    int index = rd.InternalNext(seedLength);
                    *pd++ = randomStr[index];
                }
                return new string(pd);
            }
        }

        public unsafe static char[] GetRandomChars(int length)
        {
            return GetRandomChars(length, rotateSeed++);
        }

        /// <summary>
        /// 获得一个随机字符串
        /// </summary>
        /// <param name="length">随机字符串的长度</param>
        /// <returns></returns>
        public unsafe static char[] GetRandomChars(int length, int seed)
        {
            return GetRandomChars(length, seed, RAND_LETTER);
        }

        public unsafe static char[] GetRandomChars(int length, int seed, string randomStr)
        {
            int seedLength = randomStr.Length - 1;
            char[] ch = new char[length];
            ShiboRandom rd = new ShiboRandom(seed);
            fixed (char* pc = &ch[0])
            {
                char* pd = pc;
                for (int i = 0; i < length; i++)
                {
                    int index = rd.InternalNext(seedLength);
                    *pd++ = randomStr[index];
                }
            }
            return ch;
        }

        public unsafe static void GetRandomChars(char[] buffer)
        {
            GetRandomChars(buffer, rotateSeed++);
        }

        public unsafe static void GetRandomChars(char[] buffer, int seed)
        {
            GetRandomChars(buffer, seed, RAND_LETTER);
        }

        public unsafe static void GetRandomChars(char[] buffer, int seed, string randomStr)
        {
            int seedLength = randomStr.Length - 1;
            int length = buffer.Length;
            ShiboRandom rd = new ShiboRandom(seed);
            fixed (char* pc = &buffer[0])
            {
                char* pd = pc;
                for (int i = 0; i < length; i++)
                {
                    int index = rd.InternalNext(seedLength);
                    *pd++ = randomStr[index];
                }
            }
        }

        /// <summary>
        /// 获得随机产生的字节码集合。
        /// </summary>
        /// <param name="sizeRange">字节数组的个数范围</param>
        /// <param name="count">字节数组的个数</param>
        /// <returns></returns>
        public static List<byte[]> GetRandomArrayBytes(int sizeRange, int count)
        {
            return GetRandomArrayBytes(sizeRange, count, Environment.TickCount);
        }

        /// <summary>
        /// 获得随机产生的字节码集合。
        /// </summary>
        /// <param name="sizeRange">字节数组的个数范围</param>
        /// <param name="count">字节数组的个数</param>
        /// <returns></returns>
        public static List<byte[]> GetRandomArrayBytes(int sizeRange, int count, int seed)
        {
            ShiboRandom rd = new ShiboRandom(seed);
            List<byte[]> result = new List<byte[]>(count);
            for (int i = 0; i < count; i++)
            {
                int len = rd.Next(sizeRange);
                byte[] by = new byte[len];
                rd.NextBytes(by);
                result.Add(by);
            }
            return result;
        }

        /// <summary>
        /// 获得随机产生的字节码集合。(固定长度)
        /// </summary>
        /// <param name="bytesLength">字节数组的固定长度</param>
        /// <param name="count">字节数组的个数</param>
        /// <returns></returns>
        public static List<byte[]> GetFixedRandomArrayBytes(int fixLength, int count)
        {
            return GetFixedRandomArrayBytes(fixLength, count, Environment.TickCount);
        }

        /// <summary>
        /// 获得随机产生的字节码集合。(固定长度)
        /// </summary>
        /// <param name="bytesLength">字节数组的固定长度</param>
        /// <param name="count">字节数组的个数</param>
        /// <returns></returns>
        public static List<byte[]> GetFixedRandomArrayBytes(int fixLength, int count, int seed)
        {
            ShiboRandom rd = new ShiboRandom(seed);
            List<byte[]> result = new List<byte[]>(count);
            for (int i = 0; i < count; i++)
            {
                byte[] by = new byte[fixLength];
                rd.NextBytes(by);
                result.Add(by);
            }
            return result;
        }

        #endregion

        /// <summary>
        /// 随机选择数据
        /// </summary>
        /// <typeparam name="T">T类型</typeparam>
        /// <param name="values">值</param>
        /// <param name="count">选择数据的数量</param>
        /// <returns></returns>
        public static T[] RandomSelect<T>(IList<T> values, int count)
        {
            int[] ints = GetRandomInts(0, values.Count - 1, count);
            T[] result = new T[count];
            for (int i = 0; i < ints.Length; i++)
                result[i] = values[ints[i]];
            return result;
        }
    }

    public sealed class ShiboRandom<T>
    {
        ShiboRandom rand = null;
        List<int> list;
        List<T> values;
        int min = 1;
        int max = 0;

        public ShiboRandom()
        {
            rand = new ShiboRandom();
        }

        public void Set(IList<KeyValuePair<T, int>> range)
        {
            list = new List<int>(range.Count);
            values = new List<T>(range.Count);
            int sum = 0;
            foreach (KeyValuePair<T, int> item in range)
            {
                if (item.Value <= 0)
                    throw new Exception("频率不能小于等于0");
                sum += item.Value;
                list.Add(sum);
                values.Add(item.Key);
            }
            max = sum;
        }

        public T Next()
        {
            int v = rand.Next(min, max);
            int idx = Math.Abs(list.BinarySearch(v));
            if (idx > 0)
                idx--;
            return values[idx];
        }

    }
}
