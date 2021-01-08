### **`taskr`**
Tool (dotnet) to View **Azure DevOps** _Tasks'_ details by _States_ and other _Fields_

**`dotnet tool install -g --ignore-failed-sources taskr`**

![Screenshot](https://github.com/vamsitp/Taskr/blob/master/Screenshot.png?raw=true)
> Type Index / Work-item ID / Search term 

##### Settings: `%USERPROFILE%\Documents\Taskr.json`   
(For relocated _Documents_ folder: `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders`)

```json
{
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
      "Org": "Org-2",
      "Project": "Project-2",
      "Token": "PAT Token for Org-2/Project-2",
      "Query": "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{0}' AND [System.WorkItemType] = 'Task' AND [System.AreaPath] UNDER 'My Project Team' ORDER BY [System.Id] ASC", // Override
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
