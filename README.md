# İklim Muhafızları (Geçici İsim)

> Afetlere müdahale et, şehrini güçlendir, iklim direncini artır.

**Oyun Tasarım Dokümanı (GDD) — Taslak**

Bu doküman, oyunun hedeflenen tasarımını ve hackathon için planlanan MVP kapsamını tanımlar.

## 1. Oyun Kimliği ve Özeti

| Özellik | Tasarım |
| --- | --- |
| Tür | 2.5D aksiyon, strateji ve kaynak yönetimi |
| Tema | İklim dirençli şehirler |
| Platform | PC: Windows ve WebGL |
| Perspektif | İzometrik, ortografik kamera |

Oyuncu, izometrik bir şehirde kurtarma ekibini yöneterek yangın ve taşkınlara anında müdahale eder. Başarılı müdahalelerden kazanılan **Karbon Puanları (CP)** ile güneş paneli ve taşkın bariyeri gibi kalıcı altyapılar kurulur. Bu yatırımlar şehrin afetlere karşı direncini artırır.

## 2. Temel Oyun Döngüsü

1. **Afet bildirimi:** Şehrin rastgele noktalarında dinamik olarak yangın veya sel alarmları tetiklenir.
2. **Müdahale:** Oyuncu, itfaiye ve kurtarma araçlarını olay yerine yönlendirerek afeti büyümeden kontrol altına almaya çalışır.
3. **Ödül:** Kurtarılan her yapı ve başarıyla tamamlanan her görev sonucunda Karbon Puanı kazanılır.
4. **İklim direnci yatırımı:** Kazanılan puanlar, riskli bölgelere su kanalları ve yenilenebilir enerji panelleri gibi dayanıklı altyapılar kurmak için harcanır. Bu yapılar, ilgili bölgede yeniden afet çıkma olasılığını kalıcı olarak düşürür.

## 3. Oynanış Mekanikleri

### Karakter ve Navigasyon

Tıklama (point-and-click) tabanlı kontrol kullanılır. Unity NavMesh sistemi, karakterlerin ve itfaiye araçlarının engellerin çevresinden dolaşarak hedeflerine ulaşmasını sağlar.

### Dinamik Yayılım

Ateş ve su seviyesi zamanla büyür. Müdahale geciktiğinde afet, çevredeki diğer grid hücrelerine sıçrar. Oyuncunun olayları önceliklendirmesi ve hızlı müdahale etmesi gerekir.

### Hızlı Yükseltmeler

Serbest inşaat yerine, mevcut binalara tıklanarak önceden tanımlanmış yükseltmeler anında uygulanır. Taşkın bariyeri ve yalıtım, bu yükseltmelere örnektir.

## 4. Görsel ve İşitsel Tasarım

### Kamera ve Perspektif

X ekseninde **30°**, Y ekseninde **45°** açılı ortografik kamera kullanılır. Üç boyutlu objelerle klasik 2.5D izometrik görünüm hedeflenir.

### Sanat Tarzı

Kenney 3D City Kit varlıkları temel alınır. Universal Render Pipeline (URP) üzerinde cel shading (toon shader) ve outline (dış hat) efektleri uygulanarak çizgi roman estetiği oluşturulması planlanır.

### Ses Tasarımı

Ana lobide ve güvenli anlarda vokalsiz, **21 saniyelik temiz bir döngüye** sahip altyapı müzikleri çalar. Afet anlarında tempolu ses efektleri (SFX) devreye girer.

## 5. Teknik Mimari ve Geliştirme Süreci

### Teknoloji Altyapısı

| Bileşen | Teknoloji |
| --- | --- |
| Oyun motoru | Unity 6000.3.18f1 |
| Programlama dili | C# |
| Render pipeline | Universal Render Pipeline (URP) |
| Navigasyon | Unity AI Navigation / NavMesh |
| Kamera | Cinemachine |
| Girdi | Unity Input System |
| Paylaşılan sistemler | VectorViolet |

### Sistem Mimarisi

Afet bildirimleri, arayüz güncellemeleri ve oyun içi durum değişiklikleri **Event Bus** mimarisiyle yönetilir. Su fışkırtma, ateş partikülleri ve duman efektleri için **Object Pooling** kullanılır.

### Sürüm Kontrolü

Kaynak kodu için kebab-case adlandırma standartlarıyla yapılandırılmış bir GitHub deposu hedeflenir. Üç boyutlu modeller ve sesler gibi binary varlıklar için Plastic SCM entegrasyonu tercih edilir.

### CI/CD

GitHub Actions ile otomatik build süreçlerinin kurulması planlanır. Böylece ekip, demo sürümünü geliştirme boyunca düzenli olarak test edebilir.

## 6. Hackathon Demo Kapsamı (MVP)

Demo, değerlendirme kriterlerindeki **Oynanabilirlik** ve **Teknik İşlevsellik** puanlarını en üst düzeye çıkarmak için küçük ve tamamlanabilir bir kapsamla hazırlanacaktır.

| Alan | MVP Kapsamı |
| --- | --- |
| Harita | Tek bir küçük mahalle |
| Afet | Yalnızca yangın |
| Bina yükseltmeleri | İki tür yükseltme |

Şehir ölçeğindeki harita ve sel/taşkın mekanikleri, daha geniş oyun tasarımının parçasıdır; hackathon demosunun kapsamına dahil değildir.

### Sunum ve Teslim

**Son teslim: 1 Kasım Pazar, saat 23.59.**

Hackathon takvimi gereği oyun sunumları, belirtilen şablona uygun hazırlanarak **WhatsApp üzerinden** teslim edilmelidir.
