using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace PaymentService
{
    public class PaymentContext : DbContext
    {
        public PaymentContext(DbContextOptions<PaymentContext> options) : base(options)
        {
        }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<InboxMessage> InboxMessages { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // У одного пользователя всегда не более одного счета
            modelBuilder.Entity<Account>()
                .HasIndex(a => a.UserId)
                .IsUnique();

            // MessageId должен быть уникальным для идемпотентности
            modelBuilder.Entity<InboxMessage>()
                .HasIndex(i => i.MessageId)
                .IsUnique();

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(o => o.ProcessedAt);
        }
    }
}
