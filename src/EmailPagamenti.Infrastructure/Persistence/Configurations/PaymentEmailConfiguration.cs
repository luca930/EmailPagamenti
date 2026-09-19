using EmailPagamenti.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmailPagamenti.Infrastructure.Persistence.Configurations;

internal sealed class PaymentEmailConfiguration : IEntityTypeConfiguration<PaymentEmail>
{
    public void Configure(EntityTypeBuilder<PaymentEmail> builder)
    {
        builder.ToTable("payment_emails");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MessageId).HasMaxLength(998).IsRequired();
        builder.Property(e => e.MessageIdHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Folder).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Sender).HasMaxLength(320).IsRequired();
        builder.Property(e => e.SenderDisplayName).HasMaxLength(255);
        builder.Property(e => e.Subject).HasMaxLength(998).IsRequired();
        builder.Property(e => e.Merchant).HasMaxLength(255);
        builder.Property(e => e.Reference).HasMaxLength(128);
        builder.Property(e => e.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(e => e.MatchedRule).HasMaxLength(255);
        builder.Property(e => e.FailureReason).HasMaxLength(1024);
        builder.Property(e => e.BodySnippet).HasMaxLength(8000);

        // Precisione fissata a mano: il default di alcuni provider tronca i centesimi.
        builder.Property(e => e.Amount).HasPrecision(18, 2);

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Direction).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(32);

        // Deduplica: e' questo indice a garantire che una email non entri due volte.
        builder.HasIndex(e => e.MessageIdHash).IsUnique();

        // Indici pensati sulle ricerche reali: per periodo, per esercente, per stato.
        builder.HasIndex(e => e.SentAt);
        builder.HasIndex(e => new { e.Merchant, e.SentAt });
        builder.HasIndex(e => new { e.Status, e.SentAt });

        builder.Ignore(e => e.Total);

        builder.Metadata
            .FindNavigation(nameof(PaymentEmail.Attachments))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.Attachments)
            .WithOne()
            .HasForeignKey(a => a.PaymentEmailId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
