using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hdrules.Engine;
using Hdrules.NRules;
using Hdrules.Data;

namespace Hdrules.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly DecisionEngine _engine;
        private readonly DynamicNRulesHost _nrHost;
        private readonly DecisionRepository _repo;

        public TestController(DecisionEngine engine, DynamicNRulesHost nrHost, DecisionRepository repo)
        {
            _engine = engine;
            _nrHost = nrHost;
            _repo = repo;
        }

        [HttpGet("health")]
        public IActionResult Health() => Ok(new { status = "ok" });

        /// <summary>
        /// Karar tablosu - ARAC_BILGI için örnek test.
        /// </summary>
        [HttpPost("decision/arac-bilgi")]
        public async Task<IActionResult> DecisionAracBilgi([FromBody] JsonElement? payload)
        {
            string body;
            if (payload.HasValue)
                body = payload.Value.GetRawText();
            else
            {
                body = JsonSerializer.Serialize(new {
                    ARAC_MODEL_YIL = 2005,
                    ARAC_MARKA = "BMW",
                    ARAC_MARKA_TIP = "X5",
                    ARAC_PLAKA = "34M1234"
                });
            }
            var res = await _engine.EvaluateAsync("ARAC_BILGI", body);
            return Ok(res);
        }

        /// <summary>
        /// Karar tablosu - IKAME_ARAC_SECIMI için örnek test.
        /// </summary>
        [HttpPost("decision/ikame")]
        public async Task<IActionResult> DecisionIkame([FromBody] JsonElement? payload)
        {
            string body;
            if (payload.HasValue)
                body = payload.Value.GetRawText();
            else
            {
                body = JsonSerializer.Serialize(new {
                    KULLANIM_TARZI = "hususi",
                    ARAC_MARKA = "BMW",
                    MODEL_YILI = 2018
                });
            }
            var res = await _engine.EvaluateAsync("IKAME_ARAC_SECIMI", body);
            return Ok(res);
        }

        /// <summary>
        /// Dinamik NRules - SUM_TEMINAT_A_B örneği (kırmızı araçta a+b toplamı).
        /// </summary>
        [HttpPost("nrules/sum-teminat-ab")]
        public async Task<IActionResult> RunNRules([FromBody] JsonElement? payload)
        {
            JsonNode node;
            if (payload.HasValue)
                node = JsonNode.Parse(payload.Value.GetRawText()) ?? new JsonObject();
            else
            {
                node = JsonNode.Parse(@"{ ""Data"": { ""ARAC_RENGI"": ""kirmizi"", ""POLICE_TEMINATLARI"": [ { ""TEMINAT_KOD"": ""a"", ""BEDEL"": ""100.50"" }, { ""TEMINAT_KOD"": ""b"", ""BEDEL"": ""200.50"" }, { ""TEMINAT_KOD"": ""c"", ""BEDEL"": ""5"" } ] } }")!;
            }

            var ctx = new NRulesContext { Data = node };
            var res = await _nrHost.EvaluateAsync("SUM_TEMINAT_A_B", ctx);
            return Ok(res);
        }

        /// <summary>
        /// Karar tablosu - herhangi bir groupCode için body ile test.
        /// </summary>
        [HttpPost("decision/{groupCode}")]
        public async Task<IActionResult> DecisionCustom([FromRoute] string groupCode, [FromBody] JsonElement payload)
        {
            var body = payload.GetRawText();
            var res = await _engine.EvaluateAsync(groupCode, body);
            return Ok(res);
        }

        /// <summary>
        /// DB hızlı özet (aktif kural sayısı vs.). Test amaçlıdır.
        /// </summary>
        [HttpGet("db/summary/{groupCode}")]
        public async Task<IActionResult> DbSummary([FromRoute] string groupCode)
        {
            var g = await _repo.GetGroupByCodeAsync(groupCode);
            if (g == null) return NotFound(new { message = $"Group '{groupCode}' bulunamadı" });
            var rules = await _repo.GetActiveRulesAsync(g.RULE_GROUP_ID);
            return Ok(new {
                g.GROUP_CODE,
                g.GROUP_NAME,
                ACTIVE_RULE_COUNT = rules is null ? 0 : System.Linq.Enumerable.Count(rules)
            });
        }
    }
}