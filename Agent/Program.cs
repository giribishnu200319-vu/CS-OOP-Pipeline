using System;
using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;

class Agent
{
    static async Task Main(string[] args)
    {
        string name = args.Length > 0 ? args[0] : "Unknown";
        Console.WriteLine($"Agent {name} started. Waiting for number...");

        using var pipe = new NamedPipeClientStream(".", "guess-pipe", PipeDirection.InOut);
        await pipe.ConnectAsync();

        using var reader = new StreamReader(pipe, leaveOpen: true);
        using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

        string line = await reader.ReadLineAsync();
        int target = int.Parse(line);
        Console.WriteLine($"Agent {name} received target: {target}");

        var rng = new Random();
        int guess;
        int attempts = 0;

        do
        {
            guess = rng.Next(1, 1001);
            attempts++;
        }
        while (guess != target);

        Console.WriteLine($"Agent {name} guessed {target} in {attempts} attempts.");
        await writer.WriteLineAsync($"Agent {name} guessed the number {target}.");
    }
}
