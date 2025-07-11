using System;
using BizConnect.Dal;
using Microsoft.EntityFrameworkCore;

namespace BizConnect.Tests.Integration;

/// <summary>
/// Smoke test to verify that scaffolded Entity Framework models compile and can be instantiated.
/// This test ensures that the database scaffolding process produces valid C# code.
/// </summary>
public class DbScaffoldSmokeTest
{
    [Fact]
    public void BizConnectContext_CanBeInstantiated()
    {
        // Arrange & Act
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // This will throw a compilation error if the scaffolded models are invalid
        using var context = new BizConnectContext(options);

        // Assert
        Assert.NotNull(context);
        Assert.NotNull(context.Users);
    }

    [Fact]
    public void BizConnectContext_HasCorrectConfiguration()
    {
        // Arrange & Act
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new BizConnectContext(options);

        // Assert - Verify the context has the expected DbSets and configuration
        Assert.NotNull(context);
        Assert.NotNull(context.Users);

        // Verify the model configuration
        var model = context.Model;
        var userEntityType = model.FindEntityType(typeof(BizConnect.Dal.Models.User));
        Assert.NotNull(userEntityType);

        // Verify key properties exist
        var idProperty = userEntityType.FindProperty("Id");
        var usernameProperty = userEntityType.FindProperty("Username");
        var passwordHashProperty = userEntityType.FindProperty("PasswordHash");
        var roleProperty = userEntityType.FindProperty("Role");

        Assert.NotNull(idProperty);
        Assert.NotNull(usernameProperty);
        Assert.NotNull(passwordHashProperty);
        Assert.NotNull(roleProperty);

        // Verify primary key
        Assert.True(idProperty.IsKey());
    }

    [Fact]
    public void DbContext_ModelsNamespace_IsCorrect()
    {
        // Verify that scaffolded models exist in the correct namespace
        var assembly = typeof(BizConnectContext).Assembly;
        var modelTypes = assembly.GetTypes()
            .Where(t => t.Namespace == "BizConnect.Dal.Models")
            .ToList();

        // Models should exist after scaffolding
        Assert.NotEmpty(modelTypes);

        // All model types should be in the correct namespace
        Assert.All(modelTypes, modelType =>
        {
            Assert.Equal("BizConnect.Dal.Models", modelType.Namespace);
        });

        // Verify specific models exist
        var userType = modelTypes.FirstOrDefault(t => t.Name == "User");
        Assert.NotNull(userType);
    }
}
