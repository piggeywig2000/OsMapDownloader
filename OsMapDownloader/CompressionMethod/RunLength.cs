using System;
using System.Collections.Generic;

namespace OsMapDownloader.CompressionMethod
{
    public class RunLength : CompressionMethod
    {
        public static byte[] Compress(byte[] source)
        {
            List<byte> compressed = new List<byte>();

            //Create subpalette
            byte[] subpalette = GetSubpalette(source);

            //Add length of subpalette as first byte
            compressed.Add(Convert.ToByte(subpalette.Length));
            compressed.AddRange(subpalette);

            //Get the required bits to store a colour
            byte requiredColourBits = (byte)Math.Ceiling(Math.Log2(subpalette.Length));
            byte requiredCountBits = (byte)(8 - requiredColourBits);
            byte maxCount = (byte)(Math.Pow(2, requiredCountBits) - 1);

            //Now do the pixel data
            byte currentLength = 0;
            for (int i = 0; i < source.Length; i++)
            {
                currentLength++;

                byte pixelValue = source[i];

                //Look ahead to see if the next value is the same as this one
                bool isSame = false;
                if (i + 1 < source.Length)
                {
                    if (source[i + 1] == pixelValue)
                    {
                        isSame = true;
                    }
                }

                //If the next value is not the same as this one, or we've hit the max count
                if (!isSame || currentLength >= maxCount)
                {
                    //We've hit the end of the chain and thus need to write to the compressed data

                    //Get the index of the pixel value in the subpalette
                    byte spIndex = Convert.ToByte(Array.FindIndex(subpalette, x => (x == pixelValue)));

                    //Build the byte to append
                    byte byteToAppend = 0x00;

                    //Set it to the count
                    byteToAppend = currentLength;

                    //Shift it across to the left side (aka by the required colour bits)
                    byteToAppend <<= requiredColourBits;

                    //Now stick the colour index on the right side by ORing it
                    byteToAppend = (byte)(byteToAppend | spIndex);

                    //Append byte and reset currentLength
                    compressed.Add(byteToAppend);
                    currentLength = 0;
                }
            }

            return compressed.ToArray();
        }
    }
}
