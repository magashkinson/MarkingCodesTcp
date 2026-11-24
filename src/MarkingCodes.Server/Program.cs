using System.Net;
using MarkingCodes.Protocol;
using MarkingCodes.Server;

// Usage: MarkingCodes.Server [port]
var port = args.Length > 0 ? int.Parse(args[0]) : 5000;

using var shutdown = new Shutdown();
var generator = new Gs1CodeGenerator();
using var server = new MarkingCodeServer(new IPEndPoint(IPAddress.Any, port), generator.Generate, TimeSpan.FromMilliseconds(500));
server.Start();
await server.RunAsync(shutdown.Token);
