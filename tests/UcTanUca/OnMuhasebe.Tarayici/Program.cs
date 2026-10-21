// Kullanım: dotnet run -- <test-adı ...|hepsi> [secenekler]. Ad verilmezse testler listelenir.
using OnMuhasebe.Tarayici;

var testler = typeof(TarayiciTesti).Assembly.GetTypes()
    .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(TarayiciTesti)))
    .Select(t => (TarayiciTesti)Activator.CreateInstance(t)!)
    .OrderBy(t => t.Ad, StringComparer.Ordinal)
    .ToList();

var adlar = args.Where(a => !a.StartsWith('-')).ToList();
if (adlar.Count == 0)
{
    Console.WriteLine("Testler: " + string.Join(", ", testler.Select(t => t.Ad)) + " | hepsi");
    return 2;
}

var secilen = adlar.Contains("hepsi") ? testler : testler.Where(t => adlar.Contains(t.Ad)).ToList();
var bilinmeyen = adlar.Except(testler.Select(t => t.Ad)).Except(["hepsi"]).ToList();
if (bilinmeyen.Count > 0)
{
    Console.Error.WriteLine("Bilinmeyen test: " + string.Join(", ", bilinmeyen));
    return 2;
}

var (toplamGecen, toplamKalan) = (0, 0);
foreach (var test in secilen)
{
    Console.WriteLine($"== {test.Ad}");
    var (gecen, kalan) = await test.KosAsync(args);
    Console.WriteLine($"SONUC: {gecen} gecti, {kalan} kaldi\n");
    (toplamGecen, toplamKalan) = (toplamGecen + gecen, toplamKalan + kalan);
}

if (secilen.Count > 1)
{
    Console.WriteLine($"TOPLAM: {toplamGecen} gecti, {toplamKalan} kaldi");
}

return toplamKalan == 0 ? 0 : 1;
