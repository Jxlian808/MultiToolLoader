using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MultiToolLoader.Models
{
    public class AIModel
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string ApiEndpoint { get; set; }
        public int MaxTokens { get; set; } = 1024;
        public double Temperature { get; set; } = 0.7;
        public string SystemPrompt { get; set; } = "";
        public int RequestsPerMinute { get; set; } = 10;
        public decimal CostPerRequest { get; set; }
        public ModelCapabilities Capabilities { get; set; } = new();
        public Dictionary<string, string> Parameters { get; set; } = new();
        public List<string> SupportedLanguages { get; set; } = new();
        public ModelUsageStats UsageStats { get; set; } = new();
        public ModelPricing Pricing { get; set; } = new();
        public Dictionary<string, List<string>> SpecializedPrompts { get; set; } = new();

        [JsonIgnore]
        public bool IsAvailable => !MaintenanceMode && ErrorRate < 0.1;

        public bool MaintenanceMode { get; set; }
        public double ErrorRate { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        public string Version { get; set; } = "1.0";

        public Dictionary<string, double> GetPerformanceMetrics()
        {
            return new Dictionary<string, double>
            {
                { "average_response_time", UsageStats.AverageResponseTime },
                { "success_rate", 1 - ErrorRate },
                { "tokens_per_second", UsageStats.TokensPerSecond },
                { "cost_efficiency", CalculateCostEfficiency() }
            };
        }

        private double CalculateCostEfficiency()
        {
            if (Pricing.CostPerToken == 0) return 0;
            return (double)(UsageStats.TotalSuccessfulRequests * UsageStats.AverageTokensPerRequest) /
                   (double)(UsageStats.TotalCost);
        }

        public bool SupportsFeature(string feature)
        {
            return Capabilities.SupportedFeatures.Contains(feature);
        }

        public string GetSpecializedPrompt(string category, string subcategory)
        {
            if (SpecializedPrompts.TryGetValue(category, out var prompts))
            {
                var index = prompts.FindIndex(p => p.StartsWith(subcategory + ":"));
                if (index >= 0)
                {
                    return prompts[index].Substring(subcategory.Length + 1).Trim();
                }
            }
            return SystemPrompt;
        }
    }

    public class ModelCapabilities
    {
        public List<string> SupportedFeatures { get; set; } = new();
        public int MaxInputLength { get; set; } = 4096;
        public int MaxOutputLength { get; set; } = 2048;
        public bool SupportsStreaming { get; set; }
        public bool SupportsCodeCompletion { get; set; }
        public bool SupportsImageGeneration { get; set; }
        public Dictionary<string, double> FeatureScores { get; set; } = new();
    }

    public class ModelUsageStats
    {
        public long TotalRequests { get; set; }
        public long TotalSuccessfulRequests { get; set; }
        public long TotalFailedRequests { get; set; }
        public long TotalTokensProcessed { get; set; }
        public decimal TotalCost { get; set; }
        public double AverageResponseTime { get; set; }
        public int AverageTokensPerRequest { get; set; }
        public double TokensPerSecond { get; set; }
        public Dictionary<string, int> ErrorTypes { get; set; } = new();
        public Dictionary<string, int> UsageByLanguage { get; set; } = new();

        public void UpdateStats(bool success, int tokens, double responseTime, string? errorType = null, string? language = null)
        {
            TotalRequests++;
            if (success)
            {
                TotalSuccessfulRequests++;
                TotalTokensProcessed += tokens;
                AverageTokensPerRequest = (int)((TotalTokensProcessed / (double)TotalSuccessfulRequests));
                TokensPerSecond = TotalTokensProcessed / (DateTime.UtcNow - FirstRequest).TotalSeconds;
            }
            else
            {
                TotalFailedRequests++;
                if (errorType != null)
                {
                    ErrorTypes[errorType] = ErrorTypes.GetValueOrDefault(errorType, 0) + 1;
                }
            }

            if (language != null)
            {
                UsageByLanguage[language] = UsageByLanguage.GetValueOrDefault(language, 0) + 1;
            }

            AverageResponseTime = ((AverageResponseTime * (TotalRequests - 1)) + responseTime) / TotalRequests;
        }

        public DateTime FirstRequest { get; set; } = DateTime.UtcNow;
        public DateTime LastRequest { get; set; } = DateTime.UtcNow;
    }

    public class ModelPricing
    {
        public decimal CostPerToken { get; set; }
        public decimal MinimumCost { get; set; }
        public Dictionary<string, decimal> FeatureCosts { get; set; } = new();
        public List<PricingTier> PricingTiers { get; set; } = new();
        public bool HasDynamicPricing { get; set; }
        public DateTime LastPriceUpdate { get; set; } = DateTime.UtcNow;

        public decimal CalculateCost(int tokens, string? feature = null)
        {
            var baseCost = tokens * CostPerToken;
            if (feature != null && FeatureCosts.TryGetValue(feature, out var featureCost))
            {
                baseCost += featureCost;
            }

            foreach (var tier in PricingTiers.OrderByDescending(t => t.TokenThreshold))
            {
                if (tokens >= tier.TokenThreshold)
                {
                    baseCost *= tier.DiscountMultiplier;
                    break;
                }
            }

            return Math.Max(MinimumCost, baseCost);
        }
    }

    public class PricingTier
    {
        public int TokenThreshold { get; set; }
        public decimal DiscountMultiplier { get; set; } = 1.0m;
        public string TierName { get; set; } = "";
    }
}