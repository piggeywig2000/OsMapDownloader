using System;
using System.Collections.Generic;

namespace OsMapDownloader.Qct.CompressionMethod
{
    public abstract class CompressionMethod
    {
        protected static byte[] GetSubpalette(byte[] source)
        {
            List<byte> subpalette = new List<byte>();
            foreach (byte pixelValue in source)
            {
                //If the subpalette does not contain this colour, add it to the subpalette
                if (!subpalette.Contains(pixelValue))
                {
                    subpalette.Add(pixelValue);
                }
            }

            return subpalette.ToArray();
        }
    }
}
