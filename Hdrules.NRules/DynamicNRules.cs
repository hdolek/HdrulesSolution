// ---------- DynamicNRules.cs (clean, compile-ready) ----------
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System.Runtime.Loader;

using Hdrules.Data;
using Hdrules.NRules;
using NRules;
using NRules.Fluent;
using NRules.Fluent.Dsl;
using NRules.RuleModel;

// Alias, NRules.Fluent.RuleRepository ile Hdrules.Data.RuleRepository çakışmasın
using RuleRepositoryNR = NRules.Fluent.RuleRepository;

namespace Hdrules.NRules
{
    public class DynamicNRulesHost
    {
        private readonly DecisionRepository _repo;                   // Hdrules.Data.RuleRepository
        private readonly RoslynCompileOptions _opts;

        // Ana ctor (DI bunu kullanır)
        public DynamicNRulesHost(DecisionRepository repo, RoslynCompileOptions opts)
        {
            _repo = repo;
            _opts = opts ?? new RoslynCompileOptions { Debug = false };
        }

        // Fallback ctor (options kaydı yoksa bile çalışsın)
        public DynamicNRulesHost(DecisionRepository repo) : this(repo, new RoslynCompileOptions { Debug = false }) { }

        // --- Güvenlik (basit blacklist) + derleme cache ---
        private static readonly string[] _blacklistTokens = new[]
        {
            "System.IO", "System.Diagnostics.Process", "System.Net.Sockets",
            "System.Runtime.InteropServices", "Environment.Exit", "Process.", "File.", "Directory."
        };

        private readonly ConcurrentDictionary<string, (Assembly asm, string sig)> _nrCache = new();

        private static void ValidateSources(IEnumerable<string> sources)
        {
            foreach (var src in sources)
            {
                foreach (var bad in _blacklistTokens)
                {
                    if (src.Contains(bad, StringComparison.Ordinal))
                        throw new Exception($"NRules source blocked by whitelist: token='{bad}'");
                }
            }
        }

        private static string Signature(IEnumerable<string> sources)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var joined = string.Join("§", sources);
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(joined));
            return BitConverter.ToString(hash).Replace("-", "");
        }

        // --- Public API ---
        public async Task<Dictionary<string, object>> EvaluateAsync(string code, object facts)
        {
            var defs = (await _repo.GetNRulesAsync(code)).ToList();
            if (defs.Count == 0) return new Dictionary<string, object>();

            var sources = defs.Select(d => d.CSHARP_CODE).ToList();
            ValidateSources(sources);

            var sig = Signature(sources);
            if (!_nrCache.TryGetValue(code, out var cached) || cached.sig != sig)
            {
                var compiled = Compile(sources);
                _nrCache[code] = (compiled, sig);
            }
            var asm = _nrCache[code].asm;

            var repository = new RuleRepositoryNR();
            repository.Load(x => x.From(asm));
            var factory = repository.Compile();
            var session = factory.CreateSession();

            session.Insert(facts);
            session.Fire();

            // __NRULES_OUTPUT sözlüğünü dön
            if (facts is NRulesContext ctx) return ctx.__NRULES_OUTPUT;

            var prop = facts?.GetType().GetProperty("__NRULES_OUTPUT", BindingFlags.Public | BindingFlags.Instance);
            var dict = prop?.GetValue(facts) as Dictionary<string, object>;
            return dict ?? new Dictionary<string, object>();
        }

        // --- Roslyn Compile (DEBUG/RELEASE ayrımı + encoding fix + referanslar) ---
        private Assembly Compile(IEnumerable<string> sources)
        {
            if (_opts.Debug)
            {
                var parseOptions = new CSharpParseOptions(LanguageVersion.Latest)
                    .WithPreprocessorSymbols(new[] { "DEBUG" });

                var debugDir = Path.Combine(AppContext.BaseDirectory, ".roslynrules",
                    DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff"));
                Directory.CreateDirectory(debugDir);

                var trees = sources.Select((src, i) =>
                {
                    var fp = Path.Combine(debugDir, $"NR_{i}.cs");
                    File.WriteAllText(fp, src, new UTF8Encoding(false));           // dosyayı UTF8 yaz
                    var text = SourceText.From(src, Encoding.UTF8);                 // CS8055 için encoding ver
                    return CSharpSyntaxTree.ParseText(text, options: parseOptions, path: fp);
                }).ToArray();

                var compOptions = new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Debug,
                    checkOverflow: true,
                    nullableContextOptions: NullableContextOptions.Enable);

                var refs = BuildReferences();
                var compilation = CSharpCompilation.Create(
                    "NRulesDyn_" + Guid.NewGuid().ToString("N"),
                    syntaxTrees: trees, references: refs, options: compOptions);

                using var pe = new MemoryStream();
                using var pdb = new MemoryStream();
                var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);
                var result = compilation.Emit(pe, pdbStream: pdb, options: emitOptions);

                if (!result.Success)
                {
                    var errors = string.Join(Environment.NewLine,
                        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
                    throw new Exception("NRules dynamic compile error: " + errors);
                }

                pe.Position = 0; pdb.Position = 0;
                return AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
            }
            else
            {
                var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
                var trees = sources.Select(src => CSharpSyntaxTree.ParseText(src, parseOptions)).ToArray();

                var compOptions = new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    nullableContextOptions: NullableContextOptions.Disable);

                var refs = BuildReferences();
                var compilation = CSharpCompilation.Create(
                    "NRulesDyn_" + Guid.NewGuid().ToString("N"),
                    syntaxTrees: trees, references: refs, options: compOptions);

                using var ms = new MemoryStream();
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    var errors = string.Join(Environment.NewLine,
                        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
                    throw new Exception("NRules dynamic compile error: " + errors);
                }

                ms.Position = 0;
                return Assembly.Load(ms.ToArray());
            }
        }

        private static List<MetadataReference> BuildReferences()
        {
            var refs = new List<MetadataReference>
            {
                // temel
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),

                // NRules
                MetadataReference.CreateFromFile(typeof(Rule).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ISession).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IContext).Assembly.Location),

                // Bizim context
                MetadataReference.CreateFromFile(typeof(NRulesContext).Assembly.Location),

                // JSON Nodes
                MetadataReference.CreateFromFile(typeof(System.Text.Json.Nodes.JsonNode).Assembly.Location),
            };

            // netstandard + facade'lar
            string[] byName =
            {
                "netstandard",
                "System.Runtime",
                "System.Linq",
                "System.Linq.Expressions",
                "System.Collections",
                "System.Console",
                "System.Net.Http",
            };
            foreach (var name in byName)
            {
                try
                {
                    var asm = Assembly.Load(new AssemblyName(name));
                    if (asm != null && !string.IsNullOrEmpty(asm.Location))
                        refs.Add(MetadataReference.CreateFromFile(asm.Location));
                }
                catch { /* ignore */ }
            }

            // dynamic/binder yardımcıları
            try { refs.Add(MetadataReference.CreateFromFile(typeof(System.Dynamic.ExpandoObject).Assembly.Location)); } catch { }
            try { refs.Add(MetadataReference.CreateFromFile(typeof(System.Linq.Expressions.Expression).Assembly.Location)); } catch { }
            try { refs.Add(MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly.Location)); } catch { }

            return refs;
        }
    }
}
