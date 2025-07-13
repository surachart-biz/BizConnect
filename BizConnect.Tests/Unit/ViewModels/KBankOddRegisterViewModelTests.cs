using BizConnect.ViewModels;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace BizConnect.Tests.Unit.ViewModels;

public class KBankOddRegisterViewModelTests
{
    [Fact]
    public void Email_WithValidEmail_PassesValidation()
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = "National ID",
            IdValue = "1234567890123"
        };

        // Act
        var validationResults = ValidateModel(viewModel);

        // Assert
        Assert.DoesNotContain(validationResults, v => v.MemberNames.Contains("Email"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    public void Email_WithInvalidEmail_FailsValidation(string email)
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = email,
            MobileNo = "0812345678",
            IdType = "National ID",
            IdValue = "1234567890123"
        };

        // Act
        var validationResults = ValidateModel(viewModel);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("Email"));
    }

    [Theory]
    [InlineData("0812345678")]
    [InlineData("+66812345678")]
    [InlineData("+6681234567")]
    public void MobileNo_WithValidMobile_PassesValidation(string mobileNo)
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = mobileNo,
            IdType = "National ID",
            IdValue = "1234567890123"
        };

        // Act
        var validationResults = ValidateModel(viewModel);

        // Assert
        Assert.DoesNotContain(validationResults, v => v.MemberNames.Contains("MobileNo"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("123456789")]
    [InlineData("09123456789")]
    [InlineData("+661234567")]
    [InlineData("abc12345678")]
    public void MobileNo_WithInvalidMobile_FailsValidation(string mobileNo)
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = mobileNo,
            IdType = "National ID",
            IdValue = "1234567890123"
        };

        // Act
        var validationResults = ValidateModel(viewModel);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("MobileNo"));
    }

    [Theory]
    [InlineData("National ID")]
    [InlineData("Passport")]
    [InlineData("Tax ID")]
    [InlineData("Company Tax ID")]
    public void IdType_WithValidIdType_PassesValidation(string idType)
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = idType,
            IdValue = "1234567890123"
        };

        // Act
        var validationResults = ValidateModel(viewModel);

        // Assert
        Assert.DoesNotContain(validationResults, v => v.MemberNames.Contains("IdType"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void IdType_WithInvalidIdType_FailsValidation(string idType)
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = idType,
            IdValue = "1234567890123"
        };

        // Act
        var validationResults = ValidateModel(viewModel);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("IdType"));
    }

    [Fact]
    public void IdValue_WithValidNationalId_PassesCustomValidation()
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = "National ID",
            IdValue = "1234567890123"
        };

        // Act
        var validationResults = ValidateModel(viewModel);

        // Assert
        Assert.DoesNotContain(validationResults, v => v.MemberNames.Contains("IdValue"));
    }

    [Fact]
    public void IdValue_WithInvalidNationalId_FailsCustomValidation()
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = "National ID",
            IdValue = "12345678901" // Only 11 digits
        };

        // Act
        var validationResults = ValidateModel(viewModel);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("IdValue") && 
                                               v.ErrorMessage.Contains("National ID must be 13 digits"));
    }

    [Fact]
    public void IdValue_WithValidPassport_PassesCustomValidation()
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = "Passport",
            IdValue = "AB1234567"
        };

        // Act
        var validationResults = ValidateModel(viewModel);

        // Assert
        Assert.DoesNotContain(validationResults, v => v.MemberNames.Contains("IdValue"));
    }

    [Fact]
    public void IdValue_WithInvalidPassport_FailsCustomValidation()
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = "Passport",
            IdValue = "AB123" // Too short
        };

        // Act
        var validationResults = ValidateModel(viewModel);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("IdValue") && 
                                               v.ErrorMessage.Contains("Passport number must be 8-20 alphanumeric characters"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1234567")] // Too short
    [InlineData("123456789012345678901234567890123")] // Too long
    public void IdValue_WithInvalidLength_FailsValidation(string idValue)
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = "National ID",
            IdValue = idValue
        };

        // Act
        var validationResults = ValidateModel(viewModel);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("IdValue"));
    }

    [Fact]
    public void IdTypes_ContainsExpectedOptions()
    {
        // Act
        var idTypes = KBankOddRegisterViewModel.IdTypes;

        // Assert
        Assert.Equal(4, idTypes.Count);
        Assert.Contains(idTypes, item => item.Value == "National ID" && item.Text == "National ID");
        Assert.Contains(idTypes, item => item.Value == "Passport" && item.Text == "Passport");
        Assert.Contains(idTypes, item => item.Value == "Tax ID" && item.Text == "Tax ID");
        Assert.Contains(idTypes, item => item.Value == "Company Tax ID" && item.Text == "Company Tax ID");
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        
        // Also run custom validation if the model implements IValidatableObject
        if (model is IValidatableObject validatableObject)
        {
            validationResults.AddRange(validatableObject.Validate(validationContext));
        }
        
        return validationResults;
    }
}
