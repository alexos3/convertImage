using System;
using System.IO;
using System.Text;

namespace ImageExtractorFV4
{
    public class ImageExtractorFV4
    {
        public const int IMAGE_TYPE_UNKNOWN = 0;
        public const int IMAGE_TYPE_FV4 = 1;
        public const int IMAGE_TYPE_GIF = 2;
        public const int IMAGE_TYPE_TIF = 3;
        public const int COMPRESSION_UNKNOWN = 0;
        public const int COMPRESSION_NONE = 1;
        public const int COMPRESSION_GROUP4 = 2;
        private const int FV4_ROWS = 1280;
        private const int FV4_COLUMNS = 1024;
        private int[] codeBits = { 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 4, 5 };
        private int[] rightShift = { 30, 30, 30, 30, 30, 30, 30, 30, 28, 28, 28, 28, 26, 26, 24, 22 };
        private int[] codeBase = { 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 4, 4, 20, 20, 84, 340 };
        private int currentRow;
        protected int rows;
        protected int columns;
        protected int bitsPerPixel;
        protected byte[] colorArray;
        protected byte[] image;

        public ImageExtractorFV4()
        {
            currentRow = 0;
        }

        public byte[] GetImage(byte[] fv4bytes, EPCImageViewerCalloutArray calloutArray)
        {
            if (fv4bytes == null)
            {
                return null;
            }
            using (MemoryStream imageBuf = new MemoryStream())
            {
                try
                {
                    int bufferOffset = 0;
                    int count = DecodeImageHeader(fv4bytes, 0, fv4bytes.Length, calloutArray);
                    if (count <= 0)
                    {
                        return null;
                    }
                    bufferOffset += count;
                    //imageBuf.Write(fv4bytes, bufferOffset, fv4bytes.Length - bufferOffset);
                    while (bufferOffset < fv4bytes.Length)
                    {
                        count = DecodeImageData(fv4bytes, bufferOffset, fv4bytes.Length - bufferOffset, imageBuf);
                        if (count < 0)
                        {
                            return null;
                        }
                        if (count == 0)
                        {
                            break;
                        }
                        bufferOffset += count;
                    }
                    return imageBuf.ToArray();
                }
                catch (Exception exception)
                {
                    return null;
                }
            }
        }

