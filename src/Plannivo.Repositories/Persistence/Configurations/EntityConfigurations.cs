using Plannivo.Repositories.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Plannivo.Repositories.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).UseIdentityColumn();
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.CreatedAt).HasColumnType("timestamptz");
        builder.Property(u => u.UpdatedAt).HasColumnType("timestamptz");
        builder.Property(u => u.PasswordResetTokenExpiry).HasColumnType("timestamptz");
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityColumn();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(r => r.Name).IsUnique();
        builder.Property(r => r.CreatedAt).HasColumnType("timestamptz");
        builder.Property(r => r.UpdatedAt).HasColumnType("timestamptz");

        builder.HasData(
            new Role { Id = 1, Name = "Admin", Description = "Administrator", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 2, Name = "User", Description = "Standard user", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });
        builder.Property(ur => ur.AssignedAt).HasColumnType("timestamptz");

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Id).UseIdentityColumn();
        builder.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(256);
        builder.HasIndex(rt => rt.TokenHash).IsUnique();
        builder.Property(rt => rt.ExpiresAt).HasColumnType("timestamptz");
        builder.Property(rt => rt.RevokedAt).HasColumnType("timestamptz");
        builder.Property(rt => rt.CreatedAt).HasColumnType("timestamptz");
        builder.Property(rt => rt.UpdatedAt).HasColumnType("timestamptz");
        builder.Property(rt => rt.CreatedByIp).HasMaxLength(45);

        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LoginActivityConfiguration : IEntityTypeConfiguration<LoginActivity>
{
    public void Configure(EntityTypeBuilder<LoginActivity> builder)
    {
        builder.ToTable("login_activities");
        builder.HasKey(la => la.Id);
        builder.Property(la => la.Id).UseIdentityColumn();
        builder.Property(la => la.IpAddress).IsRequired().HasMaxLength(45);
        builder.Property(la => la.UserAgent).HasMaxLength(512);
        builder.Property(la => la.FailureReason).HasMaxLength(256);
        builder.Property(la => la.CreatedAt).HasColumnType("timestamptz");
        builder.Property(la => la.UpdatedAt).HasColumnType("timestamptz");
        builder.HasIndex(la => la.UserId);

        builder.HasOne(la => la.User)
            .WithMany(u => u.LoginActivities)
            .HasForeignKey(la => la.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
