
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http;
using System.Threading.Tasks;
using Hdrules.Domain;
using Hdrules.Data;

namespace Hdrules.Engine;

public record DecisionCell(string COL_CODE, object? Value, Dictionary<string,string>? Attributes);
public record DecisionRow(int ROW_NO, bool IS_DEFAULT, List<DecisionCell> Cells);
public record DecisionResult(string GROUP_CODE, string EVALUATED_RULE_CODE, List<DecisionRow> Rows);

public class DecisionEngine
{
    private readonly DecisionRepository _repo;
    private readonly HttpClient _http;
    private readonly Func<string, JsonNode, Task<Dictionary<string, object>>> _nRulesInvoker;
    public DecisionEngine(DecisionRepository repo, HttpClient http, Func<string, JsonNode, Task<Dictionary<string, object>>> nRulesInvoker)
    {
        _repo = repo; _http = http; _nRulesInvoker = nRulesInvoker;
    }

    public async Task<DecisionResult?> EvaluateAsync(string groupCode, string inputJson, DateTime? asOf=null)
    {
        var group = await _repo.GetGroupByCodeAsync(groupCode);
        if (group is null || group.IS_ACTIVE == 0) return null;

        var rules = (await _repo.GetActiveRulesAsync(group.RULE_GROUP_ID)).ToList();
        if (rules.Count == 0) return null;

        var conds = (await _repo.GetConditionsAsync(rules.Select(r=>r.RULE_ID))).ToList();
        var condSets = (await _repo.GetCondSetValuesAsync(conds.Select(c=>c.CONDITION_ID))).GroupBy(x=>x.CONDITION_ID).ToDictionary(g=>g.Key, g=>g.Select(x=>x.VALUE_TEXT).ToList());
        var rows = (await _repo.GetOutputRowsAsync(rules.Select(r=>r.RULE_ID))).ToList();
        var cells = (await _repo.GetOutputCellsAsync(rows.Select(x=>x.OUTPUT_ROW_ID))).ToList();
        var outCols = (await _repo.GetOutputColsAsync(group.RULE_GROUP_ID)).ToList();
        var outAttrs = (await _repo.GetOutputColAttrsAsync(outCols.Select(c=>c.OUTPUT_COL_ID))).GroupBy(a=>a.OUTPUT_COL_ID).ToDictionary(g=>g.Key, g=>g.ToDictionary(a=>a.ATTR_CODE, a=>a.ATTR_VALUE ?? ""));

        var json = JsonNode.Parse(inputJson) ?? new JsonObject();

        foreach (var rule in rules)
        {
            var rConds = conds.Where(c => c.RULE_ID == rule.RULE_ID).OrderBy(c => c.ORDINAL).ToList();
            bool ok = true;
            foreach (var c in rConds)
            {
                string leftVal = "";
                if (!string.IsNullOrWhiteSpace(c.LEFT_JSON_PATH))
                    leftVal = JsonUtils.GetByPath(json, c.LEFT_JSON_PATH)?.ToString() ?? "";
                else if (!string.IsNullOrWhiteSpace(c.LEFT_PARAM_CODE))
                    leftVal = json[c.LEFT_PARAM_CODE]?.ToString() ?? "";
                leftVal = Op.Transform(leftVal, c.LEFT_TRANSFORMS);

                bool passed = false;
                if (c.VALUE_TYPE == "SET")
                {
                    var set = condSets.TryGetValue(c.CONDITION_ID, out var lst) ? lst : new List<string>();
                    passed = Op.InSet(leftVal, set, c.CASE_SENSITIVE == 1);
                }
                else if (c.VALUE_TYPE == "RANGE")
                {
                    passed = Op.Evaluate("BETWEEN", leftVal, c.VALUE_TEXT, c.VALUE_TO_TEXT, c.CASE_SENSITIVE == 1);
                }
                else
                {
                    var right = c.VALUE_TYPE == "WILDCARD" ? "*" :
                                (c.VALUE_TYPE == "JSONPATH" ? (JsonUtils.GetByPath(json, c.VALUE_TEXT ?? "")?.ToString() ?? "") : c.VALUE_TEXT);
                    passed = Op.Evaluate(c.OPERATOR, leftVal, right, c.VALUE_TO_TEXT, c.CASE_SENSITIVE == 1);
                }
                if (c.NEGATE == 1) passed = !passed;
                if (!passed) { ok = false; break; }
            }
            if (!ok) continue;

            // Build outputs for this rule
            var rRows = rows.Where(x=>x.RULE_ID==rule.RULE_ID).OrderBy(x=>x.ROW_NO).ToList();
            var resultRows = new List<DecisionRow>();
            foreach (var r in rRows)
            {
                var rCells = cells.Where(x=>x.OUTPUT_ROW_ID==r.OUTPUT_ROW_ID).ToList();
                var cellsOut = new List<DecisionCell>();
                foreach (var cell in rCells)
                {
                    var col = outCols.First(oc => oc.OUTPUT_COL_ID == cell.OUTPUT_COL_ID);
                    var attr = outAttrs.TryGetValue(col.OUTPUT_COL_ID, out var a) ? a : new Dictionary<string,string>();

                    object? val = null;
                    switch (cell.SOURCE_TYPE)
                    {
                        case "CONST":
                            val = CastTo(col.DATA_TYPE, cell.CONST_VALUE);
                            break;
                        case "INPUT":
                        case "JSON":
                            {
                                var node = !string.IsNullOrEmpty(cell.JSON_PATH) ? JsonUtils.GetByPath(json, cell.JSON_PATH!) : json[col.COL_CODE];
                                var s = node?.ToString();
                                s = Op.Transform(s, cell.TRANSFORM_CHAIN);
                                val = CastTo(col.DATA_TYPE, s);
                                break;
                            }
                        case "API":
                            {
                                // demo http call by template - real implementation should use API_DEF table
                                // Here we just echo back CONST_VALUE or input value
                                var from = !string.IsNullOrEmpty(cell.JSON_PATH) ? JsonUtils.GetByPath(json, cell.JSON_PATH!)?.ToString() : cell.CONST_VALUE;
                                val = CastTo(col.DATA_TYPE, from);
                                break;
                            }
                        case "NRULES":
                            {
                                var outputs = await _nRulesInvoker(cell.NRULES_CODE ?? "", json);
                                if (outputs != null && outputs.TryGetValue(col.COL_CODE, out var v)) val = v;
                                break;
                            }
                    }
                    cellsOut.Add(new DecisionCell(col.COL_CODE, val, attr));
                }
                resultRows.Add(new DecisionRow(r.ROW_NO, r.IS_DEFAULT == 1, cellsOut));
            }
            return new DecisionResult(group.GROUP_CODE, rule.RULE_CODE, resultRows);
        }

        return null;
    }

    private static object? CastTo(string dataType, string? s)
    {
        if (s is null) return null;
        return dataType switch {
            "NUMBER" => double.TryParse(s, out var d) ? d : null,
            "BOOLEAN" => s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase),
            "DATE" => DateTime.TryParse(s, out var dt) ? dt : null,
            "JSON" => s,
            _ => s
        };
    }
}
