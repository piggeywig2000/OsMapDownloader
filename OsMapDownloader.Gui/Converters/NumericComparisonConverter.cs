using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace OsMapDownloader.Gui.Converters
{
    internal class NumericComparisonConverter : MarkupExtension, IValueConverter
    {
        public double CompareTo { get; set; }
        public NumericComparisonType Comparison { get; set; }
        public enum NumericComparisonType
        {
            None = 0,
            EqualTo,
            NotEqualTo,
            LessThan,
            GreaterThan,
            LessThanOrEqualTo,
            GreaterThanOrEqualTo
        }

        protected bool IsNumericType(Type? type)
        {
            if (type == null)
                return false;
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                case TypeCode.Object:
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        return IsNumericType(Nullable.GetUnderlyingType(type));
                    }
                    return false;
            }
            return false;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && IsNumericType(value.GetType()))
            {
                var d = System.Convert.ToDouble(value);
                return Comparison switch
                {
                    NumericComparisonType.EqualTo => d == CompareTo,
                    NumericComparisonType.NotEqualTo => d != CompareTo,
                    NumericComparisonType.LessThan => d < CompareTo,
                    NumericComparisonType.GreaterThan => d > CompareTo,
                    NumericComparisonType.LessThanOrEqualTo => d <= CompareTo,
                    NumericComparisonType.GreaterThanOrEqualTo => d >= CompareTo,
                    _ => throw new InvalidOperationException("Comparison is set to an invalid value")
                };
            }

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
