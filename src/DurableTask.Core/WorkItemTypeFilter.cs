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
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Tracks and filters work item types that are incompatible with this worker to reduce retry latency
    /// </summary>
    internal class WorkItemTypeFilter
    {
        // Time to cache incompatible types before allowing retry (in case of deployments)
        private static readonly TimeSpan IncompatibleTypeCacheExpiry = TimeSpan.FromMinutes(5);
        
        // Track incompatible orchestration/activity types with timestamp
        private readonly ConcurrentDictionary<string, DateTime> incompatibleTypes = new ConcurrentDictionary<string, DateTime>();

        /// <summary>
        /// Marks a type as incompatible with this worker
        /// </summary>
        /// <param name="name">The orchestration or activity name</param>
        /// <param name="version">The orchestration or activity version</param>
        public void MarkTypeAsIncompatible(string name, string version)
        {
            if (string.IsNullOrEmpty(name))
                return;
                
            string key = CreateTypeKey(name, version);
            incompatibleTypes[key] = DateTime.UtcNow;
        }

        /// <summary>
        /// Checks if a type is marked as incompatible with this worker
        /// </summary>
        /// <param name="name">The orchestration or activity name</param>
        /// <param name="version">The orchestration or activity version</param>
        /// <returns>True if the type should be avoided by this worker</returns>
        public bool IsTypeIncompatible(string name, string version)
        {
            if (string.IsNullOrEmpty(name))
                return false;
                
            string key = CreateTypeKey(name, version);
            
            if (incompatibleTypes.TryGetValue(key, out DateTime markedTime))
            {
                // Check if the entry has expired
                if (DateTime.UtcNow - markedTime > IncompatibleTypeCacheExpiry)
                {
                    // Remove expired entry and allow retry
                    incompatibleTypes.TryRemove(key, out _);
                    return false;
                }
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Clears all incompatible type entries (useful for testing or forced reset)
        /// </summary>
        public void ClearIncompatibleTypes()
        {
            incompatibleTypes.Clear();
        }

        /// <summary>
        /// Gets the number of currently tracked incompatible types
        /// </summary>
        public int IncompatibleTypeCount => incompatibleTypes.Count;

        private static string CreateTypeKey(string name, string version)
        {
            return $"{name}:{version ?? string.Empty}";
        }
    }
}