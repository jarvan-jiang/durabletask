using System;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Emulator;

namespace ProactiveFilteringTest
{
    // Test orchestration 1 - will be registered only in Worker1
    class OrderProcessingOrchestration : TaskOrchestration<string, string>
    {
        public override async Task<string> RunTask(OrchestrationContext context, string input)
        {
            return await context.ScheduleTask<string>(typeof(ValidateOrderActivity), input);
        }
    }

    // Test orchestration 2 - will be registered only in Worker2 
    class PaymentProcessingOrchestration : TaskOrchestration<string, string>
    {
        public override async Task<string> RunTask(OrchestrationContext context, string input)
        {
            return await context.ScheduleTask<string>(typeof(ProcessPaymentActivity), input);
        }
    }

    // Test activity 1 - will be registered only in Worker1
    class ValidateOrderActivity : TaskActivity<string, string>
    {
        protected override string Execute(TaskContext context, string input)
        {
            return $"Order validated: {input}";
        }
    }

    // Test activity 2 - will be registered only in Worker2
    class ProcessPaymentActivity : TaskActivity<string, string>
    {
        protected override string Execute(TaskContext context, string input)
        {
            return $"Payment processed: {input}";
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Testing Proactive Work Item Filtering...");

            // Create an orchestration service using the LocalOrchestrationService (emulator)
            var orchestrationService = new LocalOrchestrationService();

            // Create Worker1 - only handles OrderProcessing
            var worker1 = new TaskHubWorker(orchestrationService);
            worker1.AddTaskOrchestrations(typeof(OrderProcessingOrchestration))
                   .AddTaskActivities(typeof(ValidateOrderActivity));

            // Create Worker2 - only handles PaymentProcessing  
            var worker2 = new TaskHubWorker(orchestrationService);
            worker2.AddTaskOrchestrations(typeof(PaymentProcessingOrchestration))
                   .AddTaskActivities(typeof(ProcessPaymentActivity));

            // Create a client
            var client = new TaskHubClient(orchestrationService);

            try
            {
                await orchestrationService.CreateAsync(true);
                
                // Start both workers
                await worker1.StartAsync();
                await worker2.StartAsync();
                
                Console.WriteLine("Workers started. Testing proactive filtering...");

                // Test 1: Start an OrderProcessing orchestration - should be handled by Worker1
                var orderInstance = await client.CreateOrchestrationInstanceAsync(
                    typeof(OrderProcessingOrchestration), "Order123");
                Console.WriteLine($"Started OrderProcessing orchestration: {orderInstance.InstanceId}");

                // Test 2: Start a PaymentProcessing orchestration - should be handled by Worker2  
                var paymentInstance = await client.CreateOrchestrationInstanceAsync(
                    typeof(PaymentProcessingOrchestration), "Payment456");
                Console.WriteLine($"Started PaymentProcessing orchestration: {paymentInstance.InstanceId}");

                // Wait for completions
                await Task.Delay(5000); // Give time for orchestrations to complete

                // Check results
                var orderState = await client.GetOrchestrationStateAsync(orderInstance);
                var paymentState = await client.GetOrchestrationStateAsync(paymentInstance);

                Console.WriteLine($"Order orchestration status: {orderState.OrchestrationStatus}");
                Console.WriteLine($"Payment orchestration status: {paymentState.OrchestrationStatus}");

                if (orderState.OrchestrationStatus == OrchestrationStatus.Completed)
                {
                    Console.WriteLine($"Order result: {orderState.Output}");
                }

                if (paymentState.OrchestrationStatus == OrchestrationStatus.Completed) 
                {
                    Console.WriteLine($"Payment result: {paymentState.Output}");
                }

                Console.WriteLine("Proactive filtering test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Clean up
                await worker1.StopAsync(true);
                await worker2.StopAsync(true);
                await orchestrationService.DeleteAsync(true);
            }
        }
    }
}