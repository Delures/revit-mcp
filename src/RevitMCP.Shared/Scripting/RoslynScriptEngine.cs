using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace RevitMCP.Shared.Scripting
{
    public class RoslynScriptEngine
    {
#if !NET8_0_OR_GREATER
        private static readonly ConcurrentDictionary<string, MethodInfo> _cachedMethods = new ConcurrentDictionary<string, MethodInfo>();
#endif

        public ExecuteScriptResult Execute(string rawCode, IScriptExecutionContext context, Dictionary<string, object>? arguments = null)
        {
            var sw = Stopwatch.StartNew();
            var result = new ExecuteScriptResult();

            try
            {
                string completeCode = PrepareSourceCode(rawCode);
                MethodInfo runMethod = CompileOrGetMethod(completeCode);

                object? returnValue = runMethod.Invoke(null, new object?[] { context, arguments });

                sw.Stop();
                result.Success = true;
                result.ReturnValue = returnValue;
                result.ConsoleOutput = string.Join(Environment.NewLine, context.GetLogs());
                result.ExecutionTimeMs = sw.ElapsedMilliseconds;
                result.AffectedElementIds = new List<long>(context.GetAffectedElementIds());

                var warnings = context.GetWarnings();
                if (warnings.Count > 0) result.Warnings = new List<WarningRecord>(warnings);

                var dialogs = context.GetCapturedDialogs();
                if (dialogs.Count > 0) result.CapturedDialogs = new List<CapturedDialogRecord>(dialogs);
            }
            catch (TargetInvocationException ex)
            {
                sw.Stop();
                Exception inner = ex.InnerException ?? ex;
                result.Success = false;
                result.ErrorCode = "SCRIPT_EXECUTION_ERROR";
                result.ErrorMessage = $"{inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}";
                result.ConsoleOutput = string.Join(Environment.NewLine, context.GetLogs());
                result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            }
            catch (RoslynCompilationException ex)
            {
                sw.Stop();
                result.Success = false;
                result.ErrorCode = "SCRIPT_COMPILATION_ERROR";
                result.ErrorMessage = ex.Message;
                result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Success = false;
                result.ErrorCode = "SCRIPT_INTERNAL_ERROR";
                result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
                result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            }

            return result;
        }

        private static string PrepareSourceCode(string rawCode)
        {
            if (rawCode.Contains("class ") && rawCode.Contains("Run("))
            {
                return rawCode;
            }

            bool hasRevitApi = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "RevitAPI");

            var rawLines = rawCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var customUsings = new List<string>();
            var bodyLines = new List<string>();

            foreach (var line in rawLines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("using ") && trimmed.EndsWith(";") && !trimmed.Contains("(") && !trimmed.Contains("="))
                {
                    customUsings.Add(trimmed);
                }
                else
                {
                    bodyLines.Add(line);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine("using System.Diagnostics;");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using RevitMCP.Shared.Scripting;");

            if (hasRevitApi)
            {
                sb.AppendLine("using Autodesk.Revit.DB;");
                sb.AppendLine("using Autodesk.Revit.UI;");
                sb.AppendLine("using Autodesk.Revit.ApplicationServices;");
                sb.AppendLine("using Autodesk.Revit.DB.Architecture;");
                sb.AppendLine("using Autodesk.Revit.DB.Structure;");
                sb.AppendLine("using Autodesk.Revit.DB.Mechanical;");
                sb.AppendLine("using Autodesk.Revit.DB.Plumbing;");
                sb.AppendLine("using Autodesk.Revit.DB.Electrical;");
            }

            foreach (var u in customUsings)
            {
                sb.AppendLine(u);
            }

            sb.AppendLine();
            sb.AppendLine("namespace RevitMCP.Dynamic");
            sb.AppendLine("{");
            sb.AppendLine("    public class ScriptHost");
            sb.AppendLine("    {");
            sb.AppendLine("        public dynamic Context { get; set; } = null!;");
            sb.AppendLine("        public Dictionary<string, object>? Args { get; set; }");
            sb.AppendLine("        public dynamic context => Context;");
            sb.AppendLine("        public Dictionary<string, object>? args => Args;");
            sb.AppendLine();
            if (hasRevitApi)
            {
                sb.AppendLine("        public UIApplication App => Context.App;");
                sb.AppendLine("        public UIApplication UIApplication => Context.App;");
                sb.AppendLine("        public UIDocument? UIDoc => Context.UIDoc;");
                sb.AppendLine("        public UIDocument? UIDocument => Context.UIDoc;");
                sb.AppendLine("        public Document? Doc => Context.Doc;");
                sb.AppendLine("        public Document? Document => Context.Doc;");
                sb.AppendLine("        public T WithTransaction<T>(string name, Func<T> action) => Context.WithTransaction(name, action);");
                sb.AppendLine("        public void WithTransaction(string name, Action action) => Context.WithTransaction(name, action);");
            }
            sb.AppendLine("        public void Print(object? msg) => Context.Print(msg?.ToString() ?? \"\");");
            sb.AppendLine("        public void Log(object? msg) => Context.Print(msg?.ToString() ?? \"\");");
            sb.AppendLine();
            sb.AppendLine("        public object? Execute()");
            sb.AppendLine("        {");
            sb.AppendLine(string.Join(Environment.NewLine, bodyLines));
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static class DynamicScript");
            sb.AppendLine("    {");
            sb.AppendLine("        public static object? Run(dynamic context, Dictionary<string, object>? args)");
            sb.AppendLine("        {");
            sb.AppendLine("            var host = new ScriptHost { Context = context, Args = args };");
            sb.AppendLine("            return host.Execute();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static MethodInfo CompileOrGetMethod(string sourceCode)
        {
#if !NET8_0_OR_GREATER
            string hash = ComputeSha256(sourceCode);
            if (_cachedMethods.TryGetValue(hash, out var cachedMethod))
            {
                return cachedMethod;
            }
#endif

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            string assemblyName = $"RevitMCP_Script_{Guid.NewGuid():N}";

            var references = new List<MetadataReference>();

            // Ensure fundamental runtime references are always present
            var coreAssemblies = new[]
            {
                typeof(object).Assembly,
                typeof(Console).Assembly,
                typeof(Enumerable).Assembly,
                typeof(IScriptExecutionContext).Assembly,
                typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly,
                typeof(System.Text.Json.JsonSerializer).Assembly
            };

            foreach (var asm in coreAssemblies)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(asm.Location))
                    {
                        references.Add(MetadataReference.CreateFromFile(asm.Location));
                    }
                }
                catch { }
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                    {
                        references.Add(MetadataReference.CreateFromFile(assembly.Location));
                    }
                }
                catch
                {
                    // Ignore inaccessible assemblies
                }
            }

            var specificDiagnostics = new Dictionary<string, ReportDiagnostic>
            {
                { "CS0122", ReportDiagnostic.Suppress },
                { "CS0433", ReportDiagnostic.Suppress }
            };

            var compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default)
                .WithSpecificDiagnosticOptions(specificDiagnostics);

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                compilationOptions);

            using (var ms = new MemoryStream())
            {
                EmitResult emitResult = compilation.Emit(ms);

                if (!emitResult.Success)
                {
                    var failures = emitResult.Diagnostics
                        .Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error)
                        .Select(d => $"{d.Id} [{d.Location.GetLineSpan().StartLinePosition}]: {d.GetMessage()}");

                    string errorList = string.Join(Environment.NewLine, failures);
                    throw new RoslynCompilationException($"Compilation failed:\n{errorList}");
                }

                ms.Seek(0, SeekOrigin.Begin);
                byte[] peBytes = ms.ToArray();

