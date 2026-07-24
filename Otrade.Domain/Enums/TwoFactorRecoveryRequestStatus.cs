namespace Otrade.Domain.Enums;

public enum TwoFactorRecoveryRequestStatus
{
    PendingEmailVerification = 1,

    PendingAdminReview = 2,

    Approved = 3,

    Rejected = 4,

    Canceled = 5,

    Expired = 6
}