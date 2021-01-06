```bash
# Add the Artifacts source to %appdata%\Nuget\NuGet.Config
# dotnet nuget add source https://api.nuget.org/v3/index.json -n Nuget

# Install from AzDO Artifacts
dotnet tool install -g --ignore-failed-sources taskr

# Optional (For local debugging purposes): Install from local project path
dotnet tool install -g --ignore-failed-sources --add-source ./bin taskr

# Publish package to nuget.org (Optional)
dotnet nuget push --source "Nuget" --api-key az "./bin/Taskr.1.0.0.nupkg"
```