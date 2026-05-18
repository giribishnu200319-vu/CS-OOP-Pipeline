using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

class Master
{
    static async Task Main(string[] args)
    {
        int agentCount = args.Length > 0 ? int.Parse(args[0]) : 2;

        var rng = new Random();
        int target = rng.Next(1, 1001);
        Console.WriteLine($"Master started. Target number: {target}");
        Console.WriteLine($"Starting {agentCount} agent(s)...\n");

        string agentProjectPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../Agent/Agent.csproj")
        );

        var timers = new Stopwatch[agentCount];
        var processes = new Process[agentCount];

        for (int i = 0; i < agentCount; i++)
        {
            string agentName = (i + 1).ToString();
            timers[i] = Stopwatch.StartNew();

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{agentProjectPath}\" -- {agentName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };

            processes[i] = Process.Start(psi)!;

            if (OperatingSystem.IsWindows())
            {
                processes[i].ProcessorAffinity = (IntPtr)(1 << i);
                Console.WriteLine($"Agent {i + 1} pinned to CPU core {i}");
            }
            else
            {
                Console.WriteLine($"Agent {i + 1} started (core pinning skipped on macOS)");
            }
        }

        Console.WriteLine("Waiting for agents to connect...\n");

        var pipeTasks = new Task<string>[agentCount];
        for (int i = 0; i < agentCount; i++)
        {
            int index = i;
            pipeTasks[index] = HandleAgentAsync(target, index + 1, timers[index]);
        }

        string winner = await Task.WhenAny(pipeTasks)
                                  .ContinueWith(t => t.Result.Result);

        Console.WriteLine($"\n*** Winner: {winner} ***");

        Console.WriteLine("\nAll agent times:");
        for (int i = 0; i < agentCount; i++)
        {
            try
            {
                string result = await pipeTasks[i];
                Console.WriteLine($"  Agent {i + 1}: {timers[i].ElapsedMilliseconds} ms — {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Agent {i + 1}: error — {ex.Message}");
            }
        }

        foreach (var p in processes)
        {
            try { p.WaitForExit(3000); } catch { }
        }
    }

    static async Task<string> HandleAgentAsync(int target, int agentIndex, Stopwatch timer)
    {
        using var pipe = new NamedPipeServerStream(
            "guess-pipe",
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous
        );

        await pipe.WaitForConnectionAsync();

        using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, leaveOpen: true);

        await writer.WriteLineAsync(target.ToString());

        string message = await reader.ReadLineAsync() ?? "No response";
        timer.Stop();

        return message;
    }
}
