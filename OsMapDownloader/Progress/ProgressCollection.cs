using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsMapDownloader.Progress
{
    internal class ProgressCollection : ProgressItem
    {
        private readonly string itemName;
        private readonly List<KeyValuePair<DateTime, double>> completionHistory = new List<KeyValuePair<DateTime, double>>();

        public ProgressCollection(string name, string itemName) : base(name)
        {
            this.itemName = itemName;
        }

        private uint _totalItems = 1;
        public uint TotalItems
        {
            get => _totalItems;
            set
            {
                if (value < CompletedItems || value == 0) throw new ArgumentException("Completed items cannot be less than total items");
                _totalItems = value;
                ReportCompletion();
            }
        }

        private uint _completedItems = 0;
        public uint CompletedItems
        {
            get => _completedItems;
            private set
            {
                if (value > TotalItems) throw new ArgumentException("Completed items cannot be less than total items");
                if (value < CompletedItems) throw new InvalidOperationException("Cannot decrease completed items");
                _completedItems = value;
            }
        }

        public double ItemsPerSecond
        {
            get
            {
                if (completionHistory.Count <= 1) return 0;
                DateTime now = DateTime.UtcNow;
                double secondsSinceOldest = (now - completionHistory[1].Key).TotalSeconds;
                double itemsPerSecond = secondsSinceOldest == 0 ? 0 : (completionHistory.Zip(completionHistory.Skip(1)).Sum(dataPoints => dataPoints.Second.Value - dataPoints.First.Value)) / secondsSinceOldest;
                return itemsPerSecond;
            }
        }

        public TimeSpan? TimeRemaining
        {
            get => ItemsPerSecond == 0 ? null : TimeSpan.FromSeconds((TotalItems - CompletedItems) / ItemsPerSecond);
        }

        public override string Status
        {
            get => $"{CompletedItems} {itemName}s out of {TotalItems}    {ItemsPerSecond:0.0} {itemName}s/second    Time Remaining: {(TimeRemaining == null ? "Calculating..." : TimeSpan.FromSeconds((uint)TimeRemaining.Value.TotalSeconds))}";
        }

        protected override void OnReport(double value)
        {
            CompletedItems = (uint)value;
            ReportCompletion();
        }

        private void ReportCompletion()
        {
            AddToCompletionHistory();
            double completion = (double)CompletedItems / (double)TotalItems;
            base.OnReport(completion);
        }

        private void AddToCompletionHistory()
        {
            DateTime now = DateTime.UtcNow;
            completionHistory.Add(new KeyValuePair<DateTime, double>(now, CompletedItems));
            //Remove old entries
            while (completionHistory.Count > 0 && now - completionHistory[0].Key >= TimeSpan.FromMinutes(5))
                completionHistory.RemoveAt(0);
        }
    }
}
