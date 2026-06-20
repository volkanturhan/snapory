# Snapory

**[English](README.md) | Türkçe**

Hafif bir Windows ekran görüntüsü ve işaretleme aracı.

Snapory sistem tepsisinde sessizce durur. Bir kısayola basarsın, ekran donup
kararır, istediğin alanı sürükleyerek seçersin — sonra küçük bir editörde açılır;
panoya kopyalamadan ya da PNG kaydetmeden önce ok, kutu, vurgu ve yazı
ekleyebilirsin.

<p align="center">
  <img src="docs/screenshot.png" alt="Snapory işaretleme editörü" width="520" />
</p>

## Özellikler

- **Bölge yakala** — global kısayol (`Ctrl + Shift + S`) ekranı karartır ve tam
  istediğin alanı sürükleyerek seçtirir.
- **Piksel hassasiyetinde** — masaüstünün donmuş anlık görüntüsünden yakalar;
  yüksek DPI ve çoklu monitör kurulumlarında bile doğru.
- **İşaretle** — ok, kutu, vurgu ve yazı araçları, farklı renk seçenekleriyle.
- **Geri al** — işaretlemelerinde geri adım (`Ctrl + Z`).
- **Kopyala ya da kaydet** — sonucu panoya kopyala (`Ctrl + C`) ya da PNG kaydet
  (`Ctrl + S`); tam çözünürlükte tek katmana indirgenir.
- **Windows ile başla** — isteğe bağlı, tepsi menüsünden aç/kapa.
- **İngilizce & Türkçe** — arayüz dilini tepsiden değiştir.
- **Tasarımı gereği gizli** — her şey senin makinende kalır, hiçbir şey yüklenmez.

## Çalıştır

Snapory henüz hazır bir indirme olarak yayınlanmadı, bu yüzden şimdilik kaynaktan
çalıştırıyorsun. Windows'ta [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
(sadece runtime değil, SDK) kurulu olmalı.

```bash
git clone https://github.com/volkanturhan/Snapory.git
cd Snapory
dotnet run --project Snapory/Snapory.csproj
```

Snapory sessizce sistem tepsisinde başlar — **hiçbir pencere açılmaz**. Bu
normaldir; yakalamak için kısayola bas (ya da tepsiden **Yeni ekran
görüntüsü**'nü kullan).

## Nasıl kullanılır

1. Snapory'i başlat — sessizce sistem tepsisine yerleşir.
2. **`Ctrl + Shift + S`**'ye bas (ya da tepsiden **Yeni ekran görüntüsü**). Ekran
   kararır; istediğin alanı **sürükleyerek** seç. **Esc** iptal eder.
3. Seçim editörde açılır. Bir araç (**Ok**, **Kutu**, **Vurgu**, **Yazı**) ve renk
   seç, görüntünün üzerine çiz. **Geri al** / **Ctrl + Z** son işareti kaldırır.
4. **Kopyala** (`Ctrl + C`) sonucu panoya koyar; **Kaydet** (`Ctrl + S`) PNG yazar.

Tepsi ikonuna sağ tık: **Yeni ekran görüntüsü**, **Windows ile başlat**, dil ve
**Çıkış**.

## Paylaşılabilir exe oluştur

SDK olmadan birine verebileceğin bağımsız bir `.exe` mi istiyorsun? Kendin
derle — çıktı repoya dahil edilmez:

```bash
# dist/ içine derler (self-contained Snapory.exe + lite sürüm)
pwsh tools/publish.ps1
```

## Teknoloji

- C# / WPF, .NET 8 (Windows)
- Üçüncü parti bağımlılık yok

## Lisans

MIT — bkz. [LICENSE](LICENSE).