#if NET8_0_OR_GREATER
                var alc = new CollectibleAssemblyLoadContext(assemblyName);
                using var peStream = new MemoryStream(peBytes);
                Assembly loadedAssembly = alc.LoadFromStream(peStream);
#else
                Assembly loadedAssembly = Assembly.Load(peBytes);
#endif

                Type? scriptType = loadedAssembly.GetType("RevitMCP.Dynamic.DynamicScript") ??
                                   loadedAssembly.GetTypes().FirstOrDefault(t => t.GetMethod("Run") != null);

                if (scriptType == null)
                {
                    throw new InvalidOperationException("Could not find class 'DynamicScript' or a class with 'Run' method.");
                }

                MethodInfo? runMethod = scriptType.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                if (runMethod == null)
                {
                    throw new InvalidOperationException("Could not find public static 'Run' method.");
                }

#if !NET8_0_OR_GREATER
                _cachedMethods.TryAdd(hash, runMethod);
#endif

                return runMethod;
            }
        }

        private static string ComputeSha256(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }

#if NET8_0_OR_GREATER
    public class CollectibleAssemblyLoadContext : AssemblyLoadContext
    {
        public CollectibleAssemblyLoadContext(string name) : base(name, isCollectible: true)
        {
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            return null; // Resolve from default context
        }
    }
#endif

    public class RoslynCompilationException : Exception
    {
        public RoslynCompilationException(string message) : base(message)
        {
        }
    }
}
