using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OnMuhasebe.Shared.Infrastructure.Persistence;

public static class MetinArama
{
    // ILIKE '%metin%' aramaları B-tree index kullanamaz; pg_trgm GIN index'i 3+ karakterli aramada tam tarama yerine index'ten okur.
    // Yalnızca satır güvenliği (RLS) olmayan tablolarda işe yarar: RLS altında PostgreSQL, LEAKPROOF olmayan ILIKE'ı güvenlik
    // koşulundan önce index'te kullanmaz. Modül tablolarında arama firma index'iyle daraltılmış tarama olarak kalır.
    public static IndexBuilder<T> MetinAramaIndeksi<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, object?>> alan) where T : class
    {
        builder.Metadata.Model.GetOrAddPostgresExtension(null, "pg_trgm", null);
        return builder.HasIndex(alan).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}
