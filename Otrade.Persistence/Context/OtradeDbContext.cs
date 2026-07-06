using Microsoft.EntityFrameworkCore;
using Otrade.Domain.Entities;
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
    public DbSet<EmailLog> EmailLogs { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<JobLock> jobLocks { get; set; }
    public DbSet<EmailVerificationCode> EmailVerificationCodes { get; set; }
    public DbSet<UserWalletAddress> UserWalletAddresses { get; set; }
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

            entity.Property(x => x.Balance)
                .HasPrecision(18, 8);

            entity.Property(x => x.WalletType)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasIndex(x => x.UserId);
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

            entity.Property(x => x.Type)
                .HasConversion<string>()
                .HasMaxLength(30);
        });
        modelBuilder.Entity<WalletTransfer>(entity =>
        {
            entity.HasKey(x => x.TransferId);
        });
        modelBuilder.Entity<TicketMessage>(entity =>
        {
            entity.HasKey(x => x.MessageId);
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

            entity.Property(x => x.Type)
                .HasConversion<int>();
        });

        modelBuilder.Entity<Deposit>(entity =>
        {
            entity.ToTable("Deposits");

            entity.HasIndex(x => x.TxId).IsUnique();

            entity.Property(x => x.Amount).HasPrecision(18, 8);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
             

        });

        modelBuilder.Entity<Withdrawal>(entity =>
        {
            entity.ToTable("Withdrawals");

            entity.Property(x => x.Amount).HasPrecision(18, 8);

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
            entity.Property(x => x.DocumentType)
                    .HasConversion<string>()
                    .HasMaxLength(30);
            //entity.Property(x => x.DocumentType)
            //    .HasMaxLength(30);

            //entity.Property(x => x.FilePath)
            //    .HasMaxLength(500);

            //entity.HasIndex(x => x.UserId);
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

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.Property(x => x.Balance)
                .HasPrecision(18, 8)
                .HasDefaultValue(0);
            entity.Property(x=> x.WalletType)
            .HasConversion<string>()
            .HasMaxLength(30);
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
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.User)
                  .WithMany()
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(30);
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