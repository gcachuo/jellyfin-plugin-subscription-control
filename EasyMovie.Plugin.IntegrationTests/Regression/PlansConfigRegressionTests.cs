using System.IO;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace EasyMovie.Plugin.IntegrationTests.Regression;

/// <summary>
/// Regression tests for plans.json configuration file
/// These tests verify the configuration file directly to prevent
/// deploying with insecure settings
/// </summary>
public class PlansConfigRegressionTests
{
    /// <summary>
    /// Test ID: CFG-REG-001
    /// Given: plans.json configuration file
    /// When: File is read and parsed
    /// Then: test_mode must be FALSE
    /// 
    /// CRITICAL: This test reads the actual plans.json file
    /// and fails if test_mode is enabled. This prevents deploying
    /// configuration that would allow unauthorized access.
    /// 
    /// Set PLANS_JSON_PATH environment variable to test:
    /// export PLANS_JSON_PATH="/path/to/api/plans.json"
    /// </summary>
    [Fact]
    public void PlansJson_MustNotHaveTestModeEnabled()
    {
        // Arrange
        var plansJsonPath = System.Environment.GetEnvironmentVariable("PLANS_JSON_PATH");
        
        // Skip if path not configured
        if (string.IsNullOrEmpty(plansJsonPath))
        {
            // Test is skipped - this is expected in CI/CD
            return;
        }

        // Verify file exists
        File.Exists(plansJsonPath).Should().BeTrue(
            $"plans.json file must exist at: {plansJsonPath}");

        // Act - Read and parse plans.json
        var jsonContent = File.ReadAllText(plansJsonPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        // Assert - CRITICAL SECURITY CHECKS
        root.TryGetProperty("test_mode", out var testModeElement).Should().BeTrue(
            "plans.json must have 'test_mode' property");

        var testMode = testModeElement.GetBoolean();
        testMode.Should().BeFalse(
            "CRITICAL: test_mode must be FALSE in plans.json! " +
            "Having test_mode=true in production allows users in test_users array " +
            "to bypass all subscription restrictions. " +
            $"Current value in {plansJsonPath}: test_mode={testMode}");

        // Additional check: if test_mode is true, at least test_users should be empty
        if (testMode)
        {
            if (root.TryGetProperty("test_users", out var testUsersElement))
            {
                var testUsersCount = testUsersElement.GetArrayLength();
                testUsersCount.Should().Be(0,
                    "If test_mode is enabled (which it shouldn't be), " +
                    "at least test_users array should be empty");
            }
        }
    }

    /// <summary>
    /// Test ID: CFG-REG-002
    /// Given: plans.json configuration file
    /// When: test_users array is checked
    /// Then: Array should be empty in production
    /// 
    /// Even if test_mode is false, having users in test_users
    /// is a security risk if test_mode is accidentally enabled
    /// </summary>
    [Fact]
    public void PlansJson_TestUsersArrayShouldBeEmpty()
    {
        // Arrange
        var plansJsonPath = System.Environment.GetEnvironmentVariable("PLANS_JSON_PATH");
        
        // Skip if path not configured
        if (string.IsNullOrEmpty(plansJsonPath))
        {
            return;
        }

        // Verify file exists
        if (!File.Exists(plansJsonPath))
        {
            return;
        }

        // Act
        var jsonContent = File.ReadAllText(plansJsonPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        // Assert
        if (root.TryGetProperty("test_users", out var testUsersElement))
        {
            var testUsersCount = testUsersElement.GetArrayLength();
            testUsersCount.Should().Be(0,
                "test_users array should be empty in production configuration. " +
                "Having users in this array is a security risk if test_mode is accidentally enabled. " +
                $"Current count: {testUsersCount}");
        }
    }

    /// <summary>
    /// Test ID: CFG-REG-003
    /// Given: plans.json configuration file
    /// When: Plans are validated
    /// Then: All plans must have required fields
    /// 
    /// Verifies data integrity of plans configuration
    /// </summary>
    [Fact]
    public void PlansJson_AllPlansMustHaveRequiredFields()
    {
        // Arrange
        var plansJsonPath = System.Environment.GetEnvironmentVariable("PLANS_JSON_PATH");
        
        // Skip if path not configured
        if (string.IsNullOrEmpty(plansJsonPath))
        {
            return;
        }

        if (!File.Exists(plansJsonPath))
        {
            return;
        }

        // Act
        var jsonContent = File.ReadAllText(plansJsonPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        // Assert
        root.TryGetProperty("plans", out var plansElement).Should().BeTrue(
            "plans.json must have 'plans' array");

        var plansArray = plansElement.EnumerateArray();
        var planCount = 0;

        foreach (var plan in plansArray)
        {
            planCount++;
            var planId = plan.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;

            // Required fields
            plan.TryGetProperty("id", out _).Should().BeTrue(
                $"Plan #{planCount} must have 'id' field");
            plan.TryGetProperty("name", out _).Should().BeTrue(
                $"Plan {planId} must have 'name' field");
            plan.TryGetProperty("allow_live_tv", out _).Should().BeTrue(
                $"Plan {planId} must have 'allow_live_tv' field");

            // Folder configuration
            var hasEnableAllFolders = plan.TryGetProperty("enable_all_folders", out var enableAllElement);
            hasEnableAllFolders.Should().BeTrue(
                $"Plan {planId} must have 'enable_all_folders' field");

            if (hasEnableAllFolders && !enableAllElement.GetBoolean())
            {
                // If not enabling all folders, must have enabled_folder_ids
                plan.TryGetProperty("enabled_folder_ids", out var foldersElement).Should().BeTrue(
                    $"Plan {planId} with enable_all_folders=false must have 'enabled_folder_ids' array");

                if (foldersElement.ValueKind == JsonValueKind.Array)
                {
                    foldersElement.GetArrayLength().Should().BeGreaterThan(0,
                        $"Plan {planId} must have at least one folder in 'enabled_folder_ids'");
                }
            }
        }

        planCount.Should().BeGreaterThan(0, "plans.json must have at least one plan");
    }
}
