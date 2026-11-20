using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Cli.Mcp;
using RevitMCP.Shared;

namespace RevitMCP.Cli
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var utf8NoBom = new UTF8Encoding(false);
            Console.InputEncoding = utf8NoBom;
            Console.OutputEncoding = utf8NoBom;

            string? mcpName = null;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                if (arg == "--version" || arg == "-v")
                {
                    Console.WriteLine($"Revit MCP Server v{Mcp.VersionInfo.Current}");
                    return 0;
                }

                if (arg == "--help" || arg == "-h")
                {
                    Console.WriteLine("Revit MCP CLI Server");
                    Console.WriteLine("Usage: RevitMCP.Cli [options]");
                    Console.WriteLine();
                    Console.WriteLine("Options:");
                    Console.WriteLine("  --mcp-name, --name <name>  Override MCP instance name (default: RevitMCP)");
                    Console.WriteLine("  --version, -v             Show version information");
                    Console.WriteLine("  --help, -h                Show help");
                    Console.WriteLine();
                    Console.WriteLine("By default, starts the MCP stdio server to connect AI clients to running Revit instances.");
                    return 0;
                }

                if ((arg == "--mcp-name" || arg == "--name") && i + 1 < args.Length)
                {
                    mcpName = args[++i];
                }
            }

            McpStorageContext.Initialize(mcpName);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var server = new McpServer();
            try
            {
                await server.RunAsync(cts.Token).ConfigureAwait(false);
                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Fatal MCP server error: {ex}").ConfigureAwait(false);
                return 1;
            }
        }
    }
}
