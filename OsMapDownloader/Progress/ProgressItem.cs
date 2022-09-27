using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsMapDownloader.Progress
{
    public class ProgressItem : Progress<double>
    {
        public ProgressItem(string name)
        {
            Name = name;
        }

        private double _value = 0;
        public double Value
        {
            get => _value;
            private set
            {
                if (value < 0 || value > 1) throw new ArgumentException("Numeric progress value must be between 0 and 1");
                _value = value;
            }
        }

        public virtual string Name { get; protected set; }
        public virtual string Status { get => $"{Value * 100:0.0000}%"; }

        protected override void OnReport(double value)
        {
            Value = value;
            base.OnReport(value);
        }
    }
}
