
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Hdrules.Engine;

public enum ValueTypeKind { SCALAR, RANGE, SET, WILDCARD, JSONPATH }

public static class Op
{
    public static bool Evaluate(string op, string? left, string? right, string? rightTo = null, bool caseSensitive = false)
    {
        var cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        switch (op)
        {
            case "EQ": return right == "*" ? true : string.Equals(left ?? "", right ?? "", cmp);
            case "NE": return !string.Equals(left ?? "", right ?? "", cmp);
            case "GT": return CompareNum(left, right) > 0;
            case "GE": return CompareNum(left, right) >= 0;
            case "LT": return CompareNum(left, right) < 0;
            case "LE": return CompareNum(left, right) <= 0;
            case "BETWEEN": return Between(left, right, rightTo);
            case "LIKE": return Like(left, right, caseSensitive);
            case "ILIKE": return Like(left, right, false);
            case "STARTSWITH": return (left ?? "").StartsWith(right ?? "", cmp);
            case "ENDSWITH": return (left ?? "").EndsWith(right ?? "", cmp);
            case "CONTAINS": return (left ?? "").IndexOf(right ?? "", cmp) >= 0;
            case "REGEX": return Regex.IsMatch(left ?? "", right ?? "", caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
            case "EXISTS": return !string.IsNullOrEmpty(left);
            case "EMPTY": return string.IsNullOrEmpty(left);
            default: return false;
        }
    }

    public static bool InSet(string? left, IEnumerable<string> set, bool caseSensitive=false)
    {
        foreach (var s in set)
            if (Evaluate("EQ", left, s, null, caseSensitive)) return true;
        return false;
    }

    private static int CompareNum(string? l, string? r)
    {
        if (double.TryParse(l, out var ld) && double.TryParse(r, out var rd))
            return ld.CompareTo(rd);
        return string.Compare(l ?? "", r ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Between(string? l, string? a, string? b)
    {
        if (double.TryParse(l, out var ld) && double.TryParse(a, out var ad) && double.TryParse(b, out var bd))
            return ld >= ad && ld <= bd;
        if (DateTime.TryParse(l, out var dt) && DateTime.TryParse(a, out var da) && DateTime.TryParse(b, out var db))
            return dt >= da && dt <= db;
        return false;
    }

    private static bool Like(string? l, string? pattern, bool caseSensitive)
    {
        if (pattern is null) return false;
        var regex = "^" + Regex.Escape(pattern).Replace("\\%", ".*").Replace("\\_", ".") + "$";
        var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        return Regex.IsMatch(l ?? "", regex, options);
    }

    public static string Transform(string? value, string? chain)
    {
        if (value is null) return "";
        if (string.IsNullOrWhiteSpace(chain)) return value;
        var v = value;
        foreach (var f in chain.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var fn = f.Trim();
            if (fn.Equals("UPPER", StringComparison.OrdinalIgnoreCase)) v = v.ToUpperInvariant();
            else if (fn.Equals("LOWER", StringComparison.OrdinalIgnoreCase)) v = v.ToLowerInvariant();
            else if (fn.StartsWith("SUBSTR", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(fn, @"SUBSTR\((\d+),(\d+)\)");
                if (m.Success)
                {
                    var start = int.Parse(m.Groups[1].Value);
                    var len = int.Parse(m.Groups[2].Value);
                    var s = start - 1;
                    if (s >= 0 && s < v.Length)
                    {
                        var take = Math.Min(len, v.Length - s);
                        v = v.Substring(s, take);
                    }
                    else v = "";
                }
            }
            else if (fn.Equals("TRIM", StringComparison.OrdinalIgnoreCase)) v = v.Trim();
        }
        return v;
    }
}
