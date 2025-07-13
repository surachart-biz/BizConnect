using BizConnect.Controllers;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.KBank;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace BizConnect.Tests.Unit.Controllers;

public class KBankControllerTests
{
    private readonly Mock<IKbankOddService> _mockKbankOddService;
    private readonly Mock<ILogger<KBankController>> _mockLogger;
    private readonly KBankController _controller;

    public KBankControllerTests()
    {
        _mockKbankOddService = new Mock<IKbankOddService>();
        _mockLogger = new Mock<ILogger<KBankController>>();
        _controller = new KBankController(_mockKbankOddService.Object, _mockLogger.Object);

        // Setup controller context with authenticated user
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "User")
        }, "test"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        
        // Setup TempData
        _controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task Register_WithValidService_ReturnsRedirect()
    {
        // Arrange
        var redirectUrl = "https://test.kasikornbank.com/PGSRegistration.do?reg_id=TEST123&langLocale=th_TH";
        _mockKbankOddService.Setup(s => s.StartRegistrationRedirectUrlAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(redirectUrl);

        // Act
        var result = await _controller.Register();

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal(redirectUrl, redirectResult.Url);
    }

    [Fact]
    public async Task Register_WithServiceException_RedirectsToHomeWithError()
    {
        // Arrange
        _mockKbankOddService.Setup(s => s.StartRegistrationRedirectUrlAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service error"));

        // Act
        var result = await _controller.Register();

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal("Home", redirectResult.ControllerName);
        
        // Verify TempData was set
        Assert.True(_controller.TempData.ContainsKey("ErrorMessage"));
    }

    [Fact]
    public async Task StatusUpdate_WithSuccessResult_ReturnsOk()
    {
        // Arrange
        var dto = new StatusUpdateDto
        {
            ExternalReference = "BIZ20250713123456789",
            ReturnStatus = "0",
            ReturnCode = "0000",
            ReturnMessage = "Success",
            Timestamp = "20250713123456",
            AuthParameter = "VALID_HASH"
        };

        _mockKbankOddService.Setup(s => s.ProcessStatusUpdateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatusProcessResult.Success);

        // Act
        var result = await _controller.StatusUpdate(dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Status updated successfully", okResult.Value);
    }

    [Fact]
    public async Task StatusUpdate_WithFailResult_ReturnsOk()
    {
        // Arrange
        var dto = new StatusUpdateDto
        {
            ExternalReference = "BIZ20250713123456789",
            ReturnStatus = "1",
            ReturnCode = "9999",
            ReturnMessage = "Failed",
            Timestamp = "20250713123456",
            AuthParameter = "VALID_HASH"
        };

        _mockKbankOddService.Setup(s => s.ProcessStatusUpdateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatusProcessResult.Fail);

        // Act
        var result = await _controller.StatusUpdate(dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Registration failed - status updated", okResult.Value);
    }

    [Fact]
    public async Task StatusUpdate_WithUnauthorizedResult_ReturnsUnauthorized()
    {
        // Arrange
        var dto = new StatusUpdateDto
        {
            ExternalReference = "BIZ20250713123456789",
            AuthParameter = "INVALID_HASH"
        };

        _mockKbankOddService.Setup(s => s.ProcessStatusUpdateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatusProcessResult.Unauthorized);

        // Act
        var result = await _controller.StatusUpdate(dto);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid authentication", unauthorizedResult.Value);
    }

    [Fact]
    public async Task StatusUpdate_WithNotFoundResult_ReturnsNotFound()
    {
        // Arrange
        var dto = new StatusUpdateDto
        {
            ExternalReference = "BIZ20250713123456789",
            AuthParameter = "VALID_HASH"
        };

        _mockKbankOddService.Setup(s => s.ProcessStatusUpdateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatusProcessResult.NotFound);

        // Act
        var result = await _controller.StatusUpdate(dto);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Registration record not found", notFoundResult.Value);
    }

    [Fact]
    public async Task StatusUpdate_WithServiceException_ReturnsInternalServerError()
    {
        // Arrange
        var dto = new StatusUpdateDto
        {
            ExternalReference = "BIZ20250713123456789",
            AuthParameter = "VALID_HASH"
        };

        _mockKbankOddService.Setup(s => s.ProcessStatusUpdateAsync(dto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await _controller.StatusUpdate(dto);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("Internal server error", statusResult.Value);
    }

    [Fact]
    public void Success_ReturnsViewWithCorrectTitle()
    {
        // Act
        var result = _controller.Success();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Registration Successful", _controller.ViewData["Title"]);
    }

    [Fact]
    public void Failure_ReturnsViewWithCorrectTitle()
    {
        // Act
        var result = _controller.Failure();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Registration Failed", _controller.ViewData["Title"]);
    }
}
