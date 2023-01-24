using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsMapDownloader
{
    public enum MapGenerationExceptionReason
    {
        BorderOutOfBounds,
        BorderNonSimple,
        PolynomialCalculationOutOfMemory,
        DownloadError,
        OpenGLError,
        IOError
    }

    public class MapGenerationException : Exception
    {
        public MapGenerationExceptionReason Reason { get; }

        private static string ReasonToMessage(MapGenerationExceptionReason reason) => reason switch
        {
            MapGenerationExceptionReason.BorderOutOfBounds => "Map border is too far away from the UK. Bring the border closer to the UK.",
            MapGenerationExceptionReason.BorderNonSimple => "Map border is invalid. It must not cross over itself, must not have multiple points in the same location, and must not have 3 connected points in a straight line.",
            MapGenerationExceptionReason.PolynomialCalculationOutOfMemory => "The computer ran out of memory while calculating the geographical referencing polynomial coefficients. Try reducing the number of samples used.",
            MapGenerationExceptionReason.DownloadError => "An error occurred while downloading the images. Ordinance Survey have probably changed something on their website that broke this program. It's possible that this could be fixed by providing your own token.",
            MapGenerationExceptionReason.OpenGLError => "An OpenGL error occurred while processing the tiles. Check that your video drivers are up to date, or try disabling hardware acceleration.",
            MapGenerationExceptionReason.IOError => "The file could not be written to. Check the the file path provided is valid, and that the file is not already open in another program.",
            _ => throw new ArgumentException("Invalid value for reason")
        };

        public MapGenerationException(MapGenerationExceptionReason reason) : this(reason, null) { }

        public MapGenerationException(MapGenerationExceptionReason reason, Exception? innerException) : base(ReasonToMessage(reason), innerException)
        {
            Reason = reason;
        }
    }
}
