// Yayınlanan index.html'deki satır içi scriptlerin (derlemede üretilen importmap) SHA-256 özetleriyle içerik güvenlik
// politikasını (CSP) üretip Caddy parçası olarak yazar. Web imajı derlenirken çalışır: dotnet run deploy/csp.cs -- <index.html> <çıktı>
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

var html = File.ReadAllText(args[0]);
var ozetler = Regex.Matches(html, @"<script(?![^>]*\bsrc=)[^>]*>(.*?)</script>", RegexOptions.Singleline)
    .Select(m => $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(m.Groups[1].Value)))}'")
    .Distinct();

string[] politika =
[
    "default-src 'self'",
    $"script-src 'self' 'wasm-unsafe-eval' {string.Join(' ', ozetler)}",
    // MudBlazor bileşenleri style özniteliği kullanır.
    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
    "font-src 'self' https://fonts.gstatic.com",
    // Karekod: galeriden seçilen fotoğraf blob: adresiyle okunur.
    "img-src 'self' data: blob:",
    "connect-src 'self'",
    "worker-src 'self'",
    "manifest-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
    "upgrade-insecure-requests"
];

File.WriteAllText(args[1], $"header Content-Security-Policy \"{string.Join("; ", politika)}\"\n");
Console.WriteLine(File.ReadAllText(args[1]));
