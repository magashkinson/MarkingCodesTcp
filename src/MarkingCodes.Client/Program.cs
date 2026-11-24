using MarkingCodes.Client;
using MarkingCodes.Protocol;

// Usage: MarkingCodes.Client [host] [port] [outputFile]
var host = args.Length > 0 ? args[0] : "127.0.0.1";
var port = args.Length > 1 ? int.Parse(args[1]) : 5000;
var output = args.Length > 2 ? args[2] : "codes.txt";

using var shutdown = new Shutdown();
var client = new MarkingCodeClient(host, port, output, ClientOptions.Default);
await client.RunAsync(shutdown.Token);
