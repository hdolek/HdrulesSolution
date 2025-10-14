
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using System.Text.Json.Nodes;

using Hdrules.Data;
using Hdrules.Engine;
using Hdrules.Cache;
using Hdrules.NRules;

var builder = WebApplication.CreateBuilder(args);

// Config
var connStr = builder.Configuration.GetConnectionString("Oracle") ?? "User Id=USER;Password=PWD;Data Source=HOST:1521/ORCLPDB1";
var useRedis = builder.Configuration.GetValue<bool>("Cache:UseRedis");

// Services
builder.Services.AddSingleton(new DbConnectionFactory(connStr));
builder.Services.AddSingleton<DecisionRepository>();
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheProvider>(sp => {
    if (useRedis) {
        var mux = StackExchange.Redis.ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");
        return new RedisCacheProvider(mux);
    }
    else {
        return new MemoryCacheProvider(sp.GetRequiredService<IMemoryCache>());
    }
});
builder.Services.AddSingleton<DynamicNRulesHost>();
builder.Services.AddSingleton<DecisionEngine>(sp =>
{
    var repo = sp.GetRequiredService<DecisionRepository>();
    var http = sp.GetRequiredService<HttpClient>();
    var nr = sp.GetRequiredService<DynamicNRulesHost>();
    return new DecisionEngine(repo, http, async (code, jsonNode) => {
        // Wrap JsonObject as dynamic to pass to NRules
        // In real impl, you'd use a strongly-typed model. Here we pass JsonObject directly.
        var ctx = new Hdrules.NRules.NRulesContext { Data = jsonNode };
        return await nr.EvaluateAsync(code, ctx);
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var roslynDebugText = builder.Configuration["Roslyn:Debug"];
bool roslynDebug = builder.Environment.IsDevelopment();
if (bool.TryParse(roslynDebugText, out var cfgDebug)) roslynDebug = roslynDebug || cfgDebug;
builder.Services.AddSingleton(new Hdrules.NRules.RoslynCompileOptions { Debug = roslynDebug });

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapPost("/evaluate/decision-table/{groupCode}", async (string groupCode, DecisionRepository repo, DecisionEngine engine, HttpRequest req) =>
{
    using var reader = new System.IO.StreamReader(req.Body);
    var json = await reader.ReadToEndAsync();
    var res = await engine.EvaluateAsync(groupCode, json);
    return Results.Json(res);
});

app.MapPost("/evaluate/nrules/{ruleCode}", async (string ruleCode, DynamicNRulesHost host, HttpRequest req) =>
{
    using var reader = new System.IO.StreamReader(req.Body);
    var json = await reader.ReadToEndAsync();
    var node = JsonNode.Parse(json);
    var facts = new { Data = node?["Data"], __NRULES_OUTPUT = new System.Collections.Generic.Dictionary<string, object>() };
    var res = await host.EvaluateAsync(ruleCode, facts);
    return Results.Json(res);
});

app.MapControllers();

app.Run();
