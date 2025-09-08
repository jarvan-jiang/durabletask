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

namespace DurableTask.Core
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Information about a registered orchestration, activity, or entity
    /// </summary>
    public class WorkerRegistrationInfo
    {
        /// <summary>
        /// Initializes a new instance of the WorkerRegistrationInfo class
        /// </summary>
        /// <param name="name">Name of the registered type</param>
        /// <param name="version">Version of the registered type</param>
        public WorkerRegistrationInfo(string name, string version)
        {
            Name = name;
            Version = version;
        }

        /// <summary>
        /// Name of the registered type
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Version of the registered type
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Returns a string representation of the registration info
        /// </summary>
        public override string ToString()
        {
            return $"{Name} (v{Version})";
        }
    }

    /// <summary>
    /// Summary of all capabilities of a TaskHubWorker
    /// </summary>
    public class WorkerCapabilitySummary
    {
        /// <summary>
        /// Initializes a new instance of the WorkerCapabilitySummary class
        /// </summary>
        /// <param name="orchestrations">List of registered orchestrations</param>
        /// <param name="activities">List of registered activities</param>
        /// <param name="entities">List of registered entities</param>
        public WorkerCapabilitySummary(
            IReadOnlyList<WorkerRegistrationInfo> orchestrations,
            IReadOnlyList<WorkerRegistrationInfo> activities,
            IReadOnlyList<WorkerRegistrationInfo> entities)
        {
            Orchestrations = orchestrations;
            Activities = activities;
            Entities = entities;
        }

        /// <summary>
        /// List of registered orchestrations
        /// </summary>
        public IReadOnlyList<WorkerRegistrationInfo> Orchestrations { get; }

        /// <summary>
        /// List of registered activities
        /// </summary>
        public IReadOnlyList<WorkerRegistrationInfo> Activities { get; }

        /// <summary>
        /// List of registered entities
        /// </summary>
        public IReadOnlyList<WorkerRegistrationInfo> Entities { get; }

        /// <summary>
        /// Returns a summary string of worker capabilities
        /// </summary>
        public override string ToString()
        {
            var parts = new List<string>();
            
            if (Orchestrations.Any())
                parts.Add($"{Orchestrations.Count} orchestrations");
            
            if (Activities.Any())
                parts.Add($"{Activities.Count} activities");
            
            if (Entities.Any())
                parts.Add($"{Entities.Count} entities");

            return parts.Any() ? $"Worker supports: {string.Join(", ", parts)}" : "Worker has no registrations";
        }

        /// <summary>
        /// Returns detailed information about worker capabilities
        /// </summary>
        public string GetDetailedInfo()
        {
            var details = new List<string>();

            if (Orchestrations.Any())
            {
                details.Add("Orchestrations:");
                foreach (var orchestration in Orchestrations)
                {
                    details.Add($"  - {orchestration}");
                }
            }

            if (Activities.Any())
            {
                details.Add("Activities:");
                foreach (var activity in Activities)
                {
                    details.Add($"  - {activity}");
                }
            }

            if (Entities.Any())
            {
                details.Add("Entities:");
                foreach (var entity in Entities)
                {
                    details.Add($"  - {entity}");
                }
            }

            return details.Any() ? string.Join("\n", details) : "Worker has no registrations";
        }
    }
}