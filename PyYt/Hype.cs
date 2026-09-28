using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PyYt;

/// <summary>
/// Mirrors py_yt's hype.py: view-count/age parsing helpers and the HypeHint
/// momentum scorer. Score, level thresholds and the (deterministic) formula are
/// ported verbatim, including Python's banker's rounding via MidpointRounding.ToEven.
/// </summary>
public static partial class Hype
{
    [GeneratedRegex(@"([\d.]+)\s*([kmb])")] private static partial Regex ShortCount();
    [GeneratedRegex(@"[^\d]")] private static partial Regex NonDigits();
    [GeneratedRegex(@"(\d+)\s*(minute|min|hour|hr|day|week|month|year)")] private static partial Regex Relative();

    // parse_view_count: extracts an integer view count from string, dict, or number.
    public static long ParseViewCount(JsonNode? val)
    {
        if (val is null) return 0;
        if (val is JsonObject obj) return ParseViewCount(obj["text"] ?? obj["short"]);
        if (val is JsonValue value)
        {
            if (value.TryGetValue<long>(out long l)) return Math.Max(0, l);
            if (value.TryGetValue<double>(out double d)) return Math.Max(0, (long)d);
            if (value.TryGetValue<string>(out string? s)) return ParseViewCount(s);
        }
        return 0;
    }

    public static long ParseViewCount(string? val)
    {
        if (val is null) return 0;
        string s = val.Trim().ToLowerInvariant();
        if (s.Length == 0 || s == "none") return 0;
        Match m = ShortCount().Match(s);
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
        {
            long multiplier = m.Groups[2].Value switch { "k" => 1_000L, "m" => 1_000_000L, "b" => 1_000_000_000L, _ => 1L };
            return Math.Max(0, (long)(num * multiplier));
        }
        string digits = NonDigits().Replace(s, "");
        if (digits.Length > 0 && long.TryParse(digits, out long parsed)) return Math.Max(0, parsed);
        return 0;
    }
    // parse_age_in_hours: prefer an ISO date (publish_date then published), else a relative phrase.
    public static double ParseAgeInHours(string? publishedVal, string? publishDateVal = null)
    {
        foreach (string? dateStr in new[] { publishDateVal, publishedVal })
        {
            if (dateStr is null) continue;
            if (!(dateStr.Contains('T') || dateStr.Contains('-'))) continue;
            string iso = dateStr.Replace("Z", "+00:00");
            if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed))
            {
                double diff = (DateTimeOffset.UtcNow - parsed).TotalHours;
                if (diff >= 0) return diff;
            }
        }

        string s = (publishedVal ?? string.Empty).Trim().ToLowerInvariant();
        if (s.Length > 0)
        {
            Match m = Relative().Match(s);
            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                string unit = m.Groups[2].Value;
                double hours = unit switch
                {
                    "minute" or "min" => val / 60.0,
                    "hour" or "hr" => val,
                    "day" => val * 24.0,
                    "week" => val * 24.0 * 7.0,
                    "month" => val * 24.0 * 30.4375,
                    "year" => val * 24.0 * 365.25,
                    _ => 24.0,
                };
                return Math.Max(0.001, hours);
            }
        }
        return 24.0;
    }

    // HypeHint.calculate_score: subscriberCount/likeCount are accepted for parity but unused, exactly as py_yt.
    public static double CalculateScore(JsonNode? viewCount, string? publishedTime = null, string? publishDate = null,
        JsonNode? subscriberCount = null, JsonNode? likeCount = null)
    {
        long views = ParseViewCount(viewCount);
        if (views <= 0) return 0.0;
        double effectiveAge = Math.Max(0.25, ParseAgeInHours(publishedTime, publishDate));
        double velocityScore = Math.Log10(views / effectiveAge + 1.0) * 18.0;
        double scaleBonus = Math.Log10(views + 1.0) * 3.5;
        double recencyBonus = Math.Exp(-effectiveAge / 120.0) * 15.0;
        double raw = velocityScore + scaleBonus + recencyBonus;
        return Math.Round(Math.Min(100.0, Math.Max(0.0, raw)), 2, MidpointRounding.ToEven);
    }

    // get_hype_level: descending threshold ladder.
    public static string GetHypeLevel(double score) => score switch
    {
        >= 85 => "Ultra Viral",
        >= 70 => "High Hype",
        >= 50 => "Trending",
        >= 30 => "Moderate",
        > 0 => "Low Hype",
        _ => "No Hype",
    };
    // analyze_video: viewCount||views, publishedTime||published, publishDate||uploadDate.
    public static JsonObject AnalyzeVideo(JsonNode? video)
    {
        JsonObject obj = video as JsonObject ?? new JsonObject();
        JsonNode? viewCount = obj["viewCount"] ?? obj["views"];
        string? publishedTime = AsText(obj["publishedTime"]) ?? AsText(obj["published"]);
        string? publishDate = AsText(obj["publishDate"]) ?? AsText(obj["uploadDate"]);

        long viewsInt = ParseViewCount(viewCount);
        double ageHours = ParseAgeInHours(publishedTime, publishDate);
        double score = CalculateScore(viewCount, publishedTime, publishDate);
        return new JsonObject
        {
            ["score"] = score,
            ["level"] = GetHypeLevel(score),
            ["view_count"] = viewsInt,
            ["age_hours"] = Math.Round(ageHours, 2, MidpointRounding.ToEven),
            ["velocity_views_per_hour"] = Math.Round(viewsInt / Math.Max(0.25, ageHours), 2, MidpointRounding.ToEven),
        };
    }

    // rank: attach a "hype" block to each video and sort by score descending. The returned array
    // always holds fresh clones (a JsonNode has a single parent); with inplace the originals are
    // annotated too, mirroring py_yt's in-place mutation of the source dicts.
    public static JsonArray Rank(JsonArray? videos, bool inplace = false)
    {
        var result = new JsonArray();
        if (videos is null) return result;
        var buffer = new List<(JsonNode Node, double Score)>();
        foreach (JsonNode? video in videos)
        {
            if (video is JsonObject obj)
            {
                JsonObject hype = AnalyzeVideo(obj);
                if (inplace) obj["hype"] = (JsonNode)hype.DeepClone();
                var annotated = (JsonObject)obj.DeepClone();
                if (!inplace) annotated["hype"] = hype;
                double score = annotated["hype"]?["score"]?.GetValue<double>() ?? 0.0;
                buffer.Add((annotated, score));
            }
            else
            {
                buffer.Add((video?.DeepClone() ?? new JsonObject(), 0.0));
            }
        }
        // Stable descending sort by score (Python's sorted(..., reverse=True) is stable).
        foreach ((JsonNode node, _) in buffer.OrderByDescending(e => e.Score))
            result.Add(node);
        return result;
    }

    private static string? AsText(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out string? s) ? s : node?.ToString();
}
