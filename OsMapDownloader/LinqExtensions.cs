using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsMapDownloader
{
    public static class LinqExtensions
    {
        public static IEnumerable<T> WithCancellation<T>(this IEnumerable<T> enumerable, CancellationToken cancellationToken)
        {
            foreach (T item in enumerable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
        }
    }
}
