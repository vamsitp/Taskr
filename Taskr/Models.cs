﻿namespace Taskr
{
    using Newtonsoft.Json;

    public class AccountSettings
    {
        public string Query { get; set; }
        public string Slicers { get; set; }
        public Account[] Accounts { get; set; }
    }

    public class Account
    {
        public string Name { get; set; }
        public string Org { get; set; }
        public string Project { get; set; }
        public string Token { get; set; }
        public string Query { get; set; }
        public string Slicers { get; set; }
        public bool Enabled { get; set; }
    }

    public class WorkItems
    {
        [JsonProperty(PropertyName = "count")]
        public int Count { get; set; }

        [JsonProperty(PropertyName = "value")]
        public WorkItem[] Items { get; set; }
    }

    public class WorkItem
    {
        [JsonProperty(PropertyName = "id")]
        public int Id { get; set; }

        [JsonProperty(PropertyName = "fields")]
        public Fields Fields { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string Url { get; set; }
    }

    public class Fields
    {
        [JsonProperty(PropertyName = "System.Title")]
        public string Title { get; set; }

        [JsonProperty(PropertyName = "System.Description")]
        public string Description { get; set; }

        [JsonProperty(PropertyName = "System.WorkItemType")]
        public string WorkItemType { get; set; }

        [JsonProperty(PropertyName = "System.State")]
        public string State { get; set; }

        [JsonProperty(PropertyName = "System.Tags")]
        public string Tags { get; set; }

        [JsonProperty(PropertyName = "System.AreaPath")]
        public string AreaPath { get; set; }

        [JsonProperty(PropertyName = "System.IterationPath")]
        public string IterationPath { get; set; }

        [JsonProperty(PropertyName = "Microsoft.VSTS.Common.Priority")]
        public short Priority { get; set; }

        [JsonProperty(PropertyName = "System.AssignedTo")]
        public AssignedTo AssignedTo { get; set; }

        [JsonProperty(PropertyName = "Microsoft.VSTS.Scheduling.OriginalEstimate")]
        public float OriginalEstimate { get; set; }

        [JsonProperty(PropertyName = "Microsoft.VSTS.Scheduling.CompletedWork")]
        public float CompletedWork { get; set; }

        [JsonProperty(PropertyName = "Microsoft.VSTS.Scheduling.RemainingWork")]
        public float RemainingWork { get; set; }
    }

    public class AssignedTo
    {
        [JsonProperty(PropertyName = "displayName")]
        public string DisplayName { get; set; }

        [JsonProperty(PropertyName = "uniqueName")]
        public string UniqueName { get; set; }
    }

    public class AzDOException
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "innerException")]
        public object InnerException { get; set; }

        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

        [JsonProperty(PropertyName = "typeName")]
        public string TypeName { get; set; }

        [JsonProperty(PropertyName = "typeKey")]
        public string TypeKey { get; set; }

        [JsonProperty(PropertyName = "errorCode")]
        public int ErrorCode { get; set; }

        [JsonProperty(PropertyName = "eventId")]
        public int EventId { get; set; }
    }
}