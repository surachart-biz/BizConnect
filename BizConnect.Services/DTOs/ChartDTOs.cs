using System;
using System.Collections.Generic;

namespace BizConnect.Services.DTOs
{
    public class ChartData
    {
        public string ChartType { get; set; } = string.Empty;
        public string TimeRange { get; set; } = string.Empty;
        public List<ChartDataPoint> DataPoints { get; set; } = new();
        public ChartMetadata Metadata { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class ChartDataPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Value { get; set; }
        public string? Label { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class ChartMetadata
    {
        public string Title { get; set; } = string.Empty;
        public string XAxisLabel { get; set; } = string.Empty;
        public string YAxisLabel { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal MaxValue { get; set; }
        public decimal MinValue { get; set; }
        public List<string> Colors { get; set; } = new();
    }

    public class TrendDataPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Value { get; set; }
        public string MetricName { get; set; } = string.Empty;
    }
}