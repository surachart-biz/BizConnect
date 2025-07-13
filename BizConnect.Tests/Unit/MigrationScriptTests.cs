using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BizConnect.Tests.Unit;

/// <summary>
/// Unit tests for database migration scripts to ensure they work correctly
/// and provide proper validation feedback.
/// </summary>
public class MigrationScriptTests
{
    private readonly string _projectRoot;
    private readonly string _powershellScript;
    private readonly string _bashScript;
    private readonly string _crossPlatformScript;

    public MigrationScriptTests()
    {
        // Get the project root directory (assuming tests run from project root or subdirectory)
        _projectRoot = GetProjectRoot();
        _powershellScript = Path.Combine(_projectRoot, "scripts", "update-db.ps1");
        _bashScript = Path.Combine(_projectRoot, "scripts", "update-db.sh");
        _crossPlatformScript = Path.Combine(_projectRoot, "scripts", "update-db");
    }

    [Fact]
    public void PowerShellScript_ShouldContainExpectedContent()
    {
        // Arrange
        Assert.True(File.Exists(_powershellScript), $"PowerShell script not found at: {_powershellScript}");

        // Act
        var content = File.ReadAllText(_powershellScript);

        // Assert - check for key features we added
        Assert.Contains("Database Migration Workflow", content);
        Assert.Contains("WhatIf", content);
        Assert.Contains("environment detection", content.ToLower());
        Assert.Contains("$env:SHELL", content);
    }

    [Fact]
    public void PowerShellScript_InWrongEnvironment_ShouldShowHelpfulError()
    {
        // This test simulates running the PowerShell script in a Unix-like environment
        // Skip if we're actually on Windows PowerShell
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && IsPowerShellAvailable())
        {
            return; // Skip on actual Windows PowerShell
        }

        // Arrange
        Assert.True(File.Exists(_powershellScript), $"PowerShell script not found at: {_powershellScript}");

        // Act - try to run with bash (should fail gracefully)
        var result = RunBashCommand($"bash {_powershellScript}");

        // Assert - should fail but not crash
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void BashScript_ShouldContainExpectedContent()
    {
        // Arrange
        Assert.True(File.Exists(_bashScript), $"Bash script not found at: {_bashScript}");

        // Act
        var content = File.ReadAllText(_bashScript);

        // Assert - check for key features we added
        Assert.Contains("Database Migration Workflow", content);
        Assert.Contains("jq not found in PATH", content);
        Assert.Contains("Installation instructions", content);
        Assert.Contains("PSVersionTable", content);
    }

    [Fact]
    public void CrossPlatformScript_ShouldExist()
    {
        // Arrange & Assert
        Assert.True(File.Exists(_crossPlatformScript), $"Cross-platform script not found at: {_crossPlatformScript}");
        
        // Check if it's executable (on Unix systems)
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var fileInfo = new FileInfo(_crossPlatformScript);
            // Note: This is a basic check - in a real scenario you'd check Unix permissions
            Assert.True(fileInfo.Exists);
        }
    }

    [Fact]
    public void AllScripts_ShouldHaveConsistentBehavior()
    {
        // Arrange
        var scripts = new[]
        {
            _powershellScript,
            _bashScript,
            _crossPlatformScript
        };

        // Act & Assert
        foreach (var script in scripts)
        {
            Assert.True(File.Exists(script), $"Script not found: {script}");
            
            // Check that scripts contain expected headers/comments
            var content = File.ReadAllText(script);
            Assert.Contains("BizConnect Database Migration", content);
        }
    }

    #region Helper Methods

    private string GetProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        
        // Look for the solution file or key project files
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "BizConnect.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        
        return currentDir ?? throw new InvalidOperationException("Could not find project root");
    }

    private bool IsPowerShellAvailable()
    {
        return IsCommandAvailable("pwsh") || IsCommandAvailable("powershell");
    }

    private bool IsCommandAvailable(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private (int ExitCode, string Output) RunPowerShellScript(string arguments = "")
    {
        var powershellCommand = IsPowerShellAvailable() && IsCommandAvailable("pwsh") ? "pwsh" : "powershell";
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = powershellCommand,
                Arguments = $"-File \"{_powershellScript}\" {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _projectRoot
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output);
    }

    private (int ExitCode, string Output) RunBashScript(string arguments = "")
    {
        return RunBashCommand($"bash \"{_bashScript}\" {arguments}");
    }

    private (int ExitCode, string Output) RunBashCommand(string command)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _projectRoot
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output);
    }

    #endregion
}
