// Karekod (çeklerde Data Matrix, ayrıca QR) okuma: kamera ya da fotoğraf/galeri. Okuma tarayıcıda yapılır, görüntü hiçbir yere gönderilmez.
// ZXing (~350 KB) yalnızca ilk okutmada yüklenir.
let kutuphane;
let okuyucu;

function yukle() {
    kutuphane ??= new Promise((tamam, hata) => {
        if (window.ZXing) {
            tamam(window.ZXing);
            return;
        }

        const betik = document.createElement('script');
        betik.src = new URL('./zxing/zxing-library-0.23.0.min.js', import.meta.url).href;
        betik.onload = () => tamam(window.ZXing);
        betik.onerror = () => {
            kutuphane = undefined;
            hata(new Error('karekod kütüphanesi yüklenemedi'));
        };
        document.head.appendChild(betik);
    });
    return kutuphane;
}

function yeniOkuyucu(Z) {
    const ipuclari = new Map();
    ipuclari.set(Z.DecodeHintType.POSSIBLE_FORMATS, [Z.BarcodeFormat.DATA_MATRIX, Z.BarcodeFormat.QR_CODE]);
    ipuclari.set(Z.DecodeHintType.TRY_HARDER, true);
    return new Z.BrowserMultiFormatReader(ipuclari, 250);
}

// Kamera yalnızca güvenli bağlamda (https ya da localhost) açılır.
export function kameraDurumu() {
    if (!window.isSecureContext) {
        return 'https';
    }

    return navigator.mediaDevices?.getUserMedia ? null : 'desteklenmiyor';
}

// Telefonda arka kamera tercih edilir; okununca kamera kapanır ve metin .NET tarafına bildirilir.
export async function kameraBaslat(video, dotnet) {
    const durum = kameraDurumu();
    if (durum) {
        return durum;
    }

    const Z = await yukle();
    kameraDurdur();
    okuyucu = yeniOkuyucu(Z);
    try {
        await okuyucu.decodeFromConstraints(
            { audio: false, video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 }, height: { ideal: 1080 } } },
            video,
            sonuc => {
                if (sonuc) {
                    const metin = sonuc.getText();
                    kameraDurdur();
                    dotnet.invokeMethodAsync('Okundu', metin);
                }
            });
        return null;
    } catch (e) {
        kameraDurdur();
        if (e?.name === 'NotAllowedError' || e?.name === 'SecurityError') {
            return 'izin';
        }

        return e?.name === 'NotFoundError' || e?.name === 'OverconstrainedError' || e?.name === 'NotReadableError' ? 'kamera_yok' : 'hata';
    }
}

export function kameraDurdur() {
    try {
        okuyucu?.reset();
    } catch {
        // Kamera zaten kapalı.
    }

    okuyucu = undefined;
}

function resimYukle(adres) {
    return new Promise((tamam, hata) => {
        const resim = new Image();
        resim.onload = () => tamam(resim);
        resim.onerror = hata;
        resim.src = adres;
    });
}

// Görüntünün (sx, sy, en, boy) bölümünü en uzun kenarı enBuyuk olacak şekilde PNG adresine çevirir.
function bolum(resim, sx, sy, en, boy, enBuyuk) {
    const oran = Math.min(1, enBuyuk / Math.max(en, boy));
    const tuval = document.createElement('canvas');
    tuval.width = Math.round(en * oran);
    tuval.height = Math.round(boy * oran);
    tuval.getContext('2d').drawImage(resim, sx, sy, en, boy, 0, 0, tuval.width, tuval.height);
    return tuval.toDataURL('image/png');
}

// Denenecek görüntüler: önce tamamı (iki boyutta), sonra örtüşen kare pencereler (büyükten küçüğe).
// ZXing'in Data Matrix bulucusu aramaya görüntünün ortasından başlar; tüm çekin fotoğrafında köşedeki karekod ancak onu ortalayan
// bir pencerede bulunur.
function* adaylar(resim) {
    const en = resim.naturalWidth ?? resim.width;
    const boy = resim.naturalHeight ?? resim.height;
    yield bolum(resim, 0, 0, en, boy, 2000);
    yield bolum(resim, 0, 0, en, boy, 1000);
    for (const oran of [0.5, 0.35, 0.25]) {
        const kenar = Math.round(oran * Math.min(en, boy));
        const adim = Math.round(kenar / 2);
        for (let cy = kenar / 2; cy <= boy - kenar / 2 + adim / 2; cy += adim) {
            for (let cx = kenar / 2; cx <= en - kenar / 2 + adim / 2; cx += adim) {
                const x0 = Math.min(Math.max(0, Math.round(cx - kenar / 2)), en - kenar);
                const y0 = Math.min(Math.max(0, Math.round(cy - kenar / 2)), boy - kenar);
                yield bolum(resim, x0, y0, kenar, kenar, 600);
            }
        }
    }
}

// Seçilen fotoğraftan okur. Dönüş: metin, okunamadıysa '' , dosya seçilmediyse null.
export async function dosyadanOku(girdi) {
    const dosya = girdi.files?.[0];
    if (!dosya) {
        return null;
    }

    const Z = await yukle();
    const adres = URL.createObjectURL(dosya);
    try {
        const resim = await resimYukle(adres);
        const okuyucu = yeniOkuyucu(Z);
        for (const aday of adaylar(resim)) {
            try {
                return (await okuyucu.decodeFromImageUrl(aday)).getText();
            } catch {
                // Bu görüntüde bulunamadı; sıradakini dene.
            }
        }

        return '';
    } finally {
        URL.revokeObjectURL(adres);
        girdi.value = '';
    }
}

// Fatura PDF'indeki karekod: ilk sayfalar yüksek çözünürlükte tuvale çizilip okunur. pdf.js yalnız PDF seçilince yüklenir, belge sunucuya gitmez.
async function pdfOku(dosya) {
    const pdfjs = await import(new URL('./pdfjs/pdf.min.mjs', import.meta.url).href);
    pdfjs.GlobalWorkerOptions.workerSrc = new URL('./pdfjs/pdf.worker.min.mjs', import.meta.url).href;
    const belge = await pdfjs.getDocument({ data: await dosya.arrayBuffer(), isEvalSupported: false }).promise;
    const Z = await yukle();
    const okuyucu = yeniOkuyucu(Z);
    try {
        for (let no = 1; no <= Math.min(belge.numPages, 3); no++) {
            const sayfa = await belge.getPage(no);
            const gorunum = sayfa.getViewport({ scale: 3 });
            const tuval = document.createElement('canvas');
            tuval.width = gorunum.width;
            tuval.height = gorunum.height;
            await sayfa.render({ canvasContext: tuval.getContext('2d'), viewport: gorunum }).promise;
            for (const adres of adaylar(tuval)) {
                try {
                    return (await okuyucu.decodeFromImageUrl(adres)).getText();
                } catch {
                    // Bu pencerede karekod yok; sıradaki denenir.
                }
            }
        }

        return null;
    } finally {
        await belge.destroy();
    }
}

// Fatura belgesi: PDF ya da fotoğraf.
export async function belgeKarekodu(girdi) {
    const dosya = girdi?.files?.[0];
    if (!dosya) {
        return null;
    }

    return dosya.type === 'application/pdf' || dosya.name.toLowerCase().endsWith('.pdf') ? await pdfOku(dosya) : await dosyadanOku(girdi);
}
