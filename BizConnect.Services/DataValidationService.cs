using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.Common;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services;

/// <summary>
/// Service for data validation that maps database constraints to frontend-friendly validation rules
/// Ensures data integrity between database and UI layers
/// </summary>
public class DataValidationService : IDataValidationService
{
    private readonly BizConnectContext _context;
    private readonly ILogger<DataValidationService> _logger;
    
    // Validation patterns based on database constraints
    private static readonly Regex NationalIdPattern = new(@"^\d{13}$", RegexOptions.Compiled);
    private static readonly Regex PassportPattern = new(@"^[A-Z0-9]{6,12}$", RegexOptions.Compiled);
    private static readonly Regex TaxIdPattern = new(@"^\d{10,13}$", RegexOptions.Compiled);
    private static readonly Regex AccountNumberPattern = new(@"^\d{10,15}$", RegexOptions.Compiled);
    private static readonly Regex MobileNumberPattern = new(@"^(08\d{8}|\+66\d{8,9})$", RegexOptions.Compiled);
    private static readonly Regex OtacCodePattern = new(@"^[A-Z0-9]{8}$", RegexOptions.Compiled);
    private static readonly Regex ExternalReferencePattern = new(@"^BIZ\d{17}$", RegexOptions.Compiled);

    public DataValidationService(BizConnectContext context, ILogger<DataValidationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Validate KBank ODD registration data against database constraints
    /// </summary>
    /// <param name="registration">Registration data to validate</param>
    /// <returns>Validation result with detailed error information</returns>
    public async Task<ValidationResult> ValidateRegistrationAsync(KbankOddRegistration registration)
    {
        var result = new ValidationResult();
        var errors = new List<ValidationError>();

        try
        {
            // Required field validations
            if (string.IsNullOrWhiteSpace(registration.OtacCode))
            {
                errors.Add(new ValidationError
                {
                    Field = nameof(registration.OtacCode),
                    ErrorCode = "OTAC_REQUIRED",
                    Message = "OTAC code is required",
                    MessageTh = "รหัส OTAC จำเป็นต้องระบุ",
                    Severity = ValidationSeverity.Error
                });
            }
            else if (!OtacCodePattern.IsMatch(registration.OtacCode))
            {
                errors.Add(new ValidationError
                {
                    Field = nameof(registration.OtacCode),
                    ErrorCode = "OTAC_INVALID_FORMAT",
                    Message = "OTAC code must be 8 characters (A-Z, 0-9)",
                    MessageTh = "รหัส OTAC ต้องเป็นตัวอักษรและตัวเลข 8 ตัวอักษร",
                    Severity = ValidationSeverity.Error
                });
            }

            // Unique constraint validations
            if (!string.IsNullOrEmpty(registration.OtacCode))
            {
                var existingOtac = await _context.KbankOddRegistrations
                    .Where(r => r.OtacCode == registration.OtacCode && r.Id != registration.Id)
                    .AnyAsync();
                
                if (existingOtac)
                {
                    errors.Add(new ValidationError
                    {
                        Field = nameof(registration.OtacCode),
                        ErrorCode = "OTAC_DUPLICATE",
                        Message = "OTAC code already exists",
                        MessageTh = "รหัส OTAC นี้มีการใช้งานแล้ว",
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // External Reference validation
            if (!string.IsNullOrEmpty(registration.ExternalReference))
            {
                if (!ExternalReferencePattern.IsMatch(registration.ExternalReference))
                {
                    errors.Add(new ValidationError
                    {
                        Field = nameof(registration.ExternalReference),
                        ErrorCode = "EXTERNAL_REF_INVALID_FORMAT",
                        Message = "External reference must follow BIZyyyyMMddHHmmssfff format",
                        MessageTh = "รหัสอ้างอิงภายนอกต้องเป็นรูปแบบ BIZyyyyMMddHHmmssfff",
                        Severity = ValidationSeverity.Error
                    });
                }

                var existingRef = await _context.KbankOddRegistrations
                    .Where(r => r.ExternalReference == registration.ExternalReference && r.Id != registration.Id)
                    .AnyAsync();
                
                if (existingRef)
                {
                    errors.Add(new ValidationError
                    {
                        Field = nameof(registration.ExternalReference),
                        ErrorCode = "EXTERNAL_REF_DUPLICATE",
                        Message = "External reference already exists",
                        MessageTh = "รหัสอ้างอิงภายนอกนี้มีการใช้งานแล้ว",
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // ID Type and Value validation
            if (!string.IsNullOrEmpty(registration.IdType) && !string.IsNullOrEmpty(registration.IdValue))
            {
                var idValidation = ValidateIdTypeAndValue(registration.IdType, registration.IdValue);
                errors.AddRange(idValidation);
            }

            // Mobile number validation
            if (!string.IsNullOrEmpty(registration.MobileNo) && !MobileNumberPattern.IsMatch(registration.MobileNo))
            {
                errors.Add(new ValidationError
                {
                    Field = nameof(registration.MobileNo),
                    ErrorCode = "MOBILE_INVALID_FORMAT",
                    Message = "Mobile number must be in format 08xxxxxxxx or +66xxxxxxxx",
                    MessageTh = "หมายเลขโทรศัพท์ต้องเป็นรูปแบบ 08xxxxxxxx หรือ +66xxxxxxxx",
                    Severity = ValidationSeverity.Error
                });
            }

            // Account number validation
            if (!string.IsNullOrEmpty(registration.AccountNo) && !AccountNumberPattern.IsMatch(registration.AccountNo))
            {
                errors.Add(new ValidationError
                {
                    Field = nameof(registration.AccountNo),
                    ErrorCode = "ACCOUNT_INVALID_FORMAT",
                    Message = "Account number must be 10-15 digits",
                    MessageTh = "หมายเลขบัญชีต้องเป็นตัวเลข 10-15 หลัก",
                    Severity = ValidationSeverity.Error
                });
            }

            // Branch validation
            if (registration.BranchId.HasValue)
            {
                var branchExists = await _context.Branches
                    .Where(b => b.BranchId == registration.BranchId.Value && b.IsActive)
                    .AnyAsync();
                
                if (!branchExists)
                {
                    errors.Add(new ValidationError
                    {
                        Field = nameof(registration.BranchId),
                        ErrorCode = "BRANCH_NOT_FOUND",
                        Message = "Selected branch is not active or does not exist",
                        MessageTh = "สาขาที่เลือกไม่มีอยู่หรือไม่ได้เปิดใช้งาน",
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // User validation
            var userExists = await _context.Users
                .Where(u => u.Id == registration.GeneratedByUserId && u.IsActive)
                .AnyAsync();
            
            if (!userExists)
            {
                errors.Add(new ValidationError
                {
                    Field = nameof(registration.GeneratedByUserId),
                    ErrorCode = "USER_NOT_FOUND",
                    Message = "User does not exist or is not active",
                    MessageTh = "ไม่พบผู้ใช้หรือผู้ใช้ไม่ได้เปิดใช้งาน",
                    Severity = ValidationSeverity.Error
                });
            }

            // OTAC state validation
            var validStates = new[] { "Generated", "Validated", "Used", "Expired", "Invalidated", "Purged" };
            if (!string.IsNullOrEmpty(registration.OtacState) && !validStates.Contains(registration.OtacState))
            {
                errors.Add(new ValidationError
                {
                    Field = nameof(registration.OtacState),
                    ErrorCode = "OTAC_STATE_INVALID",
                    Message = $"OTAC state must be one of: {string.Join(", ", validStates)}",
                    MessageTh = $"สถานะ OTAC ต้องเป็นหนึ่งใน: {string.Join(", ", validStates)}",
                    Severity = ValidationSeverity.Error
                });
            }

            // Business rule validations
            await ValidateBusinessRules(registration, errors);

            result.IsValid = !errors.Any(e => e.Severity == ValidationSeverity.Error);
            result.Errors = errors;
            result.Summary = GenerateValidationSummary(errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration validation for ID: {RegistrationId}", registration.Id);
            
            errors.Add(new ValidationError
            {
                Field = "System",
                ErrorCode = "VALIDATION_ERROR",
                Message = "A system error occurred during validation",
                MessageTh = "เกิดข้อผิดพลาดของระบบระหว่างการตรวจสอบข้อมูล",
                Severity = ValidationSeverity.Error
            });
            
            result.IsValid = false;
            result.Errors = errors;
        }

        return result;
    }

    /// <summary>
    /// Validate branch data against constraints
    /// </summary>
    public async Task<ValidationResult> ValidateBranchAsync(Branch branch)
    {
        var result = new ValidationResult();
        var errors = new List<ValidationError>();

        try
        {
            // Required field validation
            if (string.IsNullOrWhiteSpace(branch.Name))
            {
                errors.Add(new ValidationError
                {
                    Field = nameof(branch.Name),
                    ErrorCode = "BRANCH_NAME_REQUIRED",
                    Message = "Branch name is required",
                    MessageTh = "ชื่อสาขาจำเป็นต้องระบุ",
                    Severity = ValidationSeverity.Error
                });
            }

            // Unique constraint validation
            if (!string.IsNullOrEmpty(branch.Code))
            {
                var existingCode = await _context.Branches
                    .Where(b => b.Code == branch.Code && b.BranchId != branch.BranchId)
                    .AnyAsync();
                
                if (existingCode)
                {
                    errors.Add(new ValidationError
                    {
                        Field = nameof(branch.Code),
                        ErrorCode = "BRANCH_CODE_DUPLICATE",
                        Message = "Branch code already exists",
                        MessageTh = "รหัสสาขานี้มีการใช้งานแล้ว",
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            result.IsValid = !errors.Any(e => e.Severity == ValidationSeverity.Error);
            result.Errors = errors;
            result.Summary = GenerateValidationSummary(errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during branch validation for ID: {BranchId}", branch.BranchId);
            result.IsValid = false;
        }

        return result;
    }

    /// <summary>
    /// Get frontend validation rules for a specific entity type
    /// </summary>
    public ValidationRules GetFrontendValidationRules(string entityType)
    {
        return entityType.ToLowerInvariant() switch
        {
            "registration" or "kbankoddregistration" => new ValidationRules
            {
                EntityType = "KbankOddRegistration",
                Rules = new Dictionary<string, FieldValidationRule>
                {
                    ["OtacCode"] = new FieldValidationRule
                    {
                        Required = true,
                        Pattern = @"^[A-Z0-9]{8}$",
                        MinLength = 8,
                        MaxLength = 8,
                        ErrorMessage = "OTAC code must be 8 characters (A-Z, 0-9)",
                        ErrorMessageTh = "รหัส OTAC ต้องเป็นตัวอักษรและตัวเลข 8 ตัวอักษร"
                    },
                    ["ExternalReference"] = new FieldValidationRule
                    {
                        Required = false,
                        Pattern = @"^BIZ\d{17}$",
                        ErrorMessage = "External reference must follow BIZyyyyMMddHHmmssfff format",
                        ErrorMessageTh = "รหัสอ้างอิงภายนอกต้องเป็นรูปแบบ BIZyyyyMMddHHmmssfff"
                    },
                    ["IdValue"] = new FieldValidationRule
                    {
                        Required = false,
                        MinLength = 6,
                        MaxLength = 30,
                        ErrorMessage = "ID value must be 6-30 characters",
                        ErrorMessageTh = "เลขประจำตัวต้องเป็น 6-30 ตัวอักษร",
                        CustomValidation = "validateIdByType" // Frontend function name
                    },
                    ["MobileNo"] = new FieldValidationRule
                    {
                        Required = false,
                        Pattern = @"^(08\d{8}|\+66\d{8,9})$",
                        ErrorMessage = "Mobile number must be in format 08xxxxxxxx or +66xxxxxxxx",
                        ErrorMessageTh = "หมายเลขโทรศัพท์ต้องเป็นรูปแบบ 08xxxxxxxx หรือ +66xxxxxxxx"
                    },
                    ["AccountNo"] = new FieldValidationRule
                    {
                        Required = false,
                        Pattern = @"^\d{10,15}$",
                        MinLength = 10,
                        MaxLength = 15,
                        ErrorMessage = "Account number must be 10-15 digits",
                        ErrorMessageTh = "หมายเลขบัญชีต้องเป็นตัวเลข 10-15 หลัก"
                    }
                }
            },
            "branch" => new ValidationRules
            {
                EntityType = "Branch",
                Rules = new Dictionary<string, FieldValidationRule>
                {
                    ["Name"] = new FieldValidationRule
                    {
                        Required = true,
                        MaxLength = 100,
                        ErrorMessage = "Branch name is required and cannot exceed 100 characters",
                        ErrorMessageTh = "ชื่อสาขาจำเป็นและต้องไม่เกิน 100 ตัวอักษร"
                    },
                    ["Code"] = new FieldValidationRule
                    {
                        Required = false,
                        MaxLength = 10,
                        ErrorMessage = "Branch code cannot exceed 10 characters",
                        ErrorMessageTh = "รหัสสาขาต้องไม่เกิน 10 ตัวอักษร"
                    }
                }
            },
            _ => new ValidationRules { EntityType = entityType, Rules = new Dictionary<string, FieldValidationRule>() }
        };
    }

    #region Private Helper Methods

    /// <summary>
    /// Validate ID type and value combination
    /// </summary>
    private List<ValidationError> ValidateIdTypeAndValue(string idType, string idValue)
    {
        var errors = new List<ValidationError>();

        switch (idType.ToUpperInvariant())
        {
            case "NATIONAL ID":
                if (!NationalIdPattern.IsMatch(idValue))
                {
                    errors.Add(new ValidationError
                    {
                        Field = "IdValue",
                        ErrorCode = "ID_VALUE_INVALID_NATIONAL",
                        Message = "National ID must be 13 digits",
                        MessageTh = "เลขประจำตัวประชาชนต้องเป็นตัวเลข 13 หลัก",
                        Severity = ValidationSeverity.Error
                    });
                }
                break;
            case "PASSPORT":
                if (!PassportPattern.IsMatch(idValue))
                {
                    errors.Add(new ValidationError
                    {
                        Field = "IdValue",
                        ErrorCode = "ID_VALUE_INVALID_PASSPORT",
                        Message = "Passport must be 6-12 alphanumeric characters",
                        MessageTh = "หนังสือเดินทางต้องเป็นตัวอักษรและตัวเลข 6-12 ตัว",
                        Severity = ValidationSeverity.Error
                    });
                }
                break;
            case "TAX ID":
            case "COMPANY TAX ID":
                if (!TaxIdPattern.IsMatch(idValue))
                {
                    errors.Add(new ValidationError
                    {
                        Field = "IdValue",
                        ErrorCode = "ID_VALUE_INVALID_TAX",
                        Message = "Tax ID must be 10-13 digits",
                        MessageTh = "เลขประจำตัวผู้เสียภาษีต้องเป็นตัวเลข 10-13 หลัก",
                        Severity = ValidationSeverity.Error
                    });
                }
                break;
        }

        return errors;
    }

    /// <summary>
    /// Validate business rules for registration
    /// </summary>
    private async Task ValidateBusinessRules(KbankOddRegistration registration, List<ValidationError> errors)
    {
        // OTAC expiry validation
        if (registration.OtacExpiresAt.HasValue && registration.OtacExpiresAt <= DateTime.Now)
        {
            errors.Add(new ValidationError
            {
                Field = nameof(registration.OtacExpiresAt),
                ErrorCode = "OTAC_EXPIRED",
                Message = "OTAC code has expired",
                MessageTh = "รหัส OTAC หมดอายุแล้ว",
                Severity = ValidationSeverity.Warning
            });
        }

        // State transition validation
        if (registration.OtacState == "Used" && string.IsNullOrEmpty(registration.Status))
        {
            errors.Add(new ValidationError
            {
                Field = nameof(registration.Status),
                ErrorCode = "STATUS_REQUIRED_FOR_USED",
                Message = "Status is required when OTAC is used",
                MessageTh = "ต้องระบุสถานะเมื่อ OTAC ถูกใช้งาน",
                Severity = ValidationSeverity.Error
            });
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Generate validation summary
    /// </summary>
    private ValidationSummary GenerateValidationSummary(List<ValidationError> errors)
    {
        return new ValidationSummary
        {
            TotalErrors = errors.Count(e => e.Severity == ValidationSeverity.Error),
            TotalWarnings = errors.Count(e => e.Severity == ValidationSeverity.Warning),
            ErrorFields = errors.Where(e => e.Severity == ValidationSeverity.Error).Select(e => e.Field).Distinct().ToList(),
            Message = errors.Any(e => e.Severity == ValidationSeverity.Error) 
                ? "Validation failed with errors" 
                : "Validation passed with warnings",
            MessageTh = errors.Any(e => e.Severity == ValidationSeverity.Error)
                ? "การตรวจสอบข้อมูลล้มเหลวเนื่องจากมีข้อผิดพลาด"
                : "การตรวจสอบข้อมูลผ่านแต่มีคำเตือน"
        };
    }

    #endregion
}

/// <summary>
/// Interface for data validation service
/// </summary>
public interface IDataValidationService
{
    Task<ValidationResult> ValidateRegistrationAsync(KbankOddRegistration registration);
    Task<ValidationResult> ValidateBranchAsync(Branch branch);
    ValidationRules GetFrontendValidationRules(string entityType);
}

#region Data Transfer Objects

/// <summary>
/// Validation error details
/// </summary>
public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string MessageTh { get; set; } = string.Empty;
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Validation result with comprehensive error information
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new List<ValidationError>();
    public ValidationSummary? Summary { get; set; }
}

/// <summary>
/// Validation severity levels
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Validation summary
/// </summary>
public class ValidationSummary
{
    public int TotalErrors { get; set; }
    public int TotalWarnings { get; set; }
    public List<string> ErrorFields { get; set; } = new List<string>();
    public string Message { get; set; } = string.Empty;
    public string MessageTh { get; set; } = string.Empty;
}

/// <summary>
/// Frontend validation rules
/// </summary>
public class ValidationRules
{
    public string EntityType { get; set; } = string.Empty;
    public Dictionary<string, FieldValidationRule> Rules { get; set; } = new Dictionary<string, FieldValidationRule>();
}

/// <summary>
/// Individual field validation rule
/// </summary>
public class FieldValidationRule
{
    public bool Required { get; set; }
    public string? Pattern { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorMessageTh { get; set; } = string.Empty;
    public string? CustomValidation { get; set; }
}

#endregion