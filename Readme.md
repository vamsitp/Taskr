### **`taskr`**
Tool (dotnet) to View **Azure DevOps** _Tasks'_ details by _States_ and other _Fields_

**`dotnet tool install -g --ignore-failed-sources taskr`**
> \> `taskr`

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
