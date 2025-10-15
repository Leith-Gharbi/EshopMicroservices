


using Ordering.Domain.ValueObjects;

namespace Ordering.Infrastructure.Data.Configurations
{
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.HasKey(c => c.Id); //Indique que Id est la clé primaire dans la base.
            builder.Property(c => c.Id).HasConversion(
                   customerId => customerId.Value,   // Quand on enregistre dans la base (Quand EF sauvegarde, il prend la propriété Value du CustomerId (un Guid))
                   dbId => CustomerId.Of(dbId));     // Quand on relit depuis la base (Quand EF lit depuis la base, il recrée un CustomerId à partir du Guid.)

            builder.Property(c => c.Name).HasMaxLength(100).IsRequired();

            builder.Property(c => c.Email).HasMaxLength(255);

            builder.HasIndex(c => c.Email).IsUnique();
        }
    }
}
