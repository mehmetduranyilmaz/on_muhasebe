// Tema tercihi ve hareket ayarı. Tarayıcı depolaması kapalıysa sessizce sistem ayarına düşülür.
window.omTema = {
    oku: () => { try { return localStorage.getItem("om-tema"); } catch { return null; } },
    yaz: deger => { try { localStorage.setItem("om-tema", deger); } catch { } }
};
window.omHareketAzaltildiMi = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;

// PWA: yeni sürüm arka planda iner, uygulama bir sonraki açılışta sessizce güncellenir.
if ("serviceWorker" in navigator) navigator.serviceWorker.register("service-worker.js", { updateViaCache: "none" });
