// Tema tercihi ve hareket ayarı. Tarayıcı depolaması kapalıysa sessizce sistem ayarına düşülür.
window.omTema = {
    oku: () => { try { return localStorage.getItem("om-tema"); } catch { return null; } },
    yaz: deger => { try { localStorage.setItem("om-tema", deger); } catch { } }
};
window.omHareketAzaltildiMi = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;

// PWA: yeni sürüm arka planda iner, uygulama bir sonraki açılışta sessizce güncellenir.
if ("serviceWorker" in navigator) navigator.serviceWorker.register("service-worker.js", { updateViaCache: "none" });

// E-posta bağlantılarının kodu adresin # kısmında gelir. Açık sayfaya yeni bir bağlantı yapıştırılırsa yalnızca # değişir ve
// Blazor sayfayı yenilemez; sayfa yeniden yüklenip kod baştan okunur.
window.addEventListener("hashchange", () => { if (location.hash.includes("kod=")) location.reload(); });
