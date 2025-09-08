using System;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Emulator;

namespace MultiWorkerTestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Testing multiple workers with different orchestrations...");

            try
            {
                // Test 1: Separate services (different task hubs)
                await TestSeparateServices();
                
                // Test 2: Shared service (same task hub)
                await TestSharedService();
                
                // Test 3: Unregistered orchestration handling
                await TestUnregisteredOrchestration();
                
                Console.WriteLine("All tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        static async Task TestSeparateServices()
        {
            Console.WriteLine("\n=== Test 1: Separate Services ===");
            
            var service1 = new LocalOrchestrationService();
            var service2 = new LocalOrchestrationService();

            var worker1 = new TaskHubWorker(service1);
            var worker2 = new TaskHubWorker(service2);

            worker1.AddTaskOrchestrations(typeof(Worker1Orchestration))
                   .AddTaskActivities(typeof(Worker1Activity));

            worker2.AddTaskOrchestrations(typeof(Worker2Orchestration))
                   .AddTaskActivities(typeof(Worker2Activity));

            var client1 = new TaskHubClient(service1);
            var client2 = new TaskHubClient(service2);

            try
            {
                await worker1.StartAsync();
                await worker2.StartAsync();

                var instance1 = await client1.CreateOrchestrationInstanceAsync(typeof(Worker1Orchestration), "Worker1Input");
                var result1 = await client1.WaitForOrchestrationAsync(instance1, TimeSpan.FromSeconds(30));
                
                var instance2 = await client2.CreateOrchestrationInstanceAsync(typeof(Worker2Orchestration), "Worker2Input");
                var result2 = await client2.WaitForOrchestrationAsync(instance2, TimeSpan.FromSeconds(30));

                Console.WriteLine($"Worker1 result: {result1.OrchestrationStatus} - {result1.Output}");
                Console.WriteLine($"Worker2 result: {result2.OrchestrationStatus} - {result2.Output}");
                
                if (result1.OrchestrationStatus == OrchestrationStatus.Completed && 
                    result2.OrchestrationStatus == OrchestrationStatus.Completed)
                {
                    Console.WriteLine("✓ Separate services test PASSED");
                }
                else
                {
                    Console.WriteLine("✗ Separate services test FAILED");
                }
            }
            finally
            {
                await worker1.StopAsync();
                await worker2.StopAsync();
            }
        }

        static async Task TestSharedService()
        {
            Console.WriteLine("\n=== Test 2: Shared Service ===");
            
            var service = new LocalOrchestrationService();

            var worker1 = new TaskHubWorker(service);
            var worker2 = new TaskHubWorker(service);

            worker1.AddTaskOrchestrations(typeof(Worker1Orchestration))
                   .AddTaskActivities(typeof(Worker1Activity));

            worker2.AddTaskOrchestrations(typeof(Worker2Orchestration))
                   .AddTaskActivities(typeof(Worker2Activity));

            var client = new TaskHubClient(service);

            try
            {
                await worker1.StartAsync();
                await worker2.StartAsync();

                var instance1 = await client.CreateOrchestrationInstanceAsync(typeof(Worker1Orchestration), "Worker1Input");
                var instance2 = await client.CreateOrchestrationInstanceAsync(typeof(Worker2Orchestration), "Worker2Input");

                var result1 = await client.WaitForOrchestrationAsync(instance1, TimeSpan.FromSeconds(30));
                var result2 = await client.WaitForOrchestrationAsync(instance2, TimeSpan.FromSeconds(30));

                Console.WriteLine($"Worker1 result: {result1.OrchestrationStatus} - {result1.Output}");
                Console.WriteLine($"Worker2 result: {result2.OrchestrationStatus} - {result2.Output}");
                
                if (result1.OrchestrationStatus == OrchestrationStatus.Completed && 
                    result2.OrchestrationStatus == OrchestrationStatus.Completed)
                {
                    Console.WriteLine("✓ Shared service test PASSED");
                }
                else
                {
                    Console.WriteLine("✗ Shared service test FAILED");
                }
            }
            finally
            {
                await worker1.StopAsync();
                await worker2.StopAsync();
            }
        }

        static async Task TestUnregisteredOrchestration()
        {
            Console.WriteLine("\n=== Test 3: Unregistered Orchestration ===");
            
            var service = new LocalOrchestrationService();
            var worker = new TaskHubWorker(service);

            // Register only Worker1 types
            worker.AddTaskOrchestrations(typeof(Worker1Orchestration))
                  .AddTaskActivities(typeof(Worker1Activity));

            var client = new TaskHubClient(service);

            try
            {
                await worker.StartAsync();

                // Try to start Worker2Orchestration (not registered on this worker)
                var instance = await client.CreateOrchestrationInstanceAsync(typeof(Worker2Orchestration), "input");

                // This should timeout or fail because the orchestration is not registered
                var result = await client.WaitForOrchestrationAsync(instance, TimeSpan.FromSeconds(10));

                Console.WriteLine($"Unregistered orchestration result: {result.OrchestrationStatus} - {result.Output}");
                
                if (result.OrchestrationStatus != OrchestrationStatus.Completed)
                {
                    Console.WriteLine("✓ Unregistered orchestration test PASSED (orchestration did not complete)");
                }
                else
                {
                    Console.WriteLine("✗ Unregistered orchestration test FAILED (orchestration completed unexpectedly)");
                }
            }
            finally
            {
                await worker.StopAsync();
            }
        }
    }

    // Test orchestrations and activities
    public class Worker1Orchestration : TaskOrchestration<string, string>
    {
        public override async Task<string> RunTask(OrchestrationContext context, string input)
        {
            var result = await context.ScheduleTask<string>(typeof(Worker1Activity), input);
            return $"Worker1: {result}";
        }
    }

    public class Worker1Activity : TaskActivity<string, string>
    {
        protected override string Execute(TaskContext context, string input)
        {
            return $"{input} processed";
        }
    }

    public class Worker2Orchestration : TaskOrchestration<string, string>
    {
        public override async Task<string> RunTask(OrchestrationContext context, string input)
        {
            var result = await context.ScheduleTask<string>(typeof(Worker2Activity), input);
            return $"Worker2: {result}";
        }
    }

    public class Worker2Activity : TaskActivity<string, string>
    {
        protected override string Execute(TaskContext context, string input)
        {
            return $"{input} processed";
        }
    }
}