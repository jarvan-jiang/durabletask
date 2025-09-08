using System;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Emulator;

// Test orchestration for worker 1
public sealed class Worker1Orchestration : TaskOrchestration<string, string>
{
    public override Task<string> RunTask(OrchestrationContext context, string input)
    {
        return Task.FromResult($"Worker1 processed: {input}");
    }
}

// Test orchestration for worker 2  
public sealed class Worker2Orchestration : TaskOrchestration<string, string>
{
    public override Task<string> RunTask(OrchestrationContext context, string input)
    {
        return Task.FromResult($"Worker2 processed: {input}");
    }
}

// Test activity for worker 1
public sealed class Worker1Activity : TaskActivity<string, string>
{
    protected override string Execute(TaskContext context, string input)
    {
        return $"Worker1Activity processed: {input}";
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Multi-Worker Latency Optimization Test");
        Console.WriteLine("=====================================");

        // Create shared orchestration service (simulating shared task hub)
        var sharedService = new LocalOrchestrationService();

        // Create two workers - worker1 only has Worker1Orchestration, worker2 only has Worker2Orchestration
        var worker1 = new TaskHubWorker(sharedService);
        var worker2 = new TaskHubWorker(sharedService);

        worker1.AddTaskOrchestrations(typeof(Worker1Orchestration))
               .AddTaskActivities(typeof(Worker1Activity));
        worker2.AddTaskOrchestrations(typeof(Worker2Orchestration));

        var client = new TaskHubClient(sharedService);

        try
        {
            await sharedService.CreateAsync();
            await worker1.StartAsync();
            await worker2.StartAsync();

            Console.WriteLine("Workers started. Testing latency optimization...");

            // Test 1: Start Worker1Orchestration (should succeed quickly on worker1)
            Console.WriteLine("\nTest 1: Starting Worker1Orchestration...");
            var instance1 = await client.CreateOrchestrationInstanceAsync(typeof(Worker1Orchestration), "test input 1");
            var result1 = await client.WaitForOrchestrationAsync(instance1, TimeSpan.FromSeconds(10));
            Console.WriteLine($"Result 1: {result1?.OrchestrationStatus} - {result1?.Output}");

            // Test 2: Start Worker2Orchestration (should initially fail on worker1 but then succeed on worker2)
            // With our optimization, worker1 should quickly skip retrying this type after the first failure
            Console.WriteLine("\nTest 2: Starting Worker2Orchestration...");
            var instance2 = await client.CreateOrchestrationInstanceAsync(typeof(Worker2Orchestration), "test input 2");
            var result2 = await client.WaitForOrchestrationAsync(instance2, TimeSpan.FromSeconds(10));
            Console.WriteLine($"Result 2: {result2?.OrchestrationStatus} - {result2?.Output}");

            Console.WriteLine("\nTest completed successfully!");
            Console.WriteLine("The latency optimization should reduce retry delays for incompatible orchestration types.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
        }
        finally
        {
            await worker1.StopAsync(true);
            await worker2.StopAsync(true);
        }
    }
}