using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.KasaBanka.Domain;

internal static class KasaBankaHatalari
{
    public static readonly Hata HesapBulunamadi = Hata.Bulunamadi("hesap_bulunamadi", "Kasa/banka hesabı bulunamadı.");
    public static readonly Hata TeminatBulunamadi = Hata.Bulunamadi("teminat_bulunamadi", "Teminat kaydı bulunamadı.");
    public static readonly Hata OtelemeTarihiGecersiz = Hata.Dogrulama("oteleme_tarihi", "Öteleme için yeni vade, mevcut vadeden sonra olmalıdır.");
    public static readonly Hata SubeBulunamadi = Hata.Bulunamadi("sube_bulunamadi", "Şube bulunamadı.");
    public static readonly Hata SubeKullaniliyor = Hata.IsKurali("sube_kullaniliyor", "Bu şube bir hesaba bağlı; önce hesaplardan çıkarın.");
    public static readonly Hata IslemBulunamadi = Hata.Bulunamadi("islem_bulunamadi", "İşlem bulunamadı.");
    public static readonly Hata KategoriBulunamadi = Hata.Bulunamadi("kategori_bulunamadi", "Gelir/gider kategorisi bulunamadı.");
    public static readonly Hata CariBulunamadi = Hata.Bulunamadi("cari_bulunamadi", "Cari hesap bulunamadı.");
    public static readonly Hata KodKullaniliyor = Hata.Cakisma("kod_kullaniliyor", "Bu hesap kodu başka bir hesapta kullanılıyor.");
    public static readonly Hata KategoriAdiKullaniliyor = Hata.Cakisma("kategori_adi_kullaniliyor", "Bu isimde bir kategori zaten var.");
    public static readonly Hata PasifHesap = Hata.IsKurali("pasif_hesap", "Pasif hesaba işlem yapılamaz.");
    public static readonly Hata KrediBulunamadi = Hata.Bulunamadi("kredi_bulunamadi", "Kredi bulunamadı.");
    public static readonly Hata KrediBankaHesabiGerekli = Hata.IsKurali("kredi_banka_hesabi", "Kredi bir banka hesabına kullandırılır; banka hesabı seçin.");
    public static readonly Hata KrediIptal = Hata.IsKurali("kredi_iptal", "İptal edilmiş kredide işlem yapılamaz.");
    public static readonly Hata TaksitBulunamadi = Hata.Bulunamadi("taksit_bulunamadi", "Taksit bulunamadı.");
    public static readonly Hata TaksitOdenmis = Hata.IsKurali("taksit_odenmis", "Bu taksit zaten ödenmiş.");
    public static readonly Hata TaksitOdenmemis = Hata.IsKurali("taksit_odenmemis", "Bu taksit ödenmemiş.");
    public static readonly Hata TaksitTarihi = Hata.Dogrulama("taksit_tarihi", "Taksit ödeme tarihi kullandırım tarihinden önce olamaz.");
    public static readonly Hata TaksitHesapDovizi = Hata.IsKurali("taksit_hesap_dovizi", "Taksit, kredinin dövizindeki bir hesaptan ödenmelidir.");
    public static readonly Hata OdenmisKrediIptalEdilemez = Hata.IsKurali("odenmis_kredi_iptal", "Taksiti ödenmiş kredi iptal edilemez; önce ödemeleri geri alın.");
    public static readonly Hata PasifCari = Hata.IsKurali("pasif_cari", "Pasif cariye işlem yapılamaz.");
    public static readonly Hata HareketliHesapSilinemez = Hata.IsKurali("hareketli_hesap_silinemez", "Hareketi olan hesap silinemez; pasifleştirebilirsiniz.");
    public static readonly Hata HareketliHesapDegistirilemez = Hata.IsKurali("hareketli_hesap", "Hareketi olan hesabın türü ve para birimi değiştirilemez.");
    public static readonly Hata KullanilanKategoriSilinemez = Hata.IsKurali("kategori_kullaniliyor", "Bu kategoride kayıt var; pasifleştirebilirsiniz.");
    public static readonly Hata TutarPozitifOlmali = Hata.Dogrulama("tutar_gecersiz", "Tutar sıfırdan büyük olmalıdır.");
    public static readonly Hata KurGerekli = Hata.Dogrulama("kur_gerekli", "Bu tarih için TCMB kuru bulunamadı; kuru elle girin.");
    public static readonly Hata AyniHesap = Hata.Dogrulama("ayni_hesap", "Kaynak ve hedef hesap farklı olmalıdır.");
    public static readonly Hata FarkliDoviz = Hata.IsKurali("farkli_doviz", "Hesabın dövizi carinin dövizinden farklı. Cari kartında 'Farklı dövizde tahsilat/ödeme' açılmadıkça işlem yapılamaz.");
    public static readonly Hata AcilisZatenVar = Hata.IsKurali("acilis_zaten_var", "Bu hesabın açılış bakiyesi zaten girilmiş; önce mevcut açılışı iptal edin.");
    public static readonly Hata BicimGecersiz = Hata.Dogrulama("bicim_gecersiz", "'Ödendi' kaydında hesap, 'Tahakkuk' kaydında cari seçilmelidir.");
    public static readonly Hata KategoriTuruUyusmuyor = Hata.Dogrulama("kategori_turu", "Kategori türü (gelir/gider) işlem türüyle uyuşmuyor.");
    public static readonly Hata KaynakBelgedenIptal = Hata.IsKurali("kaynak_belgeden_iptal", "Bu işlem bir belgeye (fatura, çek/senet) bağlı; belgesinden iptal edilmelidir.");
    public static readonly Hata ZatenIptalEdilmis = Hata.IsKurali("zaten_iptal", "Bu işlem zaten iptal edilmiş.");
    public static readonly Hata CariLisansiYok = Hata.Yetki("cari_lisansi_yok", "Cari işlemi için Cari modülü lisansı gerekir.");
    public static readonly Hata KasayaEkstreYok = Hata.IsKurali("kasaya_ekstre_yok", "Ekstre yalnızca banka, kredi kartı ve POS hesaplarına aktarılır.");
    public static readonly Hata DosyaOkunamadi = Hata.Dogrulama("dosya_okunamadi", "Dosya okunamadı. CSV, Excel (.xlsx/.xls) veya MT940 biçiminde bir ekstre yükleyin.");
    public static readonly Hata DosyaCokBuyuk = Hata.Dogrulama("dosya_cok_buyuk", "Ekstre dosyası en fazla 5 MB olabilir.");
    public static Hata EkstreCokBuyuk(string mesaj) => Hata.Dogrulama("ekstre_cok_buyuk", mesaj);
    public static readonly Hata EkstreBos = Hata.Dogrulama("ekstre_bos", "Dosyada aktarılacak hareket bulunamadı.");
    public static readonly Hata AktarimBulunamadi = Hata.Bulunamadi("aktarim_bulunamadi", "Ekstre aktarımı veya satırı bulunamadı.");
    public static readonly Hata EslemeDegistirilemez = Hata.IsKurali("esleme_degistirilemez", "Satırları aktarılmış ya da MT940 biçimindeki aktarımın kolon eşlemesi değiştirilemez.");
    public static readonly Hata EslemeSatirVermedi = Hata.Dogrulama("esleme_satir_vermedi", "Bu kolon eşlemesiyle hiçbir satır okunamadı; tarih ve tutar kolonlarını kontrol edin.");
    public static readonly Hata SatirDegistirilemez = Hata.IsKurali("satir_degistirilemez", "Aktarılmış ya da atlanmış satır değiştirilemez.");
    public static readonly Hata AktarimHazirDegil = Hata.IsKurali("aktarim_hazir_degil", "Önce kolon eşlemesini tamamlayın; tamamlanmış aktarım tekrar aktarılamaz.");
    public static readonly Hata AktarimSilinemez = Hata.IsKurali("aktarim_silinemez", "Satırı aktarılmış aktarım silinemez.");
    public static readonly Hata SatirCariGerekli = Hata.Dogrulama("satir_cari_gerekli", "Tahsilat/ödeme için cari seçilmeli.");
    public static readonly Hata SatirKategoriGerekli = Hata.Dogrulama("satir_kategori_gerekli", "Gelir/gider için kategori seçilmeli.");
    public static readonly Hata SatirKarsiHesapGerekli = Hata.Dogrulama("satir_karsi_hesap_gerekli", "Virman için karşı hesap seçilmeli.");
    public static readonly Hata SatirYonuUyusmuyor = Hata.Dogrulama("satir_yonu", "İşlem türü satırın yönüyle uyuşmuyor (giriş: tahsilat/gelir/virman, çıkış: ödeme/gider/virman).");
    public static readonly Hata SatirVirmanDovizi = Hata.IsKurali("satir_virman_dovizi", "Ekstreden virman yalnızca aynı dövizdeki hesaplar arasında yapılır; farklı dövizli virmanı elle girin.");
    public static readonly Hata KuralBulunamadi = Hata.Bulunamadi("kural_bulunamadi", "Kural bulunamadı.");

