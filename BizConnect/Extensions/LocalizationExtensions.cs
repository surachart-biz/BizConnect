using Microsoft.AspNetCore.Localization;
using BizConnect.Dal.Models;
using System.Globalization;
using Microsoft.Extensions.Localization;

namespace BizConnect.Extensions
{
    /// <summary>
    /// Extension methods for multi-language database content and localization
    /// </summary>
    public static class LocalizationExtensions
    {
        /// <summary>
        /// Get localized branch name based on current culture
        /// </summary>
        public static string GetLocalizedName(this Branch branch, HttpContext context)
        {
            if (branch == null) return string.Empty;

            var feature = context.Features.Get<IRequestCultureFeature>();
            var culture = feature?.RequestCulture?.Culture?.Name ?? "en-US";
            
            return culture.StartsWith("th") 
                ? branch.NameTh ?? branch.NameEn ?? branch.Name ?? "Unknown Branch"
                : branch.NameEn ?? branch.Name ?? branch.NameTh ?? "Unknown Branch";
        }
        
        /// <summary>
        /// Get localized branch address based on current culture
        /// </summary>
        public static string GetLocalizedAddress(this Branch branch, HttpContext context)
        {
            if (branch == null) return string.Empty;

            var feature = context.Features.Get<IRequestCultureFeature>();
            var culture = feature?.RequestCulture?.Culture?.Name ?? "en-US";
            
            return culture.StartsWith("th") 
                ? branch.AddressTh ?? branch.AddressEn ?? branch.Address ?? ""
                : branch.AddressEn ?? branch.Address ?? branch.AddressTh ?? "";
        }

        /// <summary>
        /// Get current culture from HttpContext
        /// </summary>
        public static string GetCurrentCulture(this HttpContext context)
        {
            var feature = context.Features.Get<IRequestCultureFeature>();
            return feature?.RequestCulture?.Culture?.Name ?? "en-US";
        }

        /// <summary>
        /// Check if current culture is Thai
        /// </summary>
        public static bool IsThaiCulture(this HttpContext context)
        {
            return context.GetCurrentCulture().StartsWith("th");
        }

        /// <summary>
        /// Format DateTime according to Thai Buddhist calendar or Gregorian calendar
        /// </summary>
        public static string ToLocalizedString(this DateTime dateTime, HttpContext context, string format = "dd/MM/yyyy HH:mm")
        {
            if (context.IsThaiCulture())
            {
                return FormatThaiDateTime(dateTime, format);
            }
            return dateTime.ToString(format, new CultureInfo("en-US"));
        }

        /// <summary>
        /// Format nullable DateTime with localization
        /// </summary>
        public static string ToLocalizedString(this DateTime? dateTime, HttpContext context, string format = "dd/MM/yyyy HH:mm")
        {
            if (!dateTime.HasValue) return string.Empty;
            return dateTime.Value.ToLocalizedString(context, format);
        }

        /// <summary>
        /// Format DateTime for Thai Buddhist calendar
        /// </summary>
        private static string FormatThaiDateTime(DateTime dateTime, string format)
        {
            try
            {
                var thaiCalendar = new ThaiBuddhistCalendar();
                var buddhistYear = thaiCalendar.GetYear(dateTime);
                
                return format switch
                {
                    "dd/MM/yyyy HH:mm" => $"{dateTime.Day:D2}/{dateTime.Month:D2}/{buddhistYear} {dateTime:HH:mm}",
                    "dd/MM/yyyy" => $"{dateTime.Day:D2}/{dateTime.Month:D2}/{buddhistYear}",
                    "yyyy-MM-dd" => $"{buddhistYear}-{dateTime.Month:D2}-{dateTime.Day:D2}",
                    "MM/dd/yyyy" => $"{dateTime.Month:D2}/{dateTime.Day:D2}/{buddhistYear}",
                    _ => $"{dateTime.Day:D2}/{dateTime.Month:D2}/{buddhistYear} {dateTime:HH:mm}"
                };
            }
            catch
            {
                return dateTime.ToString(format, CultureInfo.GetCultureInfo("th-TH"));
            }
        }

        /// <summary>
        /// Get localized relative time
        /// </summary>
        public static string ToRelativeTimeString(this DateTime dateTime, HttpContext context)
        {
            var timeSpan = DateTime.Now - dateTime;
            var isThaiCulture = context.IsThaiCulture();

            return timeSpan.TotalMinutes switch
            {
                < 1 => isThaiCulture ? "เมื่อสักครู่" : "Just now",
                < 60 => isThaiCulture ? $"{Math.Floor(timeSpan.TotalMinutes)} นาทีที่แล้ว" : $"{Math.Floor(timeSpan.TotalMinutes)} minutes ago",
                < 1440 => isThaiCulture ? $"{Math.Floor(timeSpan.TotalHours)} ชั่วโมงที่แล้ว" : $"{Math.Floor(timeSpan.TotalHours)} hours ago",
                < 43200 => isThaiCulture ? $"{Math.Floor(timeSpan.TotalDays)} วันที่แล้ว" : $"{Math.Floor(timeSpan.TotalDays)} days ago",
                _ => dateTime.ToLocalizedString(context)
            };
        }

