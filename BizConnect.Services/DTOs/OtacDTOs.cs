namespace BizConnect.Services.DTOs
{
    public class OtacResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string OtacCode { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        
        // Missing properties that controllers expect
        public int AttemptsRemaining { get; set; } = 5;
        public TimeSpan? LockoutTimeRemaining { get; set; }
        public bool IsLocked { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
    }

    public class VerifyOtacResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public int AttemptsRemaining { get; set; }
        public bool IsLocked { get; set; }
        public TimeSpan? LockoutTimeRemaining { get; set; }
    }

    public class SessionExtensionResult
    {
        public bool Extended { get; set; }
        public DateTime NewExpiryTime { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SessionInfo
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class CsrfTokenResult
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}