        private int DecodeImageData(byte[] buffer, int bufferOffset, int bufferBytes, MemoryStream imageBuf)
        {
            short[][] planesOdd = new short[bitsPerPixel][];
            for (int i = 0; i < bitsPerPixel; i++)
            {
                planesOdd[i] = new short[65535];
            }
            short[][] planesEven = new short[bitsPerPixel][];
            for (int i = 0; i < bitsPerPixel; i++)
            {
                planesEven[i] = new short[65535];
            }

            byte[] imagePaletteIn = new byte[1025];

            int subgroupBits = ((buffer[(bufferOffset + 6)] & 0xFF) << 8) + (buffer[(bufferOffset + 5)] & 0xFF);
            int subgroupBytes = subgroupBits + 7 >> 3;
            if (subgroupBytes + 16 > bufferBytes)
            {
                return 0;
            }
            int subgroupStartRow = ((buffer[(bufferOffset + 1)] & 0xFF) << 8) + (buffer[(bufferOffset + 0)] & 0xFF);
            if ((buffer[(bufferOffset + 4)] != bitsPerPixel) || (subgroupStartRow != currentRow))
            {
                return 0;
            }
            int subgroupRows = ((buffer[(bufferOffset + 3)] & 0xFF) << 8) + (buffer[(bufferOffset + 2)] & 0xFF);

            int bufferIndex = bufferOffset + 16;
            int value = (buffer[(bufferIndex++)] & 0xFF) << 24;
            value |= (buffer[(bufferIndex++)] & 0xFF) << 16;
            value |= (buffer[(bufferIndex++)] & 0xFF) << 8;
            value |= buffer[(bufferIndex++)] & 0xFF;

            int iTmp = imagePaletteIn.Length;
            int index = 0;
            while (index < iTmp)
            {
                imagePaletteIn[(index++)] = 0;
            }
            int bitCount = 0;
            bool odd = false;
            while (subgroupRows-- > 0)
            {
                odd = !odd;
                byte imageMask = (byte)(1 << bitsPerPixel - 1);
                int bitPlane = 0;
                while (bitPlane < bitsPerPixel)
                {
                    short[] planePrevious;
                    short[] planeCurrent;
                    if (odd)
                    {
                        planeCurrent = planesOdd[bitPlane];
                        planePrevious = planesEven[bitPlane];
                    }
                    else
                    {
                        planeCurrent = planesEven[bitPlane];
                        planePrevious = planesOdd[bitPlane];
                    }
                    bitPlane++;

                    int toggle = 0;
                    int toggleSave = toggle;
                    int indexCurrent = 0;
                    int indexPrevious = 0;

                    int column = 0;
                    while (column < 1024)
                    {
                        if ((value & 0xE0000000) == -536870912)
                        {
                            index = (value & 0x1E000000) >> 25;
                            iTmp = codeBits[index];
                            value <<= 3 + iTmp;
                            bitCount += 3 + iTmp;
                            int runLength = (int)((uint)value >> rightShift[index]);
                            runLength += codeBase[index];
                            iTmp <<= 1;
                            value <<= iTmp;
                            bitCount += iTmp;
                            column += runLength;
                        }
                        else
                        {
                            int relLength;
                            if ((value & 0x80000000) == 0)
                            {
                                relLength = 0;
                                value <<= 1;
                                bitCount++;
                            }
                            else if ((value & 0x40000000) == 0)
                            {
                                if ((value & 0x20000000) == 0)
                                {
                                    relLength = 1;
                                }
                                else {
                                    relLength = -1;
                                }
                                value <<= 3;
                                bitCount += 3;
                            }
                            else
                            {
                                int sign = value & 0x10000000;
                                index = (value & 0xF000000) >> 24;
                                iTmp = codeBits[index];
                                value <<= 4 + iTmp;
                                bitCount += 4 + iTmp;
                                relLength = (int)((uint)value >> rightShift[index]);
                                relLength += codeBase[index] + 2;
                                if (sign != 0)
                                {
                                    relLength = -relLength;
                                }
                                iTmp <<= 1;
                                value <<= iTmp;
                                bitCount += iTmp;
                            }
                            if (toggle != toggleSave)
                            {
                                if (indexPrevious == 0)
                                {
                                    indexPrevious++;
                                }
                                else {
                                    indexPrevious--;
                                }
                            }
                            if (indexCurrent == 0)
                            {
                                iTmp = 0;
                            }
                            else {
                                iTmp = planeCurrent[(indexCurrent - 1)];
                            }
                            while (planePrevious[indexPrevious] <= iTmp)
                            {
                                indexPrevious += 2;
                            }
                            column = planePrevious[indexPrevious] + relLength;
                            toggleSave = toggle;
                        }
                        planeCurrent[(indexCurrent++)] = ((short)column); int
                          tmp706_704 = column; byte[] tmp706_702 = imagePaletteIn; tmp706_702[tmp706_704] = ((byte)(tmp706_702[tmp706_704] | imageMask));
                        if (bitCount >= 8)
                        {
                            iTmp = 0;
                            while (bitCount >= 8)
                            {
                                bitCount -= 8;
                                subgroupBits -= 8;
                                if (bufferIndex >= buffer.Length)
                                {
                                    break;
                                }
                                iTmp = iTmp << 8 | buffer[(bufferIndex++)] & 0xFF;
                            }
                            value |= iTmp << bitCount;
                        }
                        toggle ^= 0x1;
                    }
                    if (column != 1024)
                    {
                        return -1;
                    }
                    planeCurrent[(indexCurrent++)] = ((short)column);
                    planeCurrent[(indexCurrent++)] = ((short)column); int
                      tmp820_818 = column; byte[] tmp820_816 = imagePaletteIn; tmp820_816[tmp820_818] = ((byte)(tmp820_816[tmp820_818] | imageMask));
                    imageMask = (byte)(imageMask >> 1);
                }
                imageMask = (byte)((1 << bitsPerPixel) - 1);
                if (imagePaletteIn[0] != 0)
                {
                    imageMask = (byte)(imagePaletteIn[0] ^ imageMask);
                }
                imagePaletteIn[0] = 0;

                int offset = currentRow * 1024;
                iTmp = 0;
                while (iTmp++ < 1024)
                {
                    switch (imageMask)
                    {
                        case 0:
                            imageBuf.Write(new byte[] { 0 }, offset, 1);
                            break;
                        case 1:
                            imageBuf.Write(new byte[] { -27 & 0xFF }, offset, 1);
                            break;
                        case 2:
                            imageBuf.Write(new byte[] { -14 & 0xFF }, offset, 1);
                            break;
                        case 3:
                            imageBuf.Write(new byte[] { -1 & 0xFF }, offset, 1);
                            break;
                    }
                    if (imagePaletteIn[iTmp] != 0)
                    {
                        imageMask = (byte)(imagePaletteIn[iTmp] ^ imageMask);
                        imagePaletteIn[iTmp] = 0;
                    }
                    offset++;
                }
                currentRow += 1;
            }
            subgroupBytes = (subgroupBytes + 1023) / 1024 * 1024;
            return subgroupBytes;
        }

