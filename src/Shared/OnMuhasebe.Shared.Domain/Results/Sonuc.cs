using System.Diagnostics.CodeAnalysis;

namespace OnMuhasebe.Shared.Domain.Results;

public class Sonuc
{
    private static readonly Sonuc _basariliSonuc = new(null);

    protected Sonuc(Hata? hata) => Hata = hata;

    public Hata? Hata { get; }

    [MemberNotNullWhen(false, nameof(Hata))]
    public bool BasariliMi => Hata is null;

    public static Sonuc Basarili() => _basariliSonuc;

    public static Sonuc Basarisiz(Hata hata) => new(hata);

    public static implicit operator Sonuc(Hata hata) => new(hata);
}

public sealed class Sonuc<T> : Sonuc
{
    private readonly T? _deger;

    private Sonuc(T deger) : base(null) => _deger = deger;

    private Sonuc(Hata hata) : base(hata) { }

    public T Deger => BasariliMi
        ? _deger!
        : throw new InvalidOperationException($"Başarısız sonucun değeri okunamaz: {Hata.Kod}");

    public static implicit operator Sonuc<T>(T deger) => new(deger);

    public static implicit operator Sonuc<T>(Hata hata) => new(hata);
}
