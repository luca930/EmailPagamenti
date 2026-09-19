using EmailPagamenti.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmailPagamenti.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MessageId).HasMaxLength(998).IsRequired();
        builder.Property(e => e.MessageIdHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Folder).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Sender).HasMaxLength(320).IsRequired();
        builder.Property(e => e.SenderDisplayName).HasMaxLength(255);
        builder.Property(e => e.Subject).HasMaxLength(998).IsRequired();
        builder.Property(e => e.Merchant).HasMaxLength(255);
        builder.Property(e => e.Category).HasMaxLength(64).IsRequired();
        builder.Property(e => e.CardLast4).HasMaxLength(4).IsFixedLength();
        builder.Property(e => e.Reference).HasMaxLength(128);
        builder.Property(e => e.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(e => e.MatchedRule).HasMaxLength(255);
        builder.Property(e => e.FailureReason).HasMaxLength(1024);
        builder.Property(e => e.BodySnippet).HasMaxLength(8000);

        // Gli importi sono interi in centesimi: esatti, ordinabili e sommabili ovunque.
        builder.Property(e => e.AmountCents);
        builder.Property(e => e.BalanceAfterCents);

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Direction).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(32);

        // Deduplica: e' questo indice a garantire che una notifica non entri due volte.
        builder.HasIndex(e => e.MessageIdHash).IsUnique();

        // Indici pensati sulle domande che fa davvero la dashboard: quanto ho speso questo
        // mese, in che categoria, presso chi, e cosa devo ancora rivedere.
        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => new { e.Category, e.OccurredAt });
        builder.HasIndex(e => new { e.Merchant, e.OccurredAt });
        builder.HasIndex(e => new { e.Status, e.OccurredAt });

        builder.Ignore(e => e.Total);
        builder.Ignore(e => e.SignedAmount);
        builder.Ignore(e => e.Amount);
        builder.Ignore(e => e.BalanceAfter);

        builder.Metadata
            .FindNavigation(nameof(Transaction.Attachments))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.Attachments)
            .WithOne()
            .HasForeignKey(a => a.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
