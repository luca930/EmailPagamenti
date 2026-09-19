using EmailPagamenti.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailPagamenti.Infrastructure.Persistence;

public sealed class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options)
        : base(options)
    {
    }

    public DbSet<PaymentEmail> PaymentEmails => Set<PaymentEmail>();

    public DbSet<PaymentAttachment> Attachments => Set<PaymentAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
