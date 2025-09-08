//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------

namespace DurableTask.Core.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using DurableTask.Core;
    using DurableTask.Emulator;

    [TestClass]
    public class MultiWorkerTests
    {
        [TestMethod]
        public async Task TestMultipleWorkersWithDifferentOrchestrations()
        {
            // Create two separate orchestration services (simulating different task hubs)
            var service1 = new LocalOrchestrationService();
            var service2 = new LocalOrchestrationService();

            // Create two workers with different orchestrations registered
            var worker1 = new TaskHubWorker(service1);
            var worker2 = new TaskHubWorker(service2);

            // Worker1 registers Worker1Orchestration and Worker1Activity
            worker1.AddTaskOrchestrations(typeof(Worker1Orchestration))
                   .AddTaskActivities(typeof(Worker1Activity));

            // Worker2 registers Worker2Orchestration and Worker2Activity  
            worker2.AddTaskOrchestrations(typeof(Worker2Orchestration))
                   .AddTaskActivities(typeof(Worker2Activity));

            // Create clients
            var client1 = new TaskHubClient(service1);
            var client2 = new TaskHubClient(service2);

            try
            {
                await service1.CreateAsync();
                await service2.CreateAsync();

                await worker1.StartAsync();
                await worker2.StartAsync();

                // Test Worker1 with its orchestration
                var instance1 = await client1.CreateOrchestrationInstanceAsync(typeof(Worker1Orchestration), "Worker1Input");
                var result1 = await client1.WaitForOrchestrationAsync(instance1, TimeSpan.FromSeconds(30));
                Assert.AreEqual(OrchestrationStatus.Completed, result1.OrchestrationStatus);
                Assert.AreEqual("Worker1: Worker1Input processed", result1.Output);

                // Test Worker2 with its orchestration  
                var instance2 = await client2.CreateOrchestrationInstanceAsync(typeof(Worker2Orchestration), "Worker2Input");
                var result2 = await client2.WaitForOrchestrationAsync(instance2, TimeSpan.FromSeconds(30));
                Assert.AreEqual(OrchestrationStatus.Completed, result2.OrchestrationStatus);
                Assert.AreEqual("Worker2: Worker2Input processed", result2.Output);
            }
            finally
            {
                await worker1.StopAsync();
                await worker2.StopAsync();
                await service1.DeleteAsync();
                await service2.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestSingleServiceWithMultipleWorkersSharing()
        {
            // Create one orchestration service shared by multiple workers
            var service = new LocalOrchestrationService();

            // Create two workers sharing the same service
            var worker1 = new TaskHubWorker(service);
            var worker2 = new TaskHubWorker(service);

            // Worker1 registers Worker1Orchestration and Worker1Activity
            worker1.AddTaskOrchestrations(typeof(Worker1Orchestration))
                   .AddTaskActivities(typeof(Worker1Activity));

            // Worker2 registers Worker2Orchestration and Worker2Activity  
            worker2.AddTaskOrchestrations(typeof(Worker2Orchestration))
                   .AddTaskActivities(typeof(Worker2Activity));

            var client = new TaskHubClient(service);

            try
            {
                await service.CreateAsync();

                await worker1.StartAsync();
                await worker2.StartAsync();

                // Test both orchestrations using the same client/service
                var instance1 = await client.CreateOrchestrationInstanceAsync(typeof(Worker1Orchestration), "Worker1Input");
                var instance2 = await client.CreateOrchestrationInstanceAsync(typeof(Worker2Orchestration), "Worker2Input");

                var result1 = await client.WaitForOrchestrationAsync(instance1, TimeSpan.FromSeconds(30));
                var result2 = await client.WaitForOrchestrationAsync(instance2, TimeSpan.FromSeconds(30));

                Assert.AreEqual(OrchestrationStatus.Completed, result1.OrchestrationStatus);
                Assert.AreEqual("Worker1: Worker1Input processed", result1.Output);

                Assert.AreEqual(OrchestrationStatus.Completed, result2.OrchestrationStatus);
                Assert.AreEqual("Worker2: Worker2Input processed", result2.Output);
            }
            finally
            {
                await worker1.StopAsync();
                await worker2.StopAsync();
                await service.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestWorkerCannotHandleUnregisteredOrchestration()
        {
            var service = new LocalOrchestrationService();
            var worker = new TaskHubWorker(service);

            // Register only Worker1 types
            worker.AddTaskOrchestrations(typeof(Worker1Orchestration))
                  .AddTaskActivities(typeof(Worker1Activity));

            var client = new TaskHubClient(service);

            try
            {
                await service.CreateAsync();
                await worker.StartAsync();

                // Try to start Worker2Orchestration (not registered on this worker)
                var instance = await client.CreateOrchestrationInstanceAsync(typeof(Worker2Orchestration), "input");

                // This should timeout or fail because the orchestration is not registered
                var result = await client.WaitForOrchestrationAsync(instance, TimeSpan.FromSeconds(10));

                // The orchestration should not complete successfully
                Assert.AreNotEqual(OrchestrationStatus.Completed, result.OrchestrationStatus);
            }
            finally
            {
                await worker.StopAsync();
                await service.DeleteAsync();
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