using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsMapDownloader.Progress
{
    public class ProgressTracker
    {
        private (ProgressItem, double)[] progresses;

        public event EventHandler<double>? ProgressChanged;

        public ProgressTracker(params (ProgressItem, double)[] progresses)
        {
            foreach ((ProgressItem, double) item in progresses)
            {
                item.Item1.ProgressChanged += (s, e) => ProgressChanged?.Invoke(this, OverallProgress);
            }
            this.progresses = progresses;
        }

        public bool IsCompleted => progresses.All(item => item.Item1.Value == 1);
        public ProgressItem? CurrentProgressItem => IsCompleted ? null : progresses.First(item => item.Item1.Value < 1).Item1;
        public IProgress<double>? CurrentProgress => CurrentProgressItem; 
        public double OverallProgress => progresses.Sum(item => item.Item1.Value * item.Item2) / progresses.Sum(item => item.Item2);
    }
}
