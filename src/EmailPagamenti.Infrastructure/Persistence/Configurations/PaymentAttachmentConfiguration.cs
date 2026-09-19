using EmailPagamenti.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmailPagamenti.Infrastructure.Persistence.Configurations;

internal sealed class PaymentAttachmentConfiguration : IEntityTypeConfiguration<PaymentAttachment>
{
    public void Configure(EntityTypeBuilder<PaymentAttachment> builder)
    {
        builder.ToTable("payment_attachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(255).IsRequired();

        builder.HasIndex(a => a.PaymentEmailId);
    }
}