        /// <summary>
        /// Get localized relative time for nullable DateTime
        /// </summary>
        public static string ToRelativeTimeString(this DateTime? dateTime, HttpContext context)
        {
            if (!dateTime.HasValue) return string.Empty;
            return dateTime.Value.ToRelativeTimeString(context);
        }

        /// <summary>
        /// Get localized labels
        /// </summary>
        public static string GetExpiryLabel(this HttpContext context)
        {
            return context.IsThaiCulture() ? "หมดอายุ" : "Expires";
        }

        public static string GetCreatedLabel(this HttpContext context)
        {
            return context.IsThaiCulture() ? "สร้างเมื่อ" : "Created";
        }

        public static string GetUpdatedLabel(this HttpContext context)
        {
            return context.IsThaiCulture() ? "อัปเดต" : "Updated";
        }

        public static string GetAttemptsLabel(this HttpContext context)
        {
            return context.IsThaiCulture() ? "ความพยายาม" : "Attempts";
        }

        public static string GetUserIdLabel(this HttpContext context)
        {
            return context.IsThaiCulture() ? "รหัส" : "ID";
        }

        /// <summary>
        /// Get localized OTAC status
        /// </summary>
        public static string GetLocalizedOtacStatus(this string otacState, HttpContext context)
        {
            if (!context.IsThaiCulture()) return otacState ?? "Unknown";

            return otacState?.ToLower() switch
            {
                "generated" => "สร้างแล้ว",
                "validated" => "ตรวจสอบแล้ว",
                "used" => "ใช้แล้ว",
                "expired" => "หมดอายุ",
                "locked" => "ล็อก",
                _ => "ไม่ทราบ"
            };
        }

        /// <summary>
        /// Get localized registration status
        /// </summary>
        public static string GetLocalizedRegistrationStatus(this string status, HttpContext context)
        {
            if (!context.IsThaiCulture()) return status ?? "Unknown";

            return status?.ToLower() switch
            {
                "pending" => "รอดำเนินการ",
                "success" => "สำเร็จ",
                "completed" => "เสร็จสิ้น",
                "failed" => "ล้มเหลว",
                "fail" => "ล้มเหลว",
                _ => "ไม่ทราบ"
            };
        }

        /// <summary>
        /// Get user display name with fallback
        /// </summary>
        public static string GetDisplayName(this object user)
        {
            if (user == null) return "Unknown User";
            
            var userType = user.GetType();
            var usernameProperty = userType.GetProperty("Username");
            var idProperty = userType.GetProperty("Id");
            
            var username = usernameProperty?.GetValue(user)?.ToString();
            var id = idProperty?.GetValue(user)?.ToString();
            
            return !string.IsNullOrWhiteSpace(username) ? username : $"User {id}";
        }

        /// <summary>
        /// Get user avatar initials
        /// </summary>
        public static string GetAvatarInitials(this object user)
        {
            if (user == null) return "??";
            
            var userType = user.GetType();
            var usernameProperty = userType.GetProperty("Username");
            var username = usernameProperty?.GetValue(user)?.ToString();
            
            if (string.IsNullOrEmpty(username)) return "??";
            
            var parts = username.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            return parts.Length switch
            {
                >= 2 => $"{parts[0][0]}{parts[1][0]}".ToUpper(),
                1 => username.Length >= 2 ? username.Substring(0, 2).ToUpper() : username.ToUpper(),
                _ => "??"
            };
        }

        /// <summary>
        /// Get user role display name
        /// </summary>
        public static string GetRoleDisplayName(this object user, HttpContext context)
        {
            if (user == null) return context.IsThaiCulture() ? "ไม่ระบุ" : "Unspecified";
            
            var userType = user.GetType();
            var roleProperty = userType.GetProperty("Role");
            var role = roleProperty?.GetValue(user)?.ToString();
            
            if (string.IsNullOrEmpty(role)) return context.IsThaiCulture() ? "ไม่ระบุ" : "Unspecified";
            
            if (!context.IsThaiCulture()) return role;

            return role.ToLower() switch
            {
                "admin" => "ผู้ดูแลระบบ",
                "user" => "ผู้ใช้",
                "manager" => "ผู้จัดการ",
                "employee" => "พนักงาน",
                _ => role
            };
        }

        /// <summary>
        /// Get user status badge class
        /// </summary>
        public static string GetStatusBadgeClass(this object user)
        {
            if (user == null) return "bg-secondary";
            
            var userType = user.GetType();
            var isActiveProperty = userType.GetProperty("IsActive");
            var isActive = isActiveProperty?.GetValue(user) as bool?;
            
            return isActive switch
            {
                true => "bg-success",
                false => "bg-danger",
                _ => "bg-secondary"
            };
        }

        /// <summary>
        /// Check if user is online (last activity within timespan)
        /// </summary>
        public static bool IsOnline(this object user, TimeSpan threshold)
        {
            if (user == null) return false;
            
            var userType = user.GetType();
            var updatedAtProperty = userType.GetProperty("UpdatedAt");
            var updatedAt = updatedAtProperty?.GetValue(user) as DateTime?;
            
            if (updatedAt == null) return false;
            return DateTime.Now - updatedAt.Value <= threshold;
        }
    }
}