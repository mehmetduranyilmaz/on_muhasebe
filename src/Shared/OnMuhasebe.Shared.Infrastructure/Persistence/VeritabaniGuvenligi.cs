using System.Text.RegularExpressions;
using Npgsql;

namespace OnMuhasebe.Shared.Infrastructure.Persistence;

// PostgreSQL satır düzeyi güvenliği (RLS): uygulama, tablo sahibi olmayan ve RLS'yi atlayamayan bir rolle bağlanır; iş verisi
// tablolarında (firma_id kolonu olan) yalnızca oturuma yazılan firmanın satırları görünür ve yazılabilir. Uygulama kodundaki firma
// filtresinin ikinci savunma hattıdır: bir sorguda filtre unutulsa bile veritabanı başka firmanın satırını vermez.
// Yönetim şeması (kullanıcı, firma üyeliği, lisans) giriş gibi işlemlerde tasarım gereği firmalar arası okunduğundan kapsam dışıdır.
// Kurulum idempotenttir; her migration'dan sonra tablo sahibi bağlantısıyla çalıştırılır (yeni tablolar da kapsanır).
public static partial class VeritabaniGuvenligi
{
    public const string FirmaAyari = "app.firma_id";
    private static readonly string[] _kapsamDisiSemalar = ["yonetim"];

    public static async Task UygulaAsync(string yoneticiBaglantisi, string uygulamaBaglantisi, CancellationToken cancellationToken)
    {
        var uygulama = new NpgsqlConnectionStringBuilder(uygulamaBaglantisi);
        var yonetici = new NpgsqlConnectionStringBuilder(yoneticiBaglantisi);
        if (string.Equals(uygulama.Username, yonetici.Username, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Uygulama bağlantısı tablo sahibiyle aynı kullanıcıyı kullanıyor; satır güvenliği (RLS) etkisiz kalır.");
        }

        if (uygulama.Username is not { } rol || !RolDeseni().IsMatch(rol))
        {
            throw new InvalidOperationException("Uygulama veritabanı kullanıcı adı geçersiz.");
        }

        await using var baglanti = new NpgsqlConnection(yoneticiBaglantisi);
        await baglanti.OpenAsync(cancellationToken);
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = KurulumSql(rol, uygulama.Password ?? "");
        await komut.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string KurulumSql(string rol, string sifre)
    {
        var sifreLiteral = "'" + sifre.Replace("'", "''", StringComparison.Ordinal) + "'";
        var kapsamDisi = string.Join(", ", _kapsamDisiSemalar.Select(s => $"'{s}'"));
        return $$"""
            DO $$
            DECLARE s record; t record;
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{{rol}}') THEN
                    CREATE ROLE {{rol}} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS PASSWORD {{sifreLiteral}};
                END IF;

                FOR s IN SELECT nspname FROM pg_namespace
                         WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema' LOOP
                    EXECUTE format('GRANT USAGE ON SCHEMA %I TO {{rol}}', s.nspname);
                    EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO {{rol}}', s.nspname);
                    EXECUTE format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO {{rol}}', s.nspname);
                END LOOP;

                FOR t IN SELECT c.table_schema, c.table_name
                         FROM information_schema.columns c
                         JOIN information_schema.tables tb ON tb.table_schema = c.table_schema AND tb.table_name = c.table_name
                         WHERE c.column_name = 'firma_id' AND tb.table_type = 'BASE TABLE'
                           AND c.table_schema NOT IN ({{kapsamDisi}}) AND c.table_schema NOT LIKE 'pg\_%' LOOP
                    EXECUTE format('ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY', t.table_schema, t.table_name);
                    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = t.table_schema AND tablename = t.table_name
                                   AND policyname = 'firma_izolasyonu') THEN
                        EXECUTE format(
                            'CREATE POLICY firma_izolasyonu ON %I.%I USING (firma_id = NULLIF(current_setting(''{{FirmaAyari}}'', true), '''')::uuid) '
                            || 'WITH CHECK (firma_id = NULLIF(current_setting(''{{FirmaAyari}}'', true), '''')::uuid)',
                            t.table_schema, t.table_name);
                    END IF;
                END LOOP;
            END $$;
            """;
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex RolDeseni();
}
