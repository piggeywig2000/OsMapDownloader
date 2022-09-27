using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OsMapDownloader.Qct;

namespace OsMapDownloader.Gui.VIewModels
{
    internal class MetadataViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public QctMetadata UnderlyingMetadata { get; }

        public string Name
        {
            get => UnderlyingMetadata.Name ?? "";
            set
            {
                if (value != UnderlyingMetadata.Name)
                {
                    UnderlyingMetadata.Name = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string LongTitle
        {
            get => UnderlyingMetadata.LongTitle ?? "";
            set
            {
                if (value != UnderlyingMetadata.LongTitle)
                {
                    UnderlyingMetadata.LongTitle = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Identifier
        {
            get => UnderlyingMetadata.Identifier ?? "";
            set
            {
                if (value != UnderlyingMetadata.Identifier)
                {
                    UnderlyingMetadata.Identifier = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Edition
        {
            get => UnderlyingMetadata.Edition ?? "";
            set
            {
                if (value != UnderlyingMetadata.Edition)
                {
                    UnderlyingMetadata.Edition = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Revision
        {
            get => UnderlyingMetadata.Revision ?? "";
            set
            {
                if (value != UnderlyingMetadata.Revision)
                {
                    UnderlyingMetadata.Revision = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Keywords
        {
            get => UnderlyingMetadata.Keywords ?? "";
            set
            {
                if (value != UnderlyingMetadata.Keywords)
                {
                    UnderlyingMetadata.Keywords = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Scale
        {
            get => UnderlyingMetadata.Scale ?? "";
            set
            {
                if (value != UnderlyingMetadata.Scale)
                {
                    UnderlyingMetadata.Scale = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Datum
        {
            get => UnderlyingMetadata.Datum ?? "";
            set
            {
                if (value != UnderlyingMetadata.Datum)
                {
                    UnderlyingMetadata.Datum = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Depths
        {
            get => UnderlyingMetadata.Depths ?? "";
            set
            {
                if (value != UnderlyingMetadata.Depths)
                {
                    UnderlyingMetadata.Depths = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Heights
        {
            get => UnderlyingMetadata.Heights ?? "";
            set
            {
                if (value != UnderlyingMetadata.Heights)
                {
                    UnderlyingMetadata.Heights = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Projection
        {
            get => UnderlyingMetadata.Projection ?? "";
            set
            {
                if (value != UnderlyingMetadata.Projection)
                {
                    UnderlyingMetadata.Projection = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Type
        {
            get => UnderlyingMetadata.MapType ?? "";
            set
            {
                if (value != UnderlyingMetadata.MapType)
                {
                    UnderlyingMetadata.MapType = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Copyright
        {
            get => UnderlyingMetadata.Copyright ?? "";
            set
            {
                if (value != UnderlyingMetadata.Copyright)
                {
                    UnderlyingMetadata.Copyright = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool MustHaveOriginalFile
        {
            get => UnderlyingMetadata.Flags.HasFlag(QctFlags.MustHaveOriginalFile);
            set
            {
                if (value != MustHaveOriginalFile)
                {
                    UnderlyingMetadata.Flags = value ? UnderlyingMetadata.Flags | QctFlags.MustHaveOriginalFile : UnderlyingMetadata.Flags & ~QctFlags.MustHaveOriginalFile;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool AllowCalibration
        {
            get => UnderlyingMetadata.Flags.HasFlag(QctFlags.AllowCalibration);
            set
            {
                if (value != AllowCalibration)
                {
                    UnderlyingMetadata.Flags = value ? UnderlyingMetadata.Flags | QctFlags.AllowCalibration : UnderlyingMetadata.Flags & ~QctFlags.AllowCalibration;
                    NotifyPropertyChanged();
                }
            }
        }

        public MetadataViewModel()
        {
            UnderlyingMetadata = new QctMetadata();
        }
    }
}
