using System;
using System.Collections;
using System.Collections.Generic;

namespace OsMapDownloader.Qct.CompressionMethod
{
    public class PixelPacking : CompressionMethod
    {
        public static byte[] Compress(byte[] source)
        {
            List<byte> compressed = new List<byte>();

            //Create subpalette
            byte[] subpalette = GetSubpalette(source);

            //Add 256 - length of subpalette as first byte
            compressed.Add(Convert.ToByte((byte)(256 - (byte)subpalette.Length)));
            compressed.AddRange(subpalette);

            byte requiredColourBits = (byte)Math.Ceiling(Math.Log2(subpalette.Length));

            int currentSourceIndex = 0;
            while (currentSourceIndex < source.Length)
            {
                //Process in 4 byte chunks
                BitArray thisChunk = new BitArray(32);
                byte bitsRemaining = 32;

                //Repeat until there isn't enough space left for another pixel or until there's no pixels remaining
                while (bitsRemaining - requiredColourBits >= 0 && currentSourceIndex < source.Length)
                {
                    int pixelValueToAdd = Array.FindIndex(subpalette, x => x == source[currentSourceIndex]);

                    //Create a new BitArray to represent the colour
                    BitArray thisPixel = new BitArray(new int[] { pixelValueToAdd });

                    //Shift the new BitArray based on how many bits remaining there are left in this chunk
                    thisPixel.LeftShift(32 - bitsRemaining);

                    //OR this pixel with this chunk to add it to the chunk
                    thisChunk.Or(thisPixel);

                    //We've added another pixel. Reduce the bits remaining
                    bitsRemaining -= requiredColourBits;

                    currentSourceIndex++;
                }

                //Chunk is finished. Convert to array and reverse the bytes (not bits)
                byte[] bChunk = new byte[4];
                thisChunk.CopyTo(bChunk, 0);

                compressed.AddRange(bChunk);
            }

            return compressed.ToArray();
        }
    }
}
