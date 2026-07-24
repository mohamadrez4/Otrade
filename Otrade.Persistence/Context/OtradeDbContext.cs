using Microsoft.EntityFrameworkCore;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using System.Diagnostics.Contracts;
using System.Net.Sockets;

namespace Otrade.Persistence.Context;

public class OtradeDbContext : DbContext
{
    public OtradeDbContext(DbContextOptions<OtradeDbContext> options)
        : base(options)
    {
    }

    // ================= USERS =================
    public DbSet<User> Users { get; set; }

    // ================= WALLETS =================
    public DbSet<Wallet> Wallets { get; set; }

    // ================= RANKS =================
    public DbSet<Rank> Ranks { get; set; }

    public DbSet<RankHistory> RankHistories { get; set; }

    public DbSet<ProfitLedger>ProfitLedgers { get; set; }
    // ================= CONTRACTS =================
    public DbSet<Domain.Entities.Contract> Contracts { get; set; }

    // ================= DEPOSITS =================
    public DbSet<Deposit> Deposits { get; set; }

    // ================= WITHDRAWALS =================
    public DbSet<Withdrawal> Withdrawals { get; set; }

    // ================= REFERRAL =================
    public DbSet<ReferralRelation> ReferralRelations { get; set; }
    public DbSet<ReferralBonusRecord> ReferralBonusRecords { get; set; }

    public DbSet<BonusCode> BonusCodes => Set<BonusCode>();

    public DbSet<BonusCodeUsage> BonusCodeUsages => Set<BonusCodeUsage>();
    // ================= WALLET =================
    public DbSet<WalletTransaction> WalletTransactions { get; set; }
    public DbSet<WalletTransfer> WalletTransfers { get; set; }

    // ================= KYC =================
    public DbSet<KycDocument> KycDocuments { get; set; }

    // ================= PROFIT =================

    // ================= TICKETS =================
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketMessage> TicketMessages { get; set; }

