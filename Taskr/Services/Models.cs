﻿// <auto-generated>

namespace Taskr
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    using Newtonsoft.Json;

    internal enum FlowStep
    {
        Accounts,        
        Details
    }

    public class AccountsData
    {
        public Dictionary<Account, List<WorkItem>> Items { get; set; }
    }

    public enum AccountType
    {
        AzDo,
        Jira
    }

    public class AccountSettings
    {
        public bool CheckUpdates { get; set; }
        public string Query { get; set; }
        public string Slicers { get; set; }
        public Account[] Accounts { get; set; }

        [JsonIgnore]
        public Version Version => Assembly.GetEntryAssembly().GetName().Version;
    }

    public class Account
    {
        public string Name { get; set; }
        public string Org { get; set; }
        public string Project { get; set; }
        public string Token { get; set; }
        public string Query { get; set; }
        public string Slicers { get; set; }
        public AccountType Type { get; set; }
        public bool Enabled { get; set; }

        [JsonIgnore]
        public bool IsPat => this.Token.Length.Equals(52);
    }

    public class WorkItems
    {
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("value")]
        public WorkItem[] Items { get; set; }
    }

    public class WorkItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("fields")]
        public Fields Fields { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class Fields
    {
        private const string Unassigned = "Unassigned";

        [JsonProperty("System.Title")]
        public string Title { get; set; }

        [JsonProperty("System.Description")]
        public string Description { get; set; }

        [JsonProperty("System.WorkItemType")]
        public string WorkItemType { get; set; }

        [JsonProperty("System.State")]
        public string State { get; set; }

        [JsonProperty("System.Reason")]
        public string Reason { get; set; }

        [JsonProperty("System.Tags")]
        public string Tags { get; set; }

        [JsonProperty("System.AreaPath")]
        public string AreaPath { get; set; }

        [JsonProperty("System.IterationPath")]
        public string IterationPath { get; set; }

        [JsonProperty("Microsoft.VSTS.Common.Priority")]
        public short Priority { get; set; }

        [JsonProperty("System.AssignedTo")]
        public AssignedTo AssignedToObj { get; set; }

        [JsonIgnore]
        public string AssignedTo => this.AssignedToObj?.DisplayName ?? (this.AssignedToObj?.UniqueName ?? Unassigned);

        [JsonProperty("Microsoft.VSTS.Scheduling.OriginalEstimate")]
        public float OriginalEstimate { get; set; }

        [JsonProperty("Microsoft.VSTS.Scheduling.CompletedWork")]
        public float CompletedWork { get; set; }

        [JsonProperty("Microsoft.VSTS.Scheduling.RemainingWork")]
        public float RemainingWork { get; set; }
    }

    public class AssignedTo
    {
        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("uniqueName")]
        public string UniqueName { get; set; }
    }

    public class AzDOException
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("innerException")]
        public object InnerException { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("typeName")]
        public string TypeName { get; set; }

        [JsonProperty("typeKey")]
        public string TypeKey { get; set; }

        [JsonProperty("errorCode")]
        public int ErrorCode { get; set; }

        [JsonProperty("eventId")]
        public int EventId { get; set; }
    }

    // Jira

    public class JiraResponse
    {
        public string expand { get; set; }
        public int startAt { get; set; }
        public int maxResults { get; set; }
        public int total { get; set; }
        public JiraIssue[] issues { get; set; }
    }

    public class JiraIssue
    {
        public string expand { get; set; }
        public string id { get; set; }
        public string self { get; set; }
        public string key { get; set; }
        public JiraFields fields { get; set; }
    }

    public class JiraFields
    {
        public DateTime statuscategorychangedate { get; set; }
        public JiraIssuetype issuetype { get; set; }
        public JiraParent parent { get; set; }
        public JiraProject project { get; set; }
        public object resolution { get; set; }
        public object resolutiondate { get; set; }
        public int workratio { get; set; }
        public DateTime created { get; set; }

        [JsonProperty("customfield_10020")]
        public JiraSprint[] iterations { get; set; }
        public JiraPriority priority { get; set; }
        public object[] labels { get; set; }
        public object timeestimate { get; set; }
        public JiraAssignee assignee { get; set; }
        public DateTime updated { get; set; }
        public JiraStatus status { get; set; }
        public object[] components { get; set; }
        public object timeoriginalestimate { get; set; }
        public object description { get; set; }
        public string summary { get; set; }
        public JiraAggregateprogress aggregateprogress { get; set; }
        public object environment { get; set; }
        public object duedate { get; set; }
        public JiraProgress progress { get; set; }
    }

    public class JiraIssuetype
    {
        public string self { get; set; }
        public string id { get; set; }
        public string description { get; set; }
        public string iconUrl { get; set; }
        public string name { get; set; }
        public bool subtask { get; set; }
        public int avatarId { get; set; }
        public string entityId { get; set; }
    }

    public class JiraParent
    {
        public string id { get; set; }
        public string key { get; set; }
        public string self { get; set; }
        public JiraFields fields { get; set; }
    }

    public class JiraStatus
    {
        public string self { get; set; }
        public string description { get; set; }
        public string iconUrl { get; set; }
        public string name { get; set; }
        public string id { get; set; }
        public JiraStatuscategory statusCategory { get; set; }
    }

    public class JiraStatuscategory
    {
        public string self { get; set; }
        public int id { get; set; }
        public string key { get; set; }
        public string colorName { get; set; }
        public string name { get; set; }
    }

    public class JiraPriority
    {
        public string self { get; set; }
        public string iconUrl { get; set; }
        public string name { get; set; }
        public string id { get; set; }
    }

    public class JiraProject
    {
        public string self { get; set; }
        public string id { get; set; }
        public string key { get; set; }
        public string name { get; set; }
        public string projectTypeKey { get; set; }
        public bool simplified { get; set; }
    }

    public class JiraAssignee
    {
        public string self { get; set; }
        public string accountId { get; set; }
        public string emailAddress { get; set; }
        public string displayName { get; set; }
        public bool active { get; set; }
        public string timeZone { get; set; }
        public string accountType { get; set; }
    }

    public class JiraAggregateprogress
    {
        public int progress { get; set; }
        public int total { get; set; }
    }

    public class JiraProgress
    {
        public int progress { get; set; }
        public int total { get; set; }
    }


    public class Rootobject
    {
        
    }

    public class JiraSprint
    {
        public int id { get; set; }
        public string name { get; set; }
        public string state { get; set; }
        public int boardId { get; set; }
        public string goal { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
    }

}