        private int DecodeImageHeader(byte[] buffer, int bufferOffset, int bufferBytes, EPCImageViewerCalloutArray calloutArray)
        {
            int rasterOffset = 0;
            int rasterLength = 0;
            int annotationOffset = 0;
            int annotationLength = 0;

            bitsPerPixel = buffer[35];
            columns = (((buffer[38] & 0xFF) << 8) + (buffer[39] & 0xFF));
            rows = (((buffer[40] & 0xFF) << 8) + (buffer[41] & 0xFF));
            int overlays = buffer[45];
            if (((bitsPerPixel != 1) && (bitsPerPixel != 2) && (bitsPerPixel != 4)) || (columns != 1024) || (rows > 1280) || (overlays > 2))
            {
                return -1;
            }
            int offset = 46;

            while (overlays-- > 0)
            {
                if ((buffer[offset] == 1) && (buffer[(offset + 1)] == 1))
                {
                    rasterOffset = ((buffer[(offset + 6)] & 0xFF) << 8) + (buffer[(offset + 7)] & 0xFF);

                    rasterLength = ((buffer[(offset + 10)] & 0xFF) << 8) + (buffer[(offset + 11)] & 0xFF);
                }
                else if ((buffer[offset] == 32) && (buffer[(offset + 1)] == 1))
                {
                    annotationOffset = ((buffer[(offset + 6)] & 0xFF) << 8) + (buffer[(offset + 7)] & 0xFF);

                    annotationLength = ((buffer[(offset + 10)] & 0xFF) << 8) + (buffer[(offset + 11)] & 0xFF);

                    byte[] annotationData = new byte[annotationLength];
                    Array.Copy(buffer, annotationOffset, annotationData, 0, annotationLength);

                    SaveCallouts(annotationData, annotationLength, calloutArray);
                }
                offset += 16;
            }
            if ((rasterOffset == 0) || (rasterOffset + rasterLength > bufferOffset + bufferBytes) || (buffer[rasterOffset] != 1) || (buffer[(rasterOffset + 1)] != 4))
            {
                return -1;
            }
            offset = rasterOffset + 18;
            int index = 0;
            int count = 1 << bitsPerPixel;
            colorArray = new byte[count];
            while (index < count)
            {
                int tmp1;
                if ((index & 0x1) == 1)
                {
                    tmp1 = 0xF & buffer[(offset++)];
                }
                else {
                    tmp1 = (0xF0 & buffer[offset]) >> 4;
                }
                if (bitsPerPixel == 1)
                {
                    tmp1 <<= 3;
                }
                else if (bitsPerPixel == 2)
                {
                    tmp1 <<= 2;
                }
                else {
                    tmp1 <<= 1;
                }
                colorArray[(index++)] = buffer[(2 + tmp1)];
            }
            int tmp2;
            if (rasterOffset > annotationOffset)
            {
                tmp2 = rasterOffset + rasterLength - 1;
            }
            else {
                tmp2 = annotationOffset + annotationLength - 1;
            }
            tmp2 = (tmp2 + 1023) / 1024 * 1024;
            return tmp2;
        }

        private void SaveCallouts(byte[] annotationData, int annotationLength, EPCImageViewerCalloutArray calloutArray)
        {
            int len1 = annotationData[18] & 0xFF;
            int len2 = annotationData[19] & 0xFF;
            int len = 256 * len1 + len2;

            int count1 = annotationData[20] & 0xFF;
            int count2 = annotationData[21] & 0xFF;

            int count = 256 * count1 + count2;

            calloutArray.Allocate(count);

            int n = 0;
            int i = 22;
            while ((i < annotationLength) && (n < count))
            {
                int si = i - 1;

                int x = 0;
                int x1 = annotationData[(i++)];
                if (x1 < 0)
                {
                    x1 += 256;
                }
                x1 <<= 8;
                x |= x1;
                int x2 = annotationData[(i++)];
                if (x2 < 0)
                {
                    x2 += 256;
                }
                x |= x2;

                int y = 0;
                int y1 = annotationData[(i++)];
                if (y1 < 0)
                {
                    y1 += 256;
                }
                y1 <<= 8;
                y |= y1;
                int y2 = annotationData[(i++)];
                if (y2 < 0)
                {
                    y2 += 256;
                }
                y |= y2;

                int counter = 0;
                while (annotationData[(i + counter)] != 0)
                {
                    counter++;
                }
                string c = Encoding.UTF8.GetString(annotationData, i, counter);

                calloutArray.AddToX(x, n);
                calloutArray.AddToY(y, n);
                calloutArray.AddToCalloutDesc(c, n);

                i += counter;
                while (i < annotationData.Length)
                {
                    if ((i - si) % len == 0)
                    {
                        break;
                    }
                    i++;
                }
                while (i < annotationData.Length)
                {
                    if ((i - si) % 4 == 0)
                    {
                        break;
                    }
                    i++;
                }
                i++;
                n++;
            }
        }
    }
}