// Tema tercihi ve hareket ayarı. Tarayıcı depolaması kapalıysa sessizce sistem ayarına düşülür.
window.omTema = {
    oku: () => { try { return localStorage.getItem("om-tema"); } catch { return null; } },
    yaz: deger => { try { localStorage.setItem("om-tema", deger); } catch { } }
};
// Üst çubukta seçili işyeri (firma başına); depolama kapalıysa her açılışta "Tüm işyerleri" ile başlanır.
window.omIsyeri = {
    oku: firma => { try { return localStorage.getItem("om-isyeri:" + firma); } catch { return null; } },
    yaz: (firma, deger) => { try { deger ? localStorage.setItem("om-isyeri:" + firma, deger) : localStorage.removeItem("om-isyeri:" + firma); } catch { } }
};
// API'den gelen dosyayı tarayıcıya indirir.
window.omDosya = {
    indir: (ad, tur, baytlar) => {
        const adres = URL.createObjectURL(new Blob([baytlar], { type: tur }));
        const a = document.createElement("a");
        a.href = adres;
        a.download = ad;
        document.body.append(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(adres), 10000);
    }
};
window.omHareketAzaltildiMi = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;

// PWA: yeni sürüm arka planda iner, uygulama bir sonraki açılışta sessizce güncellenir.
if ("serviceWorker" in navigator) navigator.serviceWorker.register("service-worker.js", { updateViaCache: "none" });

// E-posta bağlantılarının kodu adresin # kısmında gelir. Açık sayfaya yeni bir bağlantı yapıştırılırsa yalnızca # değişir ve
// Blazor sayfayı yenilemez; sayfa yeniden yüklenip kod baştan okunur.
window.addEventListener("hashchange", () => { if (location.hash.includes("kod=")) location.reload(); });
