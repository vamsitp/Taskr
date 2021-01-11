### **`taskr`**
Tool (dotnet) to View **Azure DevOps** (or **Jira**) _Tasks'_ details by _States_ and other _Fields_
> **_Pre-req_**: Install [`dotnet core 3.1 runtime`](https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-3.1.10-windows-x64-installer) / [sdk](https://download.visualstudio.microsoft.com/download/pr/3366b2e6-ed46-48ae-bf7b-f5804f6ee4c9/186f681ff967b509c6c9ad31d3d343da/dotnet-sdk-3.1.404-win-x64.exe) (if not already installed)_   
**`dotnet tool install -g --ignore-failed-sources taskr`**   
> \> **`taskr`**

![Screenshot](https://github.com/vamsitp/Taskr/blob/master/Screenshot.png?raw=true)
> Type: Index / Work-item ID / Search term / Field=Search-term   

Examples:   
> \> `2` // Index of the Account to fetch the Work-items for   
> \> `ENTER` // Display all Work-items for the Account   
> \> `5680` // ID of the Work-item to print the details for   
> \> `secure practices` // Phrase to filter the Work-items (searches across all _fields_)   
> \> `tags=security` // _field-name_ and _value_ to filter the Work-items (searches the specified _field_ for the provided _value_)   
> \> `open 5680` // Opens the Work-item (ID: 5680) in the default browser   
> \> `cls` // Clears the console   
> \> `quit` // Quits the app   
> \> `+` // Updates Taskr to latest version   
> \> `?` // Print Help   

##### Settings: `%USERPROFILE%\Documents\Taskr.json`   
(For relocated _Documents_ folder: `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders`)

```json
{
  "CheckUpdates": false, // Check for updates when Taskr is run
  // Defaults
  "Query": "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{0}' AND [System.WorkItemType] = 'Task' ORDER BY [System.Id] ASC",
  "Slicers": "Tags,Priority,IterationPath",  
  "Accounts": [ // Azure DevOps Project details
    {
      "Name": "Account-1",
      "Org": "Org-1",
      "Project": "Project-1",
      "Token": "PAT Token for Org-1/Project-1",      
      "Slicers": "AssignedTo,Priority,IterationPath" // Override
      "Enabled": true
    },
    {
      "Name": "Account-2",
      "Type": "Jira", // For Jira projects
      "Org": "Org-2",
      "Project": "Project-2",
      "Token": "user@email.com:apiToken", // Basic-auth format
      "Query": "project={0} AND type=Subtask", // JQL query override
      "Enabled": true
    }
  ]
}
```

> You can override the default `Query` and `Slicers` values at each _Account_ level in `Taskr.json`   

---

#### Contribution
> [![pre-commit](https://img.shields.io/badge/pre--commit-enabled-brightgreen?logo=pre-commit&logoColor=white)](https://github.com/pre-commit/pre-commit)<br />
> [Install Python (and Pip)](https://www.python.org/downloads/) for `pre-commit hooks`

> **`pip install pre-commit`**
```batch
# Clone spex
git clone https://github.com/vamsitp/Taskr.git

# Important!!!
git config --global init.templateDir .git-template
```
