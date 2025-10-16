using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using NRules.Fluent.Dsl;
using NRules.RuleModel;

public class SumTeminatABRule : Rule
{
    public override void Define()
    {
#if DEBUG
        System.Diagnostics.Debugger.Break();
#endif
        Hdrules.NRules.NRulesContext? state = null;

        // IDE0350: Lambda expression can be simplified
        When()
            .Match<Hdrules.NRules.NRulesContext>(() => state!, s => true);

        // CS8604: Possible null reference argument for parameter 'state'
        Then()
            .Do((IContext ctx) => SumTeminatAb(state!));
    }

    private static void SumTeminatAb(Hdrules.NRules.NRulesContext state)
    {
        var root = state.Data as JsonNode;

        // D�ZELT�LM�� SATIR
        var data = root is JsonObject o && o.ContainsKey("Data") ? o["Data"] as JsonObject : null;

        var renk = data != null && data.ContainsKey("ARAC_RENGI") ? data["ARAC_RENGI"]?.ToString() : string.Empty;
        if (!string.Equals(renk, "kirmizi", StringComparison.OrdinalIgnoreCase))
        {
            state.__NRULES_OUTPUT["TOPLAM_TEMINAT_AB"] = 0m;
            return;
        }

        decimal toplam = 0m;
        JsonArray? arr = (data != null && data.ContainsKey("POLICE_TEMINATLARI")) ? data["POLICE_TEMINATLARI"] as JsonArray : null;

        if (arr != null)
        {
            foreach (var item in arr)
            {
                var jo = item as JsonObject;
                var kod = jo?["TEMINAT_KOD"]?.ToString();
                if (string.Equals(kod, "a", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kod, "b", StringComparison.OrdinalIgnoreCase))
                {
                    var s = jo?["BEDEL"]?.ToString();
                    if (decimal.TryParse(s ?? "0", out var d))
                        toplam += d;
                }
            }
        }

        state.__NRULES_OUTPUT["TOPLAM_TEMINAT_AB"] = toplam;
    }
}