    // ================= SYSTEM =================
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<InvestmentCapacity> InvestmentCapacities { get; set; }
    public DbSet<InvestmentWaitListEntry> InvestmentWaitListEntries => Set<InvestmentWaitListEntry>();
    public DbSet<TemporaryRegistration> TemporaryRegistrations { get; set; }
    public DbSet<EmailLog> EmailLogs { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    public DbSet<JobLock> JobLocks { get; set; }
    public DbSet<EmailVerificationCode> EmailVerificationCodes { get; set; }
    public DbSet<InternalTransferVerification> InternalTransferVerifications => Set<InternalTransferVerification>();
    public DbSet<WithdrawalVerification> WithdrawalVerifications => Set<WithdrawalVerification>();
    public DbSet<TwoFactorLoginChallenge> TwoFactorLoginChallenges => Set<TwoFactorLoginChallenge>();
    public DbSet<UserRecoveryCode> UserRecoveryCodes => Set<UserRecoveryCode>();
    public DbSet<TwoFactorRecoveryRequest> TwoFactorRecoveryRequests => Set<TwoFactorRecoveryRequest>();
    public DbSet<UserWalletAddress> UserWalletAddresses { get; set; }
    public DbSet<WalletBalanceSnapshot> WalletBalanceSnapshots { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(x => x.UserId);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasIndex(x => x.Email).IsUnique();

            entity.HasIndex(x => x.ReferralCode).IsUnique();

            entity.Property(x => x.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.LastName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.PasswordHash)
                .IsRequired();

            entity.Property(x => x.KycStatus)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(x => x.AdminRole)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(x => x.IsTotpEnabled)
                    .HasDefaultValue(false)
                .IsRequired();

            entity.Property(x => x.TotpSecretEncrypted)
                .HasMaxLength(1000);

            entity.Property(x => x.TotpSetupCreatedAt);

            entity.Property(x => x.TotpEnabledAt);

            entity.Property(x => x.LastAcceptedTotpStep);
            entity.Property(x =>
                x.PendingTotpSecretEncrypted)
                .HasMaxLength(1000);

            entity.Property(x =>
                x.PendingTotpCreatedAt);

            entity.Property(x =>
                x.TotpRecoveryLockedUntil);

            entity.Property(x =>
                    x.AuthTokenVersion)
                .HasDefaultValue(1)
                .IsRequired();

            entity.Property(x =>
                    x.MustChangePassword)
                .HasDefaultValue(false)
                .IsRequired();
            // Sponsor FK
            entity.HasOne(x => x.Sponsor)
                .WithMany()
                .HasForeignKey(x => x.SponsorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Current Rank FK
            entity.HasOne<Rank>()
                .WithMany()
                .HasForeignKey(x => x.CurrentRankId)
                .OnDelete(DeleteBehavior.Restrict);


            // Wallets
            entity.HasMany(x => x.Wallets)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable("Wallets");

            entity.HasKey(x => x.WalletId);

            entity.Property(x => x.WalletType)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Balance)
                .HasPrecision(18, 8)
                .HasDefaultValue(0);

            entity.Property(x => x.IsLocked)
                .HasDefaultValue(false);

            entity.HasIndex(x => x.UserId);

            entity.HasIndex(x => new { x.UserId, x.WalletType })
                .IsUnique();
        });
        modelBuilder.Entity<WalletBalanceSnapshot>(entity =>
        {
            entity.ToTable("WalletBalanceSnapshots");

            entity.HasKey(x => x.SnapshotId);

            entity.Property(x => x.SnapshotDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(x => x.TotalMainWallet)
                .HasPrecision(18, 8);

            entity.Property(x => x.TotalInvestWallet)
                .HasPrecision(18, 8);

            entity.Property(x => x.TotalProfitWallet)
                .HasPrecision(18, 8);

            entity.Property(x => x.TotalReferralWallet)
                .HasPrecision(18, 8);

            entity.Property(x => x.TotalAssets)
                .HasPrecision(18, 8);

            entity.HasIndex(x => x.SnapshotDate)
                .IsUnique();
        });
        modelBuilder.Entity<Rank>(entity =>
        {
            entity.ToTable("Ranks");

            entity.HasKey(x => x.RankId);

            entity.Property(x => x.MonthlyProfitPercent).HasPrecision(10, 2);
            entity.Property(x => x.DailyProfitPercent).HasPrecision(10, 2);
            entity.Property(x => x.RequiredVolume).HasPrecision(18, 2);

              });


        modelBuilder.Entity<ReferralBonusRecord>(entity =>
        {
            entity.HasKey(x => x.BonusId);

            entity.HasOne(x => x.FromUser)
                  .WithMany()
                  .HasForeignKey(x => x.FromUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ToUser)
                  .WithMany()
                  .HasForeignKey(x => x.ToUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ReferralRelation>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Ancestor)
                .WithMany()
                .HasForeignKey(x => x.AncestorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Descendant)
                .WithMany()
                .HasForeignKey(x => x.DescendantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.ToTable("WalletTransactions");

            entity.HasKey(x => x.TransactionId);

            entity.Property(x => x.Amount).HasPrecision(18, 8);
            entity.Property(x => x.BalanceBefore).HasPrecision(18, 8);
            entity.Property(x => x.BalanceAfter).HasPrecision(18, 8);

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.WalletId);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Description)
                .HasMaxLength(255);
            entity.Property(x => x.Type)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired(); 
        });
        modelBuilder.Entity<WalletTransfer>(entity =>
        {
            entity.ToTable("WalletTransfers");

            entity.HasKey(x => x.TransferId);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 8);

            entity.Property(x => x.Description)
                .HasMaxLength(500);

            entity.HasOne(x => x.FromWallet)
                .WithMany()
                .HasForeignKey(x => x.FromWalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ToWallet)
                .WithMany()
                .HasForeignKey(x => x.ToWalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.FromWalletId);
            entity.HasIndex(x => x.ToWalletId);
            entity.HasIndex(x => x.CreatedAt);
        });
        modelBuilder.Entity<TicketMessage>(entity =>
        {
            entity.HasKey(x => x.MessageId);
        });
       

        modelBuilder.Entity<Deposit>(entity =>
        {
            entity.ToTable("Deposits");

            entity.HasIndex(x => x.TxId).IsUnique();

            entity.Property(x => x.Amount).HasPrecision(18, 8);
            entity.Property(x => x.SiteWalletAddress)
                 .HasMaxLength(300);

            entity.Property(x => x.Network)
                .HasMaxLength(50);
            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(x => x.AdminNote)
                .HasMaxLength(1000);

        });

        modelBuilder.Entity<Withdrawal>(entity =>
        {
            entity.ToTable("Withdrawals");

            entity.Property(x => x.Amount)
                .HasPrecision(18, 8);

            entity.Property(x => x.WalletAddress)
                .HasMaxLength(300);

            entity.Property(x => x.Network)
                .HasMaxLength(50);

            entity.Property(x => x.AdminNote)
                .HasMaxLength(1000);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20);
        });

        modelBuilder.Entity<KycDocument>(entity =>
        {
            entity.ToTable("KycDocuments");

            entity.HasKey(x => x.DocumentId);
            entity.Property(x => x.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);
            entity.Property(x => x.RejectReason)
                   .HasMaxLength(1000);
            entity.Property(x => x.DocumentType)
                    .HasConversion<string>()
                    .HasMaxLength(30);

        });

        modelBuilder.Entity<Domain.Entities.Contract>(entity =>
        {
            entity.ToTable("Contracts");

            entity.HasIndex(x => x.UserId);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20);
        });



        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets");

            entity.Property(x => x.Status)
                .HasMaxLength(20);
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.ToTable("EmailLogs");

            entity.HasIndex(x => x.ToEmail);
        });



        modelBuilder.Entity<EmailVerificationCode>(entity =>
        {
            entity.ToTable("EmailVerificationCodes");

            entity.HasKey(x => x.VerificationId);

            entity.Property(x => x.Code)
                .HasMaxLength(10)
                .IsRequired();

            entity.HasIndex(x => x.UserId);
        });
        modelBuilder.Entity<InternalTransferVerification>(entity =>
        {
            entity.ToTable("InternalTransferVerifications");

            entity.HasKey(x => x.InternalTransferVerificationId);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 8);

            entity.Property(x => x.Description)
                .HasMaxLength(500);

            entity.Property(x => x.CodeHash)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.HasOne(x => x.SenderUser)
                .WithMany()
                .HasForeignKey(x => x.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReceiverUser)
                .WithMany()
                .HasForeignKey(x => x.ReceiverUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.SenderUserId,
                x.Status,
                x.CreatedAt
            });
        });
        modelBuilder.Entity<WithdrawalVerification>(entity =>
        {
            entity.ToTable("WithdrawalVerifications");

            entity.HasKey(x => x.WithdrawalVerificationId);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 8);

            entity.Property(x => x.WalletAddress)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(x => x.Network)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(x => x.VerificationMethod)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(WithdrawalVerificationMethod.Email)
                .IsRequired();
            entity.Property(x => x.CodeHash)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.UserId,
                x.Status,
                x.CreatedAt
            });
        });
        modelBuilder.Entity<TwoFactorLoginChallenge>(entity =>
        {
            entity.ToTable("TwoFactorLoginChallenges");

            entity.HasKey(x => x.ChallengeId);

            entity.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(64)
                .IsUnicode(false);

            entity.Property(x => x.Attempts)
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(x => x.IsUsed)
                .HasDefaultValue(false)
                .IsRequired();

            entity.HasOne(x => x.User)
                .WithMany(x => x.TwoFactorLoginChallenges)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.TokenHash)
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.UserId,
                x.IsUsed,
                x.ExpiresAt
            });
        });

        modelBuilder.Entity<UserRecoveryCode>(entity =>
        {
            entity.ToTable("UserRecoveryCodes");

            entity.HasKey(x => x.UserRecoveryCodeId);

            entity.Property(x => x.CodeHash)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasOne(x => x.User)
                .WithMany(x => x.RecoveryCodes)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.UserId,
                x.UsedAt
            });
        });
        modelBuilder.Entity<TwoFactorRecoveryRequest>(
            entity =>
            {
                entity.ToTable(
                    "TwoFactorRecoveryRequests");

                entity.HasKey(x =>
                    x.TwoFactorRecoveryRequestId);

                entity.Property(x =>
                        x.PublicTokenHash)
                    .IsRequired()
                    .HasMaxLength(64)
                    .IsUnicode(false);

                entity.Property(x =>
                        x.EmailCodeHash)
                    .IsRequired()
                    .HasMaxLength(300);

                entity.Property(x =>
                        x.Attempts)
                    .HasDefaultValue(0)
                    .IsRequired();

                entity.Property(x =>
                        x.Status)
                    .HasConversion<string>()
                    .HasMaxLength(40)
                    .IsRequired();

                entity.Property(x =>
                        x.UserDescription)
                    .HasMaxLength(1000)
                    .IsRequired();

                entity.Property(x =>
                        x.AdminNote)
                    .HasMaxLength(1000);

                entity.HasOne(x =>
                        x.User)
                    .WithMany(x =>
                        x.TwoFactorRecoveryRequests)
                    .HasForeignKey(x =>
                        x.UserId)
                    .OnDelete(
                        DeleteBehavior.Restrict);

                entity.HasOne(x =>
                        x.ReviewedByAdmin)
                    .WithMany()
                    .HasForeignKey(x =>
                        x.ReviewedByAdminId)
                    .OnDelete(
                        DeleteBehavior.Restrict);

                entity.HasIndex(x =>
                        x.PublicTokenHash)
                    .IsUnique();

                entity.HasIndex(x => new
                {
                    x.UserId,
                    x.Status,
                    x.CreatedAt
                });

                entity.HasIndex(x => new
                {
                    x.Status,
                    x.CreatedAt
                });
            });
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Key)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Value)
                .IsRequired();

            entity.HasIndex(x => x.Key)
                .IsUnique();
        });
        modelBuilder.Entity<TemporaryRegistration>(entity =>
        {
            entity.ToTable("TemporaryRegistrations");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.ReferralCode)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.DepositTxId)
                .HasMaxLength(200);

            entity.Property(x => x.ApprovedAmount)
                .HasPrecision(18, 8);

            entity.Property(x => x.DeclaredAmount)
                .HasPrecision(18, 8);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.RejectReason)
                .HasMaxLength(500);
            entity.Property(x => x.EmailVerificationCode)
                .HasMaxLength(10);
            entity.Property(x => x.CompletionToken)
                .HasMaxLength(200);
            entity.Property(x => x.TrackingToken)
                .HasMaxLength(200);
            entity.Property(x => x.RecoveryVerificationCode)
                .HasMaxLength(10);
            entity.Property(x => x.SiteWalletAddress)
                .HasMaxLength(300);

            entity.Property(x => x.Network)
                .HasMaxLength(50);
            entity.HasIndex(x => x.Email);

            entity.HasIndex(x => new { x.Status, x.CreatedAt });

            entity.HasIndex(x => x.ExpiresAt);

            entity.HasIndex(x => x.DepositTxId)
                .IsUnique()
                .HasFilter("[DepositTxId] IS NOT NULL");
            entity.HasIndex(x => x.TrackingToken)
                .IsUnique()
                .HasFilter("[TrackingToken] IS NOT NULL");
            entity.HasIndex(x => x.CompletionToken)
                .IsUnique()
                .HasFilter("[CompletionToken] IS NOT NULL");

            entity.HasOne(x => x.Sponsor)
                .WithMany()
                .HasForeignKey(x => x.SponsorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedByUser)
                .WithMany()
                .HasForeignKey(x => x.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CompletedUser)
                .WithMany()
                .HasForeignKey(x => x.CompletedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<InvestmentCapacity>(entity =>
        {
            entity.ToTable("InvestmentCapacities");

            entity.HasKey(x => x.CapacityId);

            entity.Property(x => x.MonthStart)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(x => x.TotalCapacity)
                .HasPrecision(18, 8)
                .IsRequired();

            entity.Property(x => x.UsedCapacity)
                .HasPrecision(18, 8)
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .HasDefaultValue(true)
                .IsRequired();

            entity.HasIndex(x => x.MonthStart)
                .IsUnique();

            entity.HasIndex(x => new { x.IsActive, x.MonthStart });
        });
        modelBuilder.Entity<InvestmentWaitListEntry>(entity =>
        {
            entity.ToTable("InvestmentWaitListEntries");

            entity.HasKey(x => x.WaitListId);

            entity.Property(x => x.RequestedAmount)
                .HasPrecision(18, 8);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.AdminNote)
                .HasMaxLength(1000);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.UserId);

            entity.HasIndex(x => new
            {
                x.Status,
                x.CreatedAt
            });

            entity.HasIndex(x => x.CreatedAt);
        });
        modelBuilder.Entity<Rank>().HasData(
    new Rank
    {
        RankId = 1,
        Name = "Basic",
        RequiredVolume = 0,
        DailyProfitPercent = 0.27m,
        MonthlyProfitPercent = 6,
        ReferralProfitPercent = 10,
        MainToInvestPercent = 1,
        SortOrder = 1
    },
    new Rank
    {
        RankId = 2,
        Name = "Bronze",
        RequiredVolume = 10000,
        DailyProfitPercent = 0.32m,
        MonthlyProfitPercent = 7,
        ReferralProfitPercent = 15,
        MainToInvestPercent = 1.5m,
        SortOrder = 2
    },
    new Rank
    {
        RankId = 3,
        Name = "Silver",
        RequiredVolume = 30000,
        DailyProfitPercent = 0.36m,
        MonthlyProfitPercent = 8,
        ReferralProfitPercent = 20,
        MainToInvestPercent = 2,
        SortOrder = 3
    },
    new Rank
    {
        RankId = 4,
        Name = "Gold",
        RequiredVolume = 100000,
        DailyProfitPercent = 0.45m,
        MonthlyProfitPercent = 10,
        ReferralProfitPercent = 25,
        MainToInvestPercent = 2.5m,
        SortOrder = 4
    },
    new Rank
    {
        RankId = 5,
        Name = "Diamond",
        RequiredVolume = 200000,
        DailyProfitPercent = 0.54m,
        MonthlyProfitPercent = 12,
        ReferralProfitPercent = 30,
        MainToInvestPercent = 3,
        SortOrder = 5
    }
);

        modelBuilder.Entity<ProfitLedger>(entity =>
        {
            entity.ToTable("ProfitLedgers");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 8);

            entity.Property(x => x.ReferenceId)
                .HasMaxLength(100);

            entity.Property(x => x.Type)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(x => x.RealCapitalAmount)
                .HasPrecision(18, 8);

            entity.Property(x => x.BonusCapitalAmount)
                .HasPrecision(18, 8);

            entity.Property(x => x.ProfitBaseAmount)
                .HasPrecision(18, 8);

            entity.HasOne(x => x.EffectiveRank)
                .WithMany()
                .HasForeignKey(x => x.EffectiveRankId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.EffectiveRankId);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SourceUser)
                .WithMany()
                .HasForeignKey(x => x.SourceUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.SourceUserId);
            entity.HasIndex(x => x.CreatedAt);
        });
        modelBuilder.Entity<BonusCode>(entity =>
        {
            entity.ToTable("BonusCodes");

            entity.HasKey(x => x.BonusCodeId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.Property(x => x.CampaignName)
                .HasMaxLength(150);

            entity.Property(x => x.Description)
                .HasMaxLength(500);

            entity.Property(x => x.IsActive)
                .HasDefaultValue(true);

            entity.Property(x => x.IsSingleUse)
                .HasDefaultValue(true);

            entity.Property(x => x.MaxUsageCount)
                .HasDefaultValue(1);

            entity.Property(x => x.UsedCount)
                .HasDefaultValue(0);

            entity.Property(x => x.BonusType)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.BonusCapitalPercent)
                .HasPrecision(9, 4);

            entity.HasOne(x => x.BonusRank)
                .WithMany()
                .HasForeignKey(x => x.BonusRankId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByAdmin)
                .WithMany()
                .HasForeignKey(x => x.CreatedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.IsActive,
                x.ExpiresAt
            });
        });

        modelBuilder.Entity<BonusCodeUsage>(entity =>
        {
            entity.ToTable("BonusCodeUsages");

            entity.HasKey(x => x.UsageId);

            entity.Property(x => x.RealCapitalAmount)
                .HasPrecision(18, 8)
                .HasDefaultValue(0);

            entity.Property(x => x.BonusCapitalAmount)
                .HasPrecision(18, 8)
                .HasDefaultValue(0);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.AdminNote)
                .HasMaxLength(1000);

            entity.HasOne(x => x.BonusCode)
                .WithMany(x => x.Usages)
                .HasForeignKey(x => x.BonusCodeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AppliedRank)
                .WithMany()
                .HasForeignKey(x => x.AppliedRankId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.UserId,
                x.Status
            });

            entity.HasIndex(x => new
            {
                x.BonusCodeId,
                x.UserId
            })
            .IsUnique();
        });
        modelBuilder.Entity<JobLock>()
            .HasKey(x => x.JobName);
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes()
        .SelectMany(e => e.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
        // Fluent Configs (بعداً کامل می‌کنیم)
    }
}