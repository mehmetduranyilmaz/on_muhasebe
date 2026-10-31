namespace OnMuhasebe.Web.Bilesenler;

// Belge/hareket satırının lot, SKT ve seri girdisi. Kartın izleme ayarı (lot/seri, SKT) karttan atanır.
// Ortak katmanda olduğu için Stok sözleşmelerine bağlı değildir; kurallar sunucuda (stok hareketi) uygulanır.
public sealed class IzlemeModeli
{
    public bool LotMu { get; private set; }
    public bool SeriMi { get; private set; }
    public bool SktTakibi { get; private set; }

    public string? LotNo { get; set; }
    public DateTime? Skt { get; set; }
    public string? SerilerMetni { get; set; }

    public bool VarMi => LotMu || SeriMi;

    public List<string> Seriler => string.IsNullOrWhiteSpace(SerilerMetni)
        ? []
        : SerilerMetni.Split(['\n', '\r', ',', ';', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).ToList();

    public void Ayarla(string izleme, bool sktTakibi)
    {
        (LotMu, SeriMi) = (izleme == "Lot", izleme == "Seri");
        SktTakibi = VarMi && sktTakibi;
    }

    public void SeriEkle(string seri)
    {
        if (!Seriler.Contains(seri))
        {
            SerilerMetni = string.Join('\n', Seriler.Append(seri));
        }
    }

    public string? LotIstegi => VarMi && !string.IsNullOrWhiteSpace(LotNo) ? LotNo.Trim() : null;
    public DateOnly? SktIstegi => SktTakibi && Skt is { } s ? DateOnly.FromDateTime(s) : null;
    public IReadOnlyList<string>? SeriIstegi => SeriMi && Seriler.Count > 0 ? Seriler : null;

    public void Yukle(string? lot, DateOnly? skt, IReadOnlyList<string>? seriler)
    {
        LotNo = lot;
        Skt = skt?.ToDateTime(TimeOnly.MinValue);
        SerilerMetni = seriler is { Count: > 0 } ? string.Join('\n', seriler) : null;
    }
}