    public static readonly Hata EvrakBulunamadi = Hata.Bulunamadi("evrak_bulunamadi", "Çek/senet bulunamadı.");
    public static readonly Hata BordroBulunamadi = Hata.Bulunamadi("bordro_bulunamadi", "Bordro bulunamadı.");
    public static readonly Hata BorcluBulunamadi = Hata.Bulunamadi("borclu_bulunamadi", "Keşideci/borçlu bulunamadı.");
    public static readonly Hata EvrakYok = Hata.Dogrulama("evrak_yok", "Bordroda en az bir evrak olmalıdır.");
    public static readonly Hata EvrakIslemiGecersiz = Hata.IsKurali("evrak_islemi_gecersiz", "Bu işlem evrakın şu anki durumunda yapılamaz.");
    public static readonly Hata EvrakTarihiGeride = Hata.IsKurali("evrak_tarihi_geride", "İşlem tarihi evrakın son hareket tarihinden önce olamaz.");
    public static readonly Hata EvrakTahsilTutari = Hata.Dogrulama("evrak_tahsil_tutari", "Tahsil tutarı sıfırdan büyük ve evrakın kalan tutarından küçük ya da eşit olmalıdır.");
    public static readonly Hata EvrakKismiTahsilTekEvrak = Hata.Dogrulama("evrak_kismi_tahsil", "Kısmi tahsil tutarı yalnızca tek evrakla girilebilir.");
    public static readonly Hata EvrakGeriAlinamaz = Hata.IsKurali("evrak_geri_alinamaz", "Evrakın geri alınacak hareketi yok.");
    public static readonly Hata EvrakHesapGerekli = Hata.Dogrulama("evrak_hesap_gerekli", "Bu işlem için hesap seçilmelidir.");
    public static readonly Hata EvrakHesapTuru = Hata.IsKurali("evrak_hesap_turu", "Evrak tahsile/teminata yalnızca banka hesabına verilir; tahsil ve ödeme kasa ya da banka hesabından yapılır.");
    public static readonly Hata EvrakHesapDovizi = Hata.IsKurali("evrak_hesap_dovizi", "Hesabın dövizi evrakın dövizinden farklı.");
    public static readonly Hata EvrakCariGerekli = Hata.Dogrulama("evrak_cari_gerekli", "Ciro edilecek cari seçilmelidir.");
    public static readonly Hata EvrakAyniCariyeCiro = Hata.IsKurali("evrak_ayni_cari", "Evrak, getiren cariye ciro edilemez; iade işlemini kullanın.");
    public static readonly Hata VerilenCekHesabi = Hata.Dogrulama("verilen_cek_hesabi", "Verilen çek, firmanın bir banka hesabına bağlı olmalıdır.");
    public static readonly Hata KimlikNoGerekli = Hata.Dogrulama("kimlik_no_gerekli", "Keşideci/borçlu VKN ya da TCKN girilmelidir (Ayarlar'dan isteğe bağlı yapılabilir).");
    public static readonly Hata KimlikNoGecersiz = Hata.Dogrulama("kimlik_no_gecersiz", "VKN/TCKN geçersiz; kontrol hanelerini kontrol edin.");
    public static readonly Hata BorcluAdiGerekli = Hata.Dogrulama("borclu_adi_gerekli", "Keşideci/borçlu adı girilmelidir.");
    public static readonly Hata KimlikNoKullaniliyor = Hata.Cakisma("kimlik_no_kullaniliyor", "Bu VKN/TCKN ile kayıtlı bir keşideci/borçlu var.");
    public static readonly Hata BorcluEvrakliSilinemez = Hata.IsKurali("borclu_evrakli", "Evrakı olan keşideci/borçlu silinemez.");
    public static readonly Hata EvrakZatenKayitli = Hata.Cakisma("evrak_zaten_kayitli", "Bu seri numaralı çek aynı hesaptan zaten kayıtlı.");
    public static readonly Hata SeriGerekli = Hata.Dogrulama("seri_gerekli", "Seri no girin ya da karne seçin.");
    public static readonly Hata KarneBulunamadi = Hata.Bulunamadi("karne_bulunamadi", "Çek karnesi / senet defteri bulunamadı.");
    public static readonly Hata KarneSeriGecersiz = Hata.Dogrulama("karne_seri_gecersiz", "Başlangıç seri no sayı ile bitmelidir (ör. ABC 1234501).");
    public static readonly Hata KarneCakisiyor = Hata.Cakisma("karne_cakisiyor", "Bu seri aralığı aynı hesabın başka bir karnesiyle çakışıyor.");
    public static readonly Hata KarneUyumsuz = Hata.IsKurali("karne_uyumsuz", "Seçilen karne bu evrakın türüne ya da banka hesabına ait değil.");
    public static readonly Hata KarneKapali = Hata.IsKurali("karne_kapali", "Karne kapatılmış; yeniden açmadan kullanılamaz.");
    public static readonly Hata KarneDoldu = Hata.IsKurali("karne_doldu", "Karnede boş yaprak kalmadı.");
    public static readonly Hata KarneSeriDisinda = Hata.IsKurali("karne_seri_disinda", "Seri no karnenin aralığında değil.");
    public static readonly Hata KarneYapragiKullanildi = Hata.Cakisma("karne_yapragi_kullanildi", "Bu yaprak daha önce kullanıldı.");
    public static readonly Hata KarneYapragiIptal = Hata.IsKurali("karne_yapragi_iptal", "Bu yaprak iptal edilmiş (yırtık/kayıp); kullanılamaz.");
    public static readonly Hata KarneYapragiIptalDegil = Hata.Bulunamadi("karne_yapragi_iptal_degil", "Bu yaprak iptal edilmemiş.");
    public static readonly Hata KarneKullanildi = Hata.IsKurali("karne_kullanildi", "Yaprağı kullanılmış karne silinemez; kapatabilirsiniz.");

    public static Hata RiskOnayiGerekli(string mesaj) => Hata.IsKurali("risk_onayi_gerekli", $"{mesaj} Yine de kabul etmek için onaylayın.");

    public static Hata RiskliBorcluEngellendi(string mesaj) => Hata.IsKurali("riskli_borclu", $"{mesaj} Firma ayarları gereği riskli keşideci/borçludan evrak kabul edilmiyor.");

    public static Hata EvrakIslemiGecersizDetay(string portfoyNo, string durum) =>
        Hata.IsKurali("evrak_islemi_gecersiz", $"{portfoyNo} numaralı evrak \"{durum}\" durumunda; bu işlem yapılamaz.");
}
