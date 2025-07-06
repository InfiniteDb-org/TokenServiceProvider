using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TokenService.Api.Models;

namespace TokenService.Api.Infrastructure.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Token).HasMaxLength(64).IsRequired();
        builder.HasIndex(r => r.Token).IsUnique();
        builder.Property(r => r.UserId).IsRequired();
    }
}