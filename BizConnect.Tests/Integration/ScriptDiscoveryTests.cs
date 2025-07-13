using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BizConnect.Tests.Integration;

/// <summary>
/// Integration tests for database migration script PostgreSQL client discovery
/// </summary>
[Trait("Category", "Scripts")]
public class ScriptDiscoveryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _mockPsqlPath;
    private readonly string? _originalPgBin;

    public ScriptDiscoveryTests()
    {
        // Create temporary directory for mock psql
        _tempDir = Path.Combine(Path.GetTempPath(), $"BizConnect_ScriptTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Create mock psql executable
        _mockPsqlPath = CreateMockPsql();

        // Store original PG_BIN value
        _originalPgBin = Environment.GetEnvironmentVariable("PG_BIN");
    }

    public void Dispose()
    {
        // Restore original PG_BIN
        if (_originalPgBin != null)
        {
            Environment.SetEnvironmentVariable("PG_BIN", _originalPgBin);
        }
        else
        {
            Environment.SetEnvironmentVariable("PG_BIN", null);
        }

        // Clean up temporary directory
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task PowerShellScript_WithPgBinSet_ShouldUsePgBinPath()
    {
        // Skip on non-Windows platforms
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange
        Environment.SetEnvironmentVariable("PG_BIN", _tempDir);

        // Act
        var result = await RunPowerShellScript("-WhatIf");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"Using PostgreSQL client from PG_BIN: {_mockPsqlPath}", result.Output);
    }

    [Fact]
    public async Task BashScript_WithPgBinSet_ShouldUsePgBinPath()
    {
        // Arrange
        Environment.SetEnvironmentVariable("PG_BIN", _tempDir);

        // Act
        var result = await RunBashScript();

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"Using PostgreSQL client from PG_BIN: {_mockPsqlPath}", result.Output);
    }

    [Fact]
    public async Task PowerShellScript_WithoutPsql_ShouldShowInstallInstructions()
    {
        // Skip on non-Windows platforms
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange - clear PG_BIN to force failure
        Environment.SetEnvironmentVariable("PG_BIN", null);

        // Act
        var result = await RunPowerShellScript("-WhatIf");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("PostgreSQL client 'psql' not found", result.Output);
        Assert.Contains("choco install postgresql", result.Output);
        Assert.Contains("PG_BIN", result.Output);
    }

    [Fact]
    public async Task BashScript_WithoutPsql_ShouldShowInstallInstructions()
    {
        // Arrange - clear PG_BIN to force failure
        Environment.SetEnvironmentVariable("PG_BIN", null);

        // Act
        var result = await RunBashScript();

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("PostgreSQL client 'psql' not found", result.Output);
        Assert.Contains("Installation instructions:", result.Output);
        Assert.Contains("PG_BIN", result.Output);
    }

    private string CreateMockPsql()
    {
        string mockPsqlPath;
        string mockContent;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            mockPsqlPath = Path.Combine(_tempDir, "psql.exe");
            mockContent = "@echo off\necho Mock PostgreSQL client\nexit 0";
        }
        else
        {
            mockPsqlPath = Path.Combine(_tempDir, "psql");
            mockContent = "#!/bin/bash\necho \"Mock PostgreSQL client\"\nexit 0";
        }

        File.WriteAllText(mockPsqlPath, mockContent);

        // Make executable on Unix-like systems
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x {mockPsqlPath}",
                UseShellExecute = false
            });
            chmod?.WaitForExit();
        }

        return mockPsqlPath;
    }

    private async Task<ScriptResult> RunPowerShellScript(string arguments = "")
    {
        var scriptPath = GetScriptPath("update-db.ps1");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-File \"{scriptPath}\" {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = GetProjectRoot()
        };

        // Copy current environment variables
        foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
        {
            var key = env.Key.ToString();
            var value = env.Value?.ToString();
            if (key != null && value != null)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start PowerShell process");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ScriptResult
        {
            ExitCode = process.ExitCode,
            Output = output + error
        };
    }

    private async Task<ScriptResult> RunBashScript()
    {
        var scriptPath = GetScriptPath("update-db.sh");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"\"{scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = GetProjectRoot()
        };

        // Copy current environment variables
        foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
        {
            var key = env.Key.ToString();
            var value = env.Value?.ToString();
            if (key != null && value != null)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start Bash process");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ScriptResult
        {
            ExitCode = process.ExitCode,
            Output = output + error
        };
    }

    private static string GetProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "BizConnect.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new DirectoryNotFoundException("Could not find project root");
    }

    private static string GetScriptPath(string scriptName)
    {
        var projectRoot = GetProjectRoot();
        return Path.Combine(projectRoot, "scripts", scriptName);
    }

    private record ScriptResult
    {
        public int ExitCode { get; init; }
        public string Output { get; init; } = string.Empty;
    }
}
