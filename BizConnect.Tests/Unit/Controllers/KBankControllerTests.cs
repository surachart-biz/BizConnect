using BizConnect.Controllers;
using BizConnect.Dal;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.KBank;
using BizConnect.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace BizConnect.Tests.Unit.Controllers;

public class KBankControllerTests : IDisposable
{
    private readonly Mock<IKbankOddService> _mockKbankOddService;
    private readonly Mock<ILogger<KBankController>> _mockLogger;
    private readonly BizConnectContext _context;
    private readonly KBankController _controller;

    public KBankControllerTests()
    {
        _mockKbankOddService = new Mock<IKbankOddService>();
        _mockLogger = new Mock<ILogger<KBankController>>();

        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BizConnectContext(options);

        _controller = new KBankController(_mockKbankOddService.Object, _mockLogger.Object, _context);

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
    public void Register_Get_ReturnsFormView()
    {
        // Act
        var result = _controller.Register();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<KBankOddRegisterViewModel>(viewResult.Model);
        Assert.NotNull(model);
    }

    // Removed obsolete test - GET Register now just shows the form

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

    [Fact]
    public void Register_Get_ReturnsViewWithViewModel()
    {
        // Act
        var result = _controller.Register();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<KBankOddRegisterViewModel>(viewResult.Model);
        Assert.NotNull(model);
    }

    [Fact]
    public async Task Register_Post_WithValidModel_RedirectsToKBank()
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = "National ID",
            IdValue = "1234567890123"
        };

        var expectedRedirectUrl = "https://test.kasikornbank.com/PGSRegistration.do?reg_id=TEST123&langLocale=th_TH";
        _mockKbankOddService.Setup(s => s.StartRegistrationAsync(It.IsAny<OddRegistrationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRedirectUrl);

        // Act
        var result = await _controller.Register(viewModel);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal(expectedRedirectUrl, redirectResult.Url);

        _mockKbankOddService.Verify(s => s.StartRegistrationAsync(
            It.Is<OddRegistrationRequest>(req =>
                req.Email == "test@example.com" &&
                req.MobileNo == "0812345678" &&
                req.IdType == "National ID" &&
                req.IdValue == "1234567890123"
            ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_Post_WithInvalidModel_ReturnsViewWithModel()
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "invalid-email",
            MobileNo = "123", // Invalid mobile
            IdType = "",
            IdValue = ""
        };

        _controller.ModelState.AddModelError("Email", "Please enter a valid email address");
        _controller.ModelState.AddModelError("MobileNo", "Mobile number must be in format 08xxxxxxxx or +66xxxxxxxx");
        _controller.ModelState.AddModelError("IdType", "ID type is required");
        _controller.ModelState.AddModelError("IdValue", "ID number is required");

        // Act
        var result = await _controller.Register(viewModel);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<KBankOddRegisterViewModel>(viewResult.Model);
        Assert.Equal(viewModel, model);
        Assert.False(_controller.ModelState.IsValid);

        _mockKbankOddService.Verify(s => s.StartRegistrationAsync(It.IsAny<OddRegistrationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Register_Post_WithServiceException_ReturnsViewWithError()
    {
        // Arrange
        var viewModel = new KBankOddRegisterViewModel
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = "National ID",
            IdValue = "1234567890123"
        };

        _mockKbankOddService.Setup(s => s.StartRegistrationAsync(It.IsAny<OddRegistrationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("KBank service error"));

        // Act
        var result = await _controller.Register(viewModel);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<KBankOddRegisterViewModel>(viewResult.Model);
        Assert.Equal(viewModel, model);
        Assert.True(_controller.ModelState.ContainsKey(string.Empty));
        Assert.Contains("Unable to process registration", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
