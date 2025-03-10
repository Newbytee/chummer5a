/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System.IO;

namespace SevenZip.Compression.RangeCoder
{
    internal class Encoder
    {
        public const uint kTopValue = 1 << 24;

        private Stream Stream;

        public ulong Low;
        public uint Range;
        private uint _cacheSize;
        private byte _cache;

        private long StartPosition;

        public void SetStream(Stream stream)
        {
            Stream = stream;
        }

        public void ReleaseStream()
        {
            Stream = null;
        }

        public void Init()
        {
            StartPosition = Stream.Position;

            Low = 0;
            Range = 0xFFFFFFFF;
            _cacheSize = 1;
            _cache = 0;
        }

        public void FlushData()
        {
            for (int i = 0; i < 5; i++)
                ShiftLow();
        }

        public void FlushStream()
        {
            Stream.Flush();
        }

        public void CloseStream()
        {
            Stream.Close();
        }

        public void Encode(uint start, uint size, uint total)
        {
            unchecked
            {
                Low += start * (Range /= total);
                Range *= size;
                while (Range < kTopValue)
                {
                    Range <<= 8;
                    ShiftLow();
                }
            }
        }

        public void ShiftLow()
        {
            unchecked
            {
                if ((uint)Low < 0xFF000000 || (uint)(Low >> 32) == 1)
                {
                    byte temp = _cache;
                    do
                    {
                        Stream.WriteByte((byte)(temp + (Low >> 32)));
                        temp = 0xFF;
                    } while (--_cacheSize != 0);

                    _cache = (byte)((uint)Low >> 24);
                }

                _cacheSize++;
                Low = (uint)Low << 8;
            }
        }

        public void EncodeDirectBits(uint v, int numTotalBits)
        {
            unchecked
            {
                for (int i = numTotalBits - 1; i >= 0; i--)
                {
                    Range >>= 1;
                    if (((v >> i) & 1) == 1)
                        Low += Range;
                    if (Range < kTopValue)
                    {
                        Range <<= 8;
                        ShiftLow();
                    }
                }
            }
        }

        public void EncodeBit(uint size0, int numTotalBits, uint symbol)
        {
            unchecked
            {
                uint newBound = (Range >> numTotalBits) * size0;
                if (symbol == 0)
                    Range = newBound;
                else
                {
                    Low += newBound;
                    Range -= newBound;
                }

                while (Range < kTopValue)
                {
                    Range <<= 8;
                    ShiftLow();
                }
            }
        }

        public long GetProcessedSizeAdd()
        {
            return _cacheSize +
                Stream.Position - StartPosition + 4;
            // (long)Stream.GetProcessedSize();
        }
    }

    internal class Decoder
    {
        public const uint kTopValue = 1 << 24;
        public uint Range;
        public uint Code;

        // public Buffer.InBuffer Stream = new Buffer.InBuffer(1 << 16);
        public Stream Stream;

        public void Init(Stream stream)
        {
            // Stream.Init(stream);
            Stream = stream;

            Code = 0;
            Range = 0xFFFFFFFF;
            byte[] achrBuffer = new byte[5];
            _ = Stream.Read(achrBuffer, 0, 5);
            unchecked
            {
                for (int i = 0; i < 5; i++)
                    Code = (Code << 8) | achrBuffer[i];
            }
        }

        public void ReleaseStream()
        {
            // Stream.ReleaseStream();
            Stream = null;
        }

        public void CloseStream()
        {
            Stream.Close();
        }

        public void Normalize()
        {
            unchecked
            {
                int intNumReads = Chummer.IntegerExtensions.DivAwayFromZero((int) (kTopValue / Range), 8);
                if (intNumReads <= 0)
                    return;
                byte[] achrBuffer = new byte[intNumReads];
                _ = Stream.Read(achrBuffer, 0, intNumReads);
                int i = 0;
                while (Range < kTopValue)
                {
                    Code = (Code << 8) | achrBuffer[i++];
                    Range <<= 8;
                }
            }
        }

        public void Normalize2()
        {
            unchecked
            {
                if (Range < kTopValue)
                {
                    Code = (Code << 8) | (byte)Stream.ReadByte();
                    Range <<= 8;
                }
            }
        }

        public uint GetThreshold(uint total)
        {
            return Code / (Range /= total);
        }

        public void Decode(uint start, uint size, uint total)
        {
            unchecked
            {
                Code -= start * Range;
                Range *= size;
                Normalize();
            }
        }

        public uint DecodeDirectBits(int numTotalBits)
        {
            uint range = Range;
            uint code = Code;
            uint result = 0;
            unchecked
            {
                for (int i = numTotalBits; i > 0; i--)
                {
                    range >>= 1;
                    /*
                    result <<= 1;
                    if (code >= range)
                    {
                        code -= range;
                        result |= 1;
                    }
                    */
                    uint t = (code - range) >> 31;
                    code -= range & (t - 1);
                    result = (result << 1) | (1 - t);

                    if (range < kTopValue)
                    {
                        code = (code << 8) | (byte)Stream.ReadByte();
                        range <<= 8;
                    }
                }
            }

            Range = range;
            Code = code;
            return result;
        }

        public uint DecodeBit(uint size0, int numTotalBits)
        {
            unchecked
            {
                uint newBound = (Range >> numTotalBits) * size0;
                uint symbol;
                if (Code < newBound)
                {
                    symbol = 0;
                    Range = newBound;
                }
                else
                {
                    symbol = 1;
                    Code -= newBound;
                    Range -= newBound;
                }

                Normalize();

                return symbol;
            }
        }

        // ulong GetProcessedSize() {return Stream.GetProcessedSize(); }
    }
}
