using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsMapDownloader.Progress
{
    public class ProgressPhases : ProgressItem
    {
        private readonly ProgressPhase[] phases;

        public ProgressPhases(string name, params ProgressPhase[] phases) : base(name)
        {
            this.phases = phases;
        }

        private uint _completedPhases = 0;
        public uint CompletedPhases
        {
            get => _completedPhases;
            private set
            {
                if (value > phases.Length) throw new ArgumentException("Cannot set current phase to greater than the number of phases");
                if (value < CompletedPhases) throw new InvalidOperationException("Cannot decrease the completed phases");
                _completedPhases = value;
            }
        }

        public override string Status
        {
            get => CompletedPhases < phases.Length ? phases[CompletedPhases].Name : "Completed";
        }

        protected override void OnReport(double value)
        {
            CompletedPhases = (uint)value;
            ReportPhaseChange();
        }

        private void ReportPhaseChange()
        {
            double completion = phases.Take((int)CompletedPhases).Sum(phase => phase.Weight) / phases.Sum(phase => phase.Weight);
            base.OnReport(completion);
        }
    }

    public class ProgressPhase
    {
        public ProgressPhase(string name, double weight)
        {
            Name = name;
            Weight = weight;
        }

        public string Name { get; }
        public double Weight { get; }
    }
}
