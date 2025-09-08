# Multi-Worker Support in DurableTask Framework

This document explains how the DurableTask framework supports multiple workers with different registered orchestrations and activities.

## Summary

**The DurableTask framework already provides excellent support for multiple workers with different orchestrations and activities.** No core changes were needed to enable this functionality.

## Supported Scenarios

### 1. Multiple Workers with Separate Services
Workers can use completely separate orchestration services (different task hubs):

```csharp
var service1 = new LocalOrchestrationService();
var service2 = new LocalOrchestrationService();

var worker1 = new TaskHubWorker(service1);
var worker2 = new TaskHubWorker(service2);

worker1.AddTaskOrchestrations(typeof(OrderProcessingOrchestration))
       .AddTaskActivities(typeof(ProcessPaymentActivity));

worker2.AddTaskOrchestrations(typeof(DataAnalysisOrchestration))
       .AddTaskActivities(typeof(AnalyzeDataActivity));
```

### 2. Multiple Workers Sharing the Same Service
Workers can share the same orchestration service for automatic load balancing:

```csharp
var service = new LocalOrchestrationService();

var worker1 = new TaskHubWorker(service);
var worker2 = new TaskHubWorker(service);

worker1.AddTaskOrchestrations(typeof(OrderProcessingOrchestration));
worker2.AddTaskOrchestrations(typeof(DataAnalysisOrchestration));
```

## How It Works

### Work Distribution
1. **Shared Services**: When multiple workers share an orchestration service, they all listen for work from the same queues
2. **Load Balancing**: Any available worker can pick up any work item
3. **Specialization**: If a worker picks up work for an unregistered orchestration/activity, it fails gracefully
4. **Retry Mechanism**: Failed work items are retried, potentially by a different worker that has the required registration

### Error Handling
When a worker encounters an unregistered orchestration or activity:
1. A `TypeMissingException` is thrown
2. The worker backs off to avoid immediate retries
3. The work item is made available for retry by other workers
4. Eventually, a worker with the correct registration will handle the work

## Enhanced Diagnostics (New Feature)

### Worker Capability Inspection
You can now inspect what each worker is capable of handling:

```csharp
var worker = new TaskHubWorker(service);
worker.AddTaskOrchestrations(typeof(MyOrchestration))
      .AddTaskActivities(typeof(MyActivity));

// Get summary of worker capabilities
var capabilities = worker.GetWorkerCapabilities();
Console.WriteLine(capabilities.ToString());
// Output: "Worker supports: 1 orchestrations, 1 activities"

// Get detailed information
Console.WriteLine(capabilities.GetDetailedInfo());
// Output:
// Orchestrations:
//   - MyOrchestration (v)
// Activities:
//   - MyActivity (v)

// Get specific registration lists
var orchestrations = worker.GetRegisteredOrchestrations();
var activities = worker.GetRegisteredActivities();
var entities = worker.GetRegisteredEntities();
```

## Best Practices

### 1. Use Descriptive Names
Give your orchestrations and activities clear, descriptive names:

```csharp
public class OrderProcessingOrchestration : TaskOrchestration<string, string> { }
public class ProcessPaymentActivity : TaskActivity<string, string> { }
```

### 2. Monitor Worker Capabilities
Use the new capability inspection methods to monitor and debug your workers:

```csharp
foreach (var worker in workers)
{
    var capabilities = worker.GetWorkerCapabilities();
    logger.LogInformation("Worker capabilities: {Capabilities}", capabilities);
}
```

### 3. Plan for Graceful Degradation
Design your system so that if certain workers are unavailable, the system can still function:
- Use timeouts appropriately
- Monitor for `TypeMissingException` patterns
- Consider implementing circuit breakers for critical workflows

### 4. Consider Worker Specialization
You can design workers to specialize in specific types of work:

```csharp
// Payment processing worker
var paymentWorker = new TaskHubWorker(service);
paymentWorker.AddTaskOrchestrations(typeof(PaymentOrchestration))
            .AddTaskActivities(typeof(ProcessPaymentActivity), typeof(RefundActivity));

// Data processing worker  
var dataWorker = new TaskHubWorker(service);
dataWorker.AddTaskOrchestrations(typeof(DataAnalysisOrchestration))
          .AddTaskActivities(typeof(ExtractDataActivity), typeof(TransformDataActivity));
```

## Architecture Benefits

### Automatic Load Balancing
Multiple workers automatically distribute load without additional configuration.

### Fault Tolerance
If one worker fails, others can continue processing work.

### Horizontal Scaling
Add more workers to increase processing capacity.

### Deployment Flexibility
Deploy different worker types to different machines or containers based on resource requirements.

### Latency Optimization (New in v3.3.0)
The framework now includes intelligent work item filtering to reduce latency when workers encounter incompatible orchestration/activity types:

- **Adaptive Filtering**: When a worker encounters a `TypeMissingException` for a specific orchestration/activity type, it marks that type as incompatible
- **Smart Retry Avoidance**: Future work items for incompatible types are immediately released back to the queue instead of being processed
- **Automatic Expiry**: Incompatible type markers automatically expire after 5 minutes to handle deployment scenarios
- **Reduced Latency**: Eliminates the 10-second backoff delay that previously occurred on each `TypeMissingException`

This optimization is particularly beneficial in scenarios where:
- Workers are specialized for different orchestration types
- Multiple workers share the same task hub
- High throughput is required with mixed workload types

```csharp
// Example: With the optimization, if Worker1 doesn't have CustomerOnboardingOrchestration,
// it will quickly skip processing such work items after the first encounter,
// allowing Worker2 to handle them without unnecessary delays

var worker1 = new TaskHubWorker(sharedService);
worker1.AddTaskOrchestrations(typeof(PaymentOrchestration));

var worker2 = new TaskHubWorker(sharedService);  
worker2.AddTaskOrchestrations(typeof(CustomerOnboardingOrchestration));

// CustomerOnboardingOrchestration instances will be efficiently routed to Worker2
// after Worker1 learns it cannot handle this type
```

## Monitoring and Troubleshooting

### TypeMissingException Patterns
Monitor for `TypeMissingException` patterns in your logs:
- Frequent exceptions may indicate missing worker registrations
- Check that all required orchestrations/activities are registered on at least one worker
- **Note**: With the new latency optimization, you should see fewer repeated `TypeMissingException` instances for the same types, as workers learn to avoid incompatible work items

### Latency Optimization Monitoring
The new work item filtering can be monitored through trace events:
- `TaskOrchestrationDispatcher-IncompatibleTypeSkipped`: Orchestration work item skipped due to incompatibility
- `TaskActivityDispatcher-IncompatibleTypeSkipped`: Activity work item skipped due to incompatibility

These events indicate the optimization is working correctly and reducing unnecessary processing overhead.

### Worker Health Checks
Implement health checks that verify worker capabilities:

```csharp
public bool IsWorkerHealthy(TaskHubWorker worker)
{
    var capabilities = worker.GetWorkerCapabilities();
    return capabilities.Orchestrations.Any() || capabilities.Activities.Any();
}
```

### Logging Worker Startup
Log worker capabilities at startup for better visibility:

```csharp
await worker.StartAsync();
var capabilities = worker.GetWorkerCapabilities();
logger.LogInformation("Worker started with capabilities: {Capabilities}", capabilities.GetDetailedInfo());
```

## Conclusion

The DurableTask framework provides robust, production-ready support for multiple workers with different capabilities. The enhanced diagnostic capabilities make it easier to monitor and debug multi-worker deployments.