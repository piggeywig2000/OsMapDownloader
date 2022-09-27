using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsMapDownloader.Border
{
    internal class TriangleGenerationException : Exception
    {
        public TriangleGenerationException()
        {
        }

        public TriangleGenerationException(string? message) : base(message)
        {
        }

        public TriangleGenerationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
