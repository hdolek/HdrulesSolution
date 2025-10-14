
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hdrules.Engine;

public static class JsonUtils
{
    public static JsonNode? Parse(string json) => JsonNode.Parse(json);

    // Very light JSON path (dot notation, e.g. Data.POLICE_ANA_BILGILER.POLICE_ID)
    public static JsonNode? GetByPath(JsonNode root, string path)
    {
        if (root is null || string.IsNullOrWhiteSpace(path)) return null;
        var p = path.Replace("$.","").Replace("$","");
        var parts = p.Split('.', StringSplitOptions.RemoveEmptyEntries);
        JsonNode? cur = root;
        foreach (var part in parts)
        {
            if (cur is null) return null;
            if (cur is JsonArray arr)
            {
                if (int.TryParse(part.Trim('[',']'), out var idx))
                    cur = idx >= 0 && idx < arr.Count ? arr[idx] : null;
                else return null;
            }
            else if (cur is JsonObject obj)
            {
                cur = obj.ContainsKey(part) ? obj[part] : null;
            }
            else return null;
        }
        return cur;
    }

    public static string? AsString(JsonNode? n) => n?.ToString();
    public static double? AsNumber(JsonNode? n) => n is null ? null : double.TryParse(n.ToString(), out var d) ? d : null;
    public static DateTime? AsDate(JsonNode? n) => n is null ? null : DateTime.TryParse(n.ToString(), out var dt) ? dt : null;
}
