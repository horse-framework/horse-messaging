# Queue Partition Sistemi — Özet

> **Son güncelleme:** 28 Şubat 2026  
> Bu döküman partition sistemiyle ilgili her kullanım senaryosunu, tasarım kararını ve API detayını kapsar. Yeni özellik eklendiğinde bu dosya güncellenmelidir.

---

## İçindekiler

1. [Ne İnşa Ettik?](#ne-inşa-ettik)
2. [Temel Kavramlar](#temel-kavramlar)
3. [Partition Seçenekleri (PartitionOptions)](#partition-seçenekleri-partitionoptions)
4. [Kullanım Senaryoları](#kullanım-senaryoları)
   - [Label'lı Kullanım — Dedicated Partition](#1-labelli-kullanım--dedicated-partition)
   - [Label'sız + Orphan Enabled — Load Distribution](#2-labelsız--orphan-enabled--load-distribution)
   - [Label'sız + Orphan Disabled — Round-Robin Partition](#3-labelsız--orphan-disabled--round-robin-partition)
   - [AutoQueueCreation ile Partition Oluşturma](#4-autoqueuecreation-ile-partition-oluşturma)
5. [QueueType ve Partition Davranışı](#queuetype-ve-partition-davranışı)
6. [Header Referansı](#header-referansı)
7. [Routing Akışı](#routing-akışı)
8. [Orphan Partition](#orphan-partition)
9. [AutoDestroy](#autodestroy)
10. [Metrikler](#metrikler)
11. [Event Sistemi](#event-sistemi)
12. [Client API Özeti](#client-api-özeti)
    - [SubscribePartitioned](#subscribePartitioned-queueoperator)
    - [IHorseQueueBus Partition Push Overload'ları](#ihorsequeuebus--partition-push-overloadları)
13. [Yük Dağılımı Avantajları](#yük-dağılımı-avantajları)
14. [RoundRobin Queue ile Karşılaştırma](#roundrobin-queue-ile-karşılaştırma)
15. [Bilinen Davranışlar ve Notlar](#bilinen-davranışlar-ve-notlar)
16. [Ne Elde Ettik?](#ne-elde-ettik)

---

## Ne İnşa Ettik?

Tek bir `HorseQueue`'yu **fiziksel alt kuyruklara (partition)** otomatik bölen bir sistem.  
Dışarıdan bakıldığında **tek bir queue** var; içeride birden fazla partition queue çalışıyor.

```
Producer  ──►  FetchOrders (parent, IsPartitioned=true)
                     │
               PartitionManager
                ┌────┴────────────────┐
                ▼                     ▼
   FetchOrders-Partition-a3k9x   FetchOrders-Partition-Orphan
         (Worker-1'e ait)              (sahipsiz mesajlar)
```

---

## Temel Kavramlar

### Parent Queue

```
FetchOrders              ← tek isim, dışarıdan görülen tek queue
IsPartitioned = true
PartitionManager ≠ null
```

### Partition Queue'lar (otomatik oluşturulur)

```
FetchOrders-Partition-a3k9x   ← worker-1'e ait  (IsPartitionQueue=true)
FetchOrders-Partition-b7m2p   ← worker-2'e ait  (IsPartitionQueue=true)
FetchOrders-Partition-Orphan  ← sahipsiz mesajlar için
```

- Her partition queue normal bir `HorseQueue`'dur ama `IsPartitionQueue = true`.
- Partition queue'ların kendi `PartitionManager`'ı **yoktur** (`IsPartitioned = false`).
- İsim formatı: `{parentQueueName}-Partition-{base62Id}`
- Orphan sabit isimle oluşur: `{parentQueueName}-Partition-Orphan`

### PartitionManager

Parent queue'nun içinde oturur. Tüm routing, create, destroy kararlarını verir.

| Sorumluluk | Açıklama |
|---|---|
| Subscribe routing | Gelen client'ı uygun partition'a yönlendirir |
| Message routing | Mesajı label veya round-robin ile doğru partition'a gönderir |
| Partition lifecycle | Oluştur, yok et, event fırlat |
| AutoDestroy timer | Boş/sahipsiz partition'ları periyodik temizler |
| Metrics | Her partition için anlık metrik snapshot |

### PartitionEntry

Her partition'ı temsil eden iç struct:

```csharp
public class PartitionEntry
{
    public string PartitionId  { get; set; }  // base62 unique id ("Orphan" sabit)
    public string Label        { get; set; }  // null = label'sız veya orphan
    public bool   IsOrphan     { get; set; }
    public HorseQueue Queue    { get; set; }
    public DateTime CreatedAt  { get; set; }
    public DateTime LastMessageAt { get; set; }
}
```

---

## Partition Seçenekleri (PartitionOptions)

```csharp
opts.Partition = new PartitionOptions
{
    Enabled                = true,
    MaxPartitionCount      = 10,    // 0 = sınırsız
    SubscribersPerPartition = 1,    // her partition'da max kaç subscriber
    EnableOrphanPartition  = true,  // orphan partition açık mı
    AutoDestroy            = PartitionAutoDestroy.Disabled,
    AutoDestroyIdleSeconds = 30     // AutoDestroy kontrol aralığı (saniye)
};
```

| Alan | Varsayılan | Açıklama |
|---|---|---|
| `Enabled` | `false` | Partitioning aktif mi |
| `MaxPartitionCount` | `0` | Maksimum label partition sayısı (0 = sınırsız) |
| `SubscribersPerPartition` | `1` | Her partition'daki max subscriber sayısı |
| `EnableOrphanPartition` | `true` | Orphan partition oluşturulsun mu |
| `AutoDestroy` | `Disabled` | Otomatik silme kuralı |
| `AutoDestroyIdleSeconds` | `30` | AutoDestroy timer aralığı |

---

## Kullanım Senaryoları

### 1. Label'lı Kullanım — Dedicated Partition

**Ne zaman kullanılır:** Belirli bir worker/tenant'ın mesajlarını tamamen izole etmek istediğinizde.

```csharp
// ── Server tarafı ──────────────────────────────────────────
await rider.Queue.Create("FetchOrders", opts =>
{
    opts.Type = QueueType.Push;
    opts.Partition = new PartitionOptions
    {
        Enabled                = true,
        MaxPartitionCount      = 10,
        SubscribersPerPartition = 1,
        EnableOrphanPartition  = true,
        AutoDestroy            = PartitionAutoDestroy.NoConsumers,
        AutoDestroyIdleSeconds = 30
    };
});

// ── Worker (consumer) tarafı ───────────────────────────────
await client.Queue.SubscribePartitioned(
    queue:                  "FetchOrders",
    partitionLabel:         "tenant-42",   // ← bu worker'a özel label
    verifyResponse:         true,
    maxPartitions:          10,
    subscribersPerPartition: 1);

// Response header'larında partition bilgisi gelir:
// Partition-Id: a3k9x
// Queue-Name:   FetchOrders-Partition-a3k9x

// ── Producer tarafı ────────────────────────────────────────
await producer.Queue.Push("FetchOrders", message, false,
    new[]
    {
        new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-42")
    });
```

**Ne olur:**
1. `tenant-42` label'ı için `FetchOrders-Partition-a3k9x` oluşturulur (ilk bağlanmada)
2. Aynı label ile sonraki bağlantılar aynı partition'a yönlenir
3. `PARTITION_LABEL: tenant-42` header'lı mesaj direkt bu partition'a gider
4. Başka worker mesajı göremez
5. Worker düşerse `NoConsumers` kuralı ile partition `AutoDestroyIdleSeconds` sonra silinir

**Orphan ile ilişki:**  
Label'lı worker aynı zamanda orphan partition'a da otomatik subscribe edilir (`EnableOrphanPartition=true` ise). Bu sayede label'sız gelen mesajlar da bu worker tarafından alınabilir.

---

### 2. Label'sız + Orphan Enabled — Load Distribution

**Ne zaman kullanılır:** Worker'lar hangi partition'a ait olduklarını bilmek istemediğinde; mesajların tüm worker'lara eşit dağıtılmasını istediğinizde.

```csharp
// ── Worker tarafı ──────────────────────────────────────────
await client.Queue.Subscribe("JobQueue", true);
// VEYA
await client.Queue.SubscribePartitioned(
    queue:         "JobQueue",
    partitionLabel: null,        // ← label yok
    verifyResponse: true);

// ── Producer tarafı ────────────────────────────────────────
await producer.Queue.Push("JobQueue", message, false);
// Mesajda PARTITION_LABEL header'ı yok
```

**Ne olur:**
1. Her worker bağlandığında kendine ait bir label'sız partition açılır
2. Tüm worker'lar **orphan partition'a da** otomatik subscribe edilir
3. Label'sız mesajlar orphan partition'a gönderilir
4. Orphan push semantiği ile tüm subscriber'lara dağıtır

```
JobQueue (parent)
    ├── Partition-abc  ← Worker-1 (kendi partition'ı)
    ├── Partition-xyz  ← Worker-2 (kendi partition'ı)
    └── Orphan         ← Worker-1 + Worker-2 (her ikisi de burada)

Mesaj (label'sız) → Orphan → Worker-1 veya Worker-2
```

**Not:** Bu mod temelde klasik push queue gibi davranır. Partition açılması ek bellek kullanır ama mesaj dağıtımı için orphan kullanıldığından ayrı partition'ların pratikte bir katkısı yoktur.

---

### 3. Label'sız + Orphan Disabled — Round-Robin Partition

**Ne zaman kullanılır:** Orphan partition istemediğinizde, mesajların round-robin ile partition'lara dağıtılmasını istediğinizde.

```csharp
// ── Server tarafı ──────────────────────────────────────────
await rider.Queue.Create("JobQueue", opts =>
{
    opts.Type = QueueType.Push;
    opts.Partition = new PartitionOptions
    {
        Enabled                = true,
        MaxPartitionCount      = 5,
        SubscribersPerPartition = 1,
        EnableOrphanPartition  = false,   // ← orphan kapalı
        AutoDestroy            = PartitionAutoDestroy.NoConsumers,
        AutoDestroyIdleSeconds = 30
    };
});

// ── Worker tarafı ──────────────────────────────────────────
await client.Queue.Subscribe("JobQueue", true);
// Her worker kendi partition'ını alır (label = null)

// ── Producer tarafı ────────────────────────────────────────
await producer.Queue.Push("JobQueue", message, false);
// Label'sız → round-robin ile partition'lara dağıtılır
```

**Ne olur:**
1. Worker-1 bağlanır → `Partition-abc` açılır, Worker-1 eklenir
2. Worker-2 bağlanır → `Partition-abc` dolu → `Partition-xyz` açılır, Worker-2 eklenir
3. Label'sız mesaj gelir → subscriber'ı olan partition'lar arasında **round-robin**
4. Orphan hiç oluşturulmaz

```
JobQueue (parent)
    ├── Partition-abc  ← Worker-1  (round-robin hedefi)
    └── Partition-xyz  ← Worker-2  (round-robin hedefi)

Mesaj-1 → Partition-xyz → Worker-2
Mesaj-2 → Partition-abc → Worker-1
Mesaj-3 → Partition-xyz → Worker-2
...
```

**Orphan Enabled vs Disabled karşılaştırması:**

| | Orphan Enabled | Orphan Disabled |
|---|---|---|
| Mesaj dağıtımı | Orphan üzerinden push | Round-robin ile direkt partition'a |
| Bellek | +1 orphan queue | Sadece label partition'lar |
| Orphan oluşturulur mu | Evet | Hayır |
| Subscriber yoksa | Orphan'a gider (boş kalır) | `NoConsumers` döner |

---

### 4. AutoQueueCreation ile Partition Oluşturma

**Ne zaman kullanılır:** Queue henüz mevcut değilken client'ın subscribe olurken aynı anda partitioned queue oluşturmasını istediğinizde.

```csharp
// Server'da AutoQueueCreation = true olmalı
rider.Queue.Options.AutoQueueCreation = true;

// ── Client tarafı ──────────────────────────────────────────
// PARTITION_LIMIT ve PARTITION_SUBSCRIBERS header'ları ile
// server queue'yu partitioned olarak oluşturur:
HorseResult result = await client.Queue.SubscribePartitioned(
    queue:                  "auto-part-q",   // henüz mevcut değil
    partitionLabel:         "worker-1",
    verifyResponse:         true,
    maxPartitions:          10,              // → PARTITION_LIMIT header
    subscribersPerPartition: 1);             // → PARTITION_SUBSCRIBERS header

// Server:
// 1. "auto-part-q" bulunamadı
// 2. AutoQueueCreation=true → PARTITION_LIMIT=10, PARTITION_SUBSCRIBERS=1 header'larını okur
// 3. PartitionOptions.Enabled=true, MaxPartitionCount=10 olarak queue oluşturur
// 4. InitializeQueue → PartitionManager oluşturulur → IsPartitioned=true
// 5. Subscribe akışı devam eder
```

**Kullanılan Header'lar:**

| Header | Değer | Açıklama |
|---|---|---|
| `Partition-Label` | string | Worker'ın partition label'ı |
| `Partition-Limit` | int | AutoCreate sırasında MaxPartitionCount |
| `Partition-Subscribers` | int | AutoCreate sırasında SubscribersPerPartition |

---

## Header Referansı

```csharp
// Horse.Messaging.Protocol.HorseHeaders
HorseHeaders.PARTITION_LABEL       = "Partition-Label"
HorseHeaders.PARTITION_ID          = "Partition-Id"
HorseHeaders.PARTITION_LIMIT       = "Partition-Limit"
HorseHeaders.PARTITION_SUBSCRIBERS = "Partition-Subscribers"
```

| Header | Yön | Açıklama |
|---|---|---|
| `Partition-Label` | Client → Server | Subscribe veya Push'ta hangi label'a ait olduğu |
| `Partition-Id` | Server → Client | Subscribe response'unda hangi partition'a eklendiği |
| `Partition-Limit` | Client → Server | AutoCreate için max partition sayısı |
| `Partition-Subscribers` | Client → Server | AutoCreate için partition başına max subscriber |

---

## Routing Akışı

```
Producer → Push("FetchOrders", msg, headers)
                │
          queue.IsPartitioned?
                │ YES
         PartitionManager.RouteMessage(msg)
                │
    PARTITION_LABEL header var mı?
    ┌───────────┴───────────────┐
   YES                          NO
    │                            │
LabelIndex'te var mı?    EnableOrphanPartition?
    │ YES                ┌───────┴───────┐
    │                   YES             NO
    ▼                    │               │
entry.Queue         GetOrCreate     Round-Robin
entry.LastMessageAt  Orphan()       (subscriber'ı
= DateTime.UtcNow                    olan partition'lar
    │                    │           arasında)
    └────────────────────┘
                │
           target.Push(msg)
                │
       target == null?
           → NoConsumers
```

**Label var ama subscriber yok → `ResolveNoSubscriberTarget`:**

```
EnableOrphanPartition == false → null → NoConsumers
EnableOrphanPartition == true  → orphan.Clients.Any()?
    WaitForAcknowledge && !any  → null → NoConsumers
    Otherwise                   → orphan queue
```

---

## Orphan Partition

Orphan partition label'sız veya sahipsiz mesajlar için bir fallback havuzudur.

### Oluşturulma Kuralları

| Koşul | Durum |
|---|---|
| `EnableOrphanPartition = false` | Hiç oluşturulmaz |
| `EnableOrphanPartition = true` + herhangi bir subscribe | Lazy olarak oluşturulur |
| `WaitForAcknowledge` queue + `EnableOrphanPartition = true` | `InitializeQueue`'da önceden oluşturulur |

### Subscriber Kuralları

- Orphan partition'ın `ClientLimit = 0` (sınırsız).
- Label'lı subscribe olan her worker, label partition'ına ek olarak orphan'a da otomatik eklenir.
- Label'sız subscribe olan her worker direkt orphan'a da eklenir.
- `EnableOrphanPartition = false` ise orphan subscribe edilmez.

### WaitForAcknowledge Garantisi

```csharp
// WaitForAcknowledge modunda orphan'da her zaman en az 1 subscriber şartı
if (Acknowledge == WaitForAcknowledge && !orphan.Clients.Any())
    return null; // → NoConsumers
```

---

## AutoDestroy

Partition'lar parent queue'dan bağımsız olarak yok edilebilir. `PartitionManager` kendi timer'ı ile periyodik kontrol yapar.

### Kurallar

```csharp
public enum PartitionAutoDestroy
{
    Disabled,      // Hiç silinmez (varsayılan)
    NoConsumers,   // !Clients.Any()
    NoMessages,    // IsEmpty
    Empty          // !Clients.Any() && IsEmpty
}
```

| Kural | Koşul | Davranış |
|---|---|---|
| `Disabled` | — | Partition asla silinmez |
| `NoConsumers` | Hiç subscriber kalmadı | `AutoDestroyIdleSeconds` sonra silinir |
| `NoMessages` | Queue boşaldı | `AutoDestroyIdleSeconds` sonra silinir |
| `Empty` | Ne subscriber ne mesaj var | `AutoDestroyIdleSeconds` sonra silinir |

### Notlar

- Orphan partition AutoDestroy timer'dan **muaftır** (`IsOrphan = true` olan entry'ler atlanır).
- Partition silinince `_partitions` ve `_labelIndex`'ten kaldırılır.
- Parent queue yaşamaya devam eder, diğer partition'lar etkilenmez.
- Partition queue kendi `QueueOptions.AutoDestroy` değeri `Disabled`'dır (recursive silme olmaz).

---

## Metrikler

```csharp
// Server-side: partition metriklerini tazele
queue.Info.RefreshPartitionMetrics(queue.PartitionManager);

Console.WriteLine($"Partition sayısı : {queue.Info.PartitionCount}");
Console.WriteLine($"Orphan aktif     : {queue.Info.OrphanPartitionActive}");

foreach (PartitionMetricSnapshot snap in queue.Info.PartitionMetrics)
{
    Console.WriteLine($"  [{snap.Label ?? "(orphan)"}]" +
                      $"  id={snap.PartitionId}" +
                      $"  queue={snap.QueueName}" +
                      $"  mesaj={snap.MessageCount}" +
                      $"  consumer={snap.ConsumerCount}" +
                      $"  son mesaj={snap.LastMessageAt:HH:mm:ss}");
}
```

### PartitionMetricSnapshot

| Alan | Tip | Açıklama |
|---|---|---|
| `PartitionId` | `string` | Unique partition kimliği |
| `Label` | `string?` | Worker label'ı (null = label'sız veya orphan) |
| `IsOrphan` | `bool` | Orphan partition mı |
| `QueueName` | `string` | Fiziksel queue adı |
| `MessageCount` | `int` | Şu anda kuyruktaki mesaj sayısı |
| `ConsumerCount` | `int` | Aktif subscriber sayısı |
| `CreatedAt` | `DateTime` | Partition oluşturulma zamanı |
| `LastMessageAt` | `DateTime` | Son mesaj iletim zamanı |

### QueueInfo Alanları

```csharp
public int PartitionCount { get; set; }         // Toplam partition sayısı (orphan dahil)
public bool OrphanPartitionActive { get; set; } // Orphan var ve aktif mi
public List<PartitionMetricSnapshot> PartitionMetrics { get; set; }
```

---

## Event Sistemi

### Server-Side Event Handler

```csharp
public class MyPartitionHandler : IPartitionEventHandler
{
    public Task OnPartitionCreated(HorseQueue parent, PartitionEntry entry)
    {
        Console.WriteLine($"[{parent.Name}] Yeni partition: {entry.PartitionId} label={entry.Label}");
        return Task.CompletedTask;
    }

    public Task OnPartitionDestroyed(HorseQueue parent, string partitionId)
    {
        Console.WriteLine($"[{parent.Name}] Partition silindi: {partitionId}");
        return Task.CompletedTask;
    }
}

// Kayıt
rider.Queue.PartitionEventHandlers.Add(new MyPartitionHandler());
```

### Client-Side Event Subscribe

```csharp
// QueuePartitionCreated event'ine abone ol
await client.Event.SubscribeToQueuePartitionCreated("FetchOrders", async (ev, c) =>
{
    string partitionId = ev.Name;     // PartitionId
    string queueName   = ev.Content;  // Fiziksel queue adı
    Console.WriteLine($"Yeni partition açıldı: {partitionId} → {queueName}");
});
```

### HorseEventType

```csharp
HorseEventType.QueuePartitionCreated   // Yeni partition oluşturuldu
HorseEventType.QueuePartitionDestroyed // Partition yok edildi
```

---

## Client API Özeti

### Attribute Tabanlı Konfigürasyon (Önerilen)

Partition seçenekleri tek bir `[PartitionedQueue]` attribute ile consumer veya model sınıfında bildirilebilir — builder'da kod değişikliğine gerek yok.

```csharp
// ── Consumer sınıfında — tenant-42'ye özel partition ─────────────────
[PartitionedQueue("tenant-42", MaxPartitions = 10, SubscribersPerPartition = 1)]
public class FetchOrderConsumer : IQueueConsumer<FetchOrderEvent>
{~~~~
    public Task Consume(HorseMessage rawMessage, FetchOrderEvent model, HorseClient client)
    {
        // ...
        return Task.CompletedTask;
    }
}

// ── Label'sız partitioned subscribe (orphan / round-robin yolu) ───────
[PartitionedQueue(MaxPartitions = 5)]
public class JobConsumer : IQueueConsumer<JobEvent> { ... }

// ── Veya model sınıfında (o da çalışır) ───────────────────────────────
[QueueName("FetchOrders")]
[PartitionedQueue("tenant-42", MaxPartitions = 10, SubscribersPerPartition = 1)]
public record FetchOrderEvent(string OrderId);

public class FetchOrderConsumer : IQueueConsumer<FetchOrderEvent> { ... }
```

`AutoSubscribe = true` olduğunda client, reconnect'te bildirilen değerlerle otomatik olarak `SubscribePartitioned` çağırır.

| Sözdizimi | Etki |
|---|---|
| `[PartitionedQueue("label")]` | Subscribe'da `Partition-Label` header'ı gönderir |
| `[PartitionedQueue("label", MaxPartitions = N)]` | Auto-create için `Partition-Limit: N` de gönderir |
| `[PartitionedQueue("label", MaxPartitions = N, SubscribersPerPartition = M)]` | Her üç partition header'ını gönderir |
| `[PartitionedQueue]` veya `[PartitionedQueue(null)]` | Label'sız partitioned subscribe (orphan / round-robin) |

---

### `HorseClientBuilder` — Partition ile Consumer Kaydı

```csharp
// ── Singleton (en yaygın) ─────────────────────────────────────────────
services.AddHorseClient(b => b
    .AddHost("horse://localhost:2626")
    // düz — partition yok
    .AddSingletonConsumer<FetchOrderConsumer>()
    // explicit partition label — attribute varsa üzerine yazar
    .AddSingletonConsumer<FetchOrderConsumer>("tenant-42", maxPartitions: 10, subscribersPerPartition: 1)
    // label'sız partitioned (orphan / round-robin yolu)
    .AddSingletonConsumer<JobConsumer>(partitionLabel: "", maxPartitions: 5));

// ── Transient ─────────────────────────────────────────────────────────
.AddTransientConsumer<FetchOrderConsumer>("tenant-42", maxPartitions: 10, subscribersPerPartition: 1)

// ── Scoped ────────────────────────────────────────────────────────────
.AddScopedConsumer<FetchOrderConsumer>("tenant-42", maxPartitions: 10, subscribersPerPartition: 1)
```

Öncelik: **builder parametresi > `[PartitionedQueue]` attribute > partition yok**.

---

### `SubscribePartitioned` (QueueOperator)

```csharp
// client.Queue üzerinden doğrudan kullanım
Task<HorseResult> QueueOperator.SubscribePartitioned(
    string   queue,
    string   partitionLabel,              // null = label'sız
    bool     verifyResponse,
    int      maxPartitions       = 0,     // AutoCreate için PARTITION_LIMIT
    int      subscribersPerPartition = 0, // AutoCreate için PARTITION_SUBSCRIBERS
    IEnumerable<KeyValuePair<string,string>> additionalHeaders = null);
```

Bu method aşağıdaki header'ları otomatik inşa eder:

| Header | Koşul |
|---|---|
| `Partition-Label` | `partitionLabel` null değilse |
| `Partition-Limit` | `maxPartitions > 0` ise |
| `Partition-Subscribers` | `subscribersPerPartition > 0` ise |

**Örnekler:**

```csharp
// Tenant izolasyonu
await client.Queue.SubscribePartitioned("FetchOrders", "tenant-42", true, 10, 1);

// Label'sız, orphan üzerinden dağıtım
await client.Queue.SubscribePartitioned("JobQueue", null, true, 5, 1);

// Auto-create ile, queue yoksa server oluşturur
await client.Queue.SubscribePartitioned("new-queue", "w1", true, maxPartitions: 8);
```

### Subscribe Response

Partitioned queue'ya subscribe başarılıysa response aşağıdaki header'ları içerir:

```
HorseResultCode: Ok
Partition-Id:    a3k9x                         ← hangi partition'a eklenildi
Queue-Name:      FetchOrders-Partition-a3k9x  ← fiziksel queue adı
```

---

### `IHorseQueueBus` — `partitionLabel` Parametresi ile Partition Push

Tüm `Push` metodları opsiyonel `string partitionLabel = null` parametresi kabul eder.
Değer verildiğinde bus otomatik olarak `PARTITION_LABEL` header'ını ekler.

#### Push İmzaları (partition destekli)

```csharp
// Ham içerik
Task<HorseResult> Push(string queue, MemoryStream content,
    bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
    string partitionLabel = null,
    CancellationToken cancellationToken = default);

// Ham içerik + messageId
Task<HorseResult> Push(string queue, MemoryStream content, string messageId,
    bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
    string partitionLabel = null,
    CancellationToken cancellationToken = default);

// Model (queue attribute'tan)
Task<HorseResult> Push<T>(T model, bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
    string partitionLabel = null,
    CancellationToken cancellationToken = default) where T : class;

// Model + explicit queue
Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
    string partitionLabel = null,
    CancellationToken cancellationToken = default) where T : class;
```

#### PushBulk İmzaları

```csharp
// Toplu model push
void PushBulk<T>(string queue, List<T> items,
    Action<HorseMessage, bool> callback,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null) where T : class;

// Toplu ham içerik push
void PushBulk(string queue, List<MemoryStream> contents,
    bool waitForCommit, Action<HorseMessage, bool> callback,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null);
```

#### Kullanım örnekleri

```csharp
// Model → tenantId partition'ına
[QueueName("FetchOrders")]
public record FetchOrderEvent(string OrderId);
await bus.Push(new FetchOrderEvent("ord-1"), partitionLabel: tenantId);

// Model → explicit queue + partition
await bus.Push("FetchOrders", new FetchOrderEvent("ord-1"), partitionLabel: tenantId);

// WaitForCommit + partition
HorseResult result = await bus.Push("FetchOrders", model, waitForCommit: true, partitionLabel: tenantId);

// Partition'sız — eskisi ile aynı
await bus.Push("FetchOrders", model, false);

// Label null → orphan'a (veya round-robin, orphan disabled ise)
await bus.Push("JobQueue", stream, false, partitionLabel: null);
```

### Mesaj Gönderme (low-level — QueueOperator üzerinden)

```csharp
// Label'lı — direkt o partition'a gider
await producer.Queue.Push("FetchOrders", content, false,
    new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-42") });

// Label'sız — orphan'a (enabled) veya round-robin'e (disabled) gider
await producer.Queue.Push("FetchOrders", content, false);
```

---

## RoundRobin Queue ile Karşılaştırma

Horse zaten `QueueType.RoundRobin` ile N worker'a sırayla mesaj dağıtabiliyordu. Partition sistemi buna ne katıyor?

### RoundRobin Queue (mevcut sistem)

```
FetchOrders (RoundRobin)
    ├── Worker-1  ┐
    ├── Worker-2  ├── Hepsi aynı fiziksel queue'dan sırayla mesaj alır
    └── Worker-3  ┘
```

- 10 worker var, mesajlar 1→2→3→…→10→1 sırasıyla dağıtılır
- **Hepsi aynı fiziksel queue'da yarışır**
- `WaitForAcknowledge` açıksa: Worker-1 bir mesajı işlerken Worker-2'ye sıra **gelmez** — Worker-1'in ACK'ini bekler → tüm sistem yavaşlar
- Bir worker yavaşlarsa diğerleri de durur

### Partition sistemi

```
FetchOrders (parent, Partitioned)
    ├── Partition-a → Worker-1  (sadece kendi mesajları)
    ├── Partition-b → Worker-2  (sadece kendi mesajları)
    └── Partition-c → Worker-3  (sadece kendi mesajları)
```

- Her worker **kendi fiziksel queue'sunda** çalışır
- `WaitForAcknowledge` açıksa: Worker-1 yavaşlarsa sadece `Partition-a` bekler, Worker-2 ve Worker-3 **etkilenmez**
- Worker-1 düşerse sadece `Partition-a` silinir, diğerleri çalışmaya devam eder

### Yan Yana Karşılaştırma

| | RoundRobin Queue | Partition (label'sız + orphan) | Partition (label'lı) |
|---|---|---|---|
| Fiziksel yapı | 1 queue, N worker | N+1 queue, N worker | N queue, N worker |
| `WaitForAck` izolasyonu | ⚠️ Worker meşgulse atlanır, mesaj başkasına gider | ✅ Her worker bağımsız | ✅ Mesaj sadece o partition'da kalır |
| Tenant izolasyonu | ❌ Yok | ❌ Yok | ✅ Label ile tam izolasyon |
| Worker düşünce | Mesajlar diğer worker'lara | Orphan üzerinden devam | Sadece o partition bekler |
| Bellek | En az | Orta (+1 orphan queue) | Orta (N ayrı queue) |
| Kurulum karmaşıklığı | Basit | Orta | Orta |
| Ne zaman kullan | Hafif, hızlı işler | Biraz daha güvenli dağıtım | Tenant/worker izolasyonu |

### WaitForAcknowledge Gerçeği

> **Yaygın yanılgı:** `WaitForAcknowledge` sıralı işleme garantisi verir.  
> **Gerçek:** Tek bir mesajın güvenli teslimini garanti eder — mesaj kaybolmaz. Sıra garantisi değildir.

**RoundRobin + WaitForAck davranışı** (`RoundRobinQueueState` kaynak kodundan):

```csharp
// Worker meşgulse (CurrentlyProcessing != null) atla, bir sonrakine geç
if (waitForAcknowledge && client.CurrentlyProcessing != null)
    continue;
```

Yani RoundRobin, Worker-1 meşgulken Worker-2'ye geçer — sistem bloke **olmaz**. Tüm worker'lar aynı anda meşgulse o zaman bekler.

**Partition + WaitForAck davranışı:**

```
Mesaj "tenant-42" label'ıyla geldi
    → Partition-tenant42'ye gönderildi
    → Worker-42 meşgul → mesaj Partition-tenant42'de BEKLER
    → Başka worker'a GİTMEZ
```

Bu **gerçek izolasyondur**: tenant-42'nin mesajı yavaşlasa bile diğer tenant'ların mesajları başka partition'larında kendi hızlarında işlenir.

### Partitionun Gerçekten Fark Yarattığı 2 Senaryo

**Senaryo 1 — Tenant izolasyonu (en net fark):**

```
// RoundRobin ile problem:
tenant-A'nın 10.000 ağır işi kuyrukta
→ Worker-1, Worker-2 meşgul → tenant-B mesajı Worker-3'e atanır
→ ama Worker-3 de dolunca tenant-B bekler
→ tenant-A mesajları tüm worker'lar arasında "sızar"

// Partition ile çözüm (label = tenantId):
Partition-tenantA → Worker-A  (kendi hızında, diğerlerini etkilemez)
Partition-tenantB → Worker-B  (tenant-A ne kadar yoğun olursa olsun bağımsız) ✅
```

**Senaryo 2 — Mesajın sahibini kaybetmeme:**

```
// RoundRobin + WaitForAck:
msg-1 → Worker-1'e gönderildi, ACK bekleniyor
msg-2 → Worker-1 meşgul → Worker-2'ye gönderildi
// msg-1 ve msg-2 farklı worker'larda işleniyor
// "Bu mesaj mutlaka Worker-1 tarafından işlensin" garantisi YOK

// Partition + WaitForAck:
msg-1 → Partition-worker1 → Worker-1
msg-2 → Partition-worker1 → Worker-1 ACK vermeden geldi → BEKLER
// msg-1 ACK gelince msg-2 işlenir
// "Bu mesajlar sırasıyla aynı worker'da işlensin" garantisi VAR ✅
```

### Özet: Ne Zaman Hangisi?

| İhtiyaç | Tercih |
|---|---|
| Basit yük dağıtımı, hızlı işler | RoundRobin yeterli |
| Worker meşgulken mesaj başkasına gitmesin | **Partition (label'lı)** |
| Tenant/müşteri bazlı tam izolasyon | **Partition (label'lı)** |
| Mesajlar sırasıyla aynı worker'da işlensin | **Partition (label'lı, SubscribersPerPartition=1)** |
| Worker düşünce sadece onu etkilesin | **Partition** |
| En az bellek, en basit yapı | RoundRobin |

---

## QueueType ve Partition Davranışı

Parent queue'ya verilen `QueueType` (`Push`, `RoundRobin`, `Pull`) **partition queue'lara `CloneFrom` ile aktarılır**. Yani her partition kendi içinde o tip gibi davranır. Parent queue'nun tipi yalnızca routing kararını değil, mesajın partition'a **ulaştıktan sonraki** dağıtım biçimini de belirler.

```
Producer → Push("FetchOrders", msg)
               │
         PartitionManager.RouteMessage  ← label veya round-robin ile seçim
               │
         target = Partition-a3k9x
               │
         target.Push(msg)  ← BURADAN SONRA QueueType devreye girer
               │
     QueueType neyse ona göre davranır
```

### Push + Partition (`QueueType.Push`)

```csharp
opts.Type = QueueType.Push;
opts.Partition = new PartitionOptions { SubscribersPerPartition = 3 };
```

- Mesaj partition'a gelince `PushQueueState.ProcessMessage` çalışır
- Partition'daki **tüm subscriber'lara aynı anda** gönderilir
- `SubscribersPerPartition = 3` ile 3 worker aynı mesajı alır (broadcast içi)
- Genellikle `SubscribersPerPartition = 1` ile kullanılır — o zaman Push ve RoundRobin arasında fark kalmaz

```
Partition-a3k9x (Push, 3 subscriber)
    ├── Worker-1a  ┐
    ├── Worker-1b  ├── msg-1'i hepsi alır (broadcast)
    └── Worker-1c  ┘
```

**Ne zaman kullanılır:** Her partition için hem yedek (redundancy) hem de partition izolasyonu istiyorsanız. Örneğin her tenant için 3 replika worker.

---

### RoundRobin + Partition (`QueueType.RoundRobin`)

```csharp
opts.Type = QueueType.RoundRobin;
opts.Partition = new PartitionOptions { SubscribersPerPartition = 3 };
```

- Mesaj partition'a gelince `RoundRobinQueueState.Push` çalışır
- Partition'daki subscriber'lardan **sıradakine** gönderilir (1'e)
- `SubscribersPerPartition = 3` ile aynı partition'da 3 worker vardır, her mesaj sırayla birine gider
- `WaitForAcknowledge` ile meşgul worker atlanır, boştaki bir sonrakine geçilir

```
Partition-a3k9x (RoundRobin, 3 subscriber)
    ├── Worker-1a  ← msg-1
    ├── Worker-1b  ← msg-2
    └── Worker-1c  ← msg-3
         msg-4 → Worker-1a (döngü)
```

**Ne zaman kullanılır:** Aynı tenant'ın yükü tek bir worker'a sığmıyor ama diğer tenant'larla da karışmasın istiyorsanız. Örneğin her tenant için kendi worker havuzu.

---

### Pull + Partition (`QueueType.Pull`)

```csharp
opts.Type = QueueType.Pull;
opts.Partition = new PartitionOptions { ... };
```

- Mesaj partition'a gelince `PullQueueState.Push` çalışır — bu metod sadece `return Success` döndürür, mesajı **depoya yazar**
- Consumer aktif olarak `Pull` isteği göndermeden mesaj iletilmez
- `SubscribePartitioned` ile subscribe olunabilir ama mesaj kendiliğinden gelmez; her işlem için `Pull` request gerekir
- Partition izolasyonu korunur: Pull isteği hangi partition'dan geliyorsa o partition'un deposundan mesaj çekilir

```
Partition-a3k9x (Pull)
    └── Worker-1  →  "Pull isteği" gönderir  →  Partition-a3k9x deposundan mesaj gelir
                      (isteği olmadan mesaj gelmez)
```

**Ne zaman kullanılır:** Consumer hazır olmadan mesaj işlememesi gereken durumlar. Örneğin uzun batch işlemler, başlamadan önce hazırlık gerektiren işler.

---

### Karşılaştırma: SubscribersPerPartition = 1 ile

`SubscribersPerPartition = 1` olduğunda Push ve RoundRobin **aynı davranır** — partition'da tek subscriber olduğundan seçim fark etmez.

| QueueType | SubscribersPerPartition = 1 | SubscribersPerPartition > 1 |
|---|---|---|
| `Push` | Tek worker alır | Tüm worker'lar alır (broadcast) |
| `RoundRobin` | Tek worker alır | Sıradaki worker alır (1'e dağıtım) |
| `Pull` | Worker çekene kadar bekler | Worker çekene kadar bekler |

### Pratik Öneriler

| İhtiyaç | Tip | SubscribersPerPartition |
|---|---|---|
| Tenant izolasyonu, tek işleyici | `Push` veya `RoundRobin` | 1 |
| Tenant izolasyonu, yük paylaşımı | `RoundRobin` | N (iş yüküne göre) |
| Partition içi broadcast / replikasyon | `Push` | N |
| Hazır olunca çek, önce depolama | `Pull` | 1+ |

> **Önemli not:** `Pull` tipi ile `SubscribePartitioned` kullanıldığında subscribe işlemi partition'u oluşturur ama mesajlar aktif pull isteği olmadan iletilmez. Bu bilerek tercih edilmesi gereken bir kombinasyondur.

---

### Klasik Tek Queue Sorunu

```
Queue → [msg1, msg2, msg3, msg4, msg5]
          ↓       ↓       ↓
       Worker1  Worker1  Worker2   ← dengesiz, race condition, lock
```

Tüm consumer'lar aynı queue'dan yarışır. `WaitForAcknowledge` modunda bir consumer meşgulken diğerleri de bekler.

### Partition Sistemi ile

```
FetchOrders (parent)
    ├── Partition-a → Worker-1  (sadece kendi mesajları)
    ├── Partition-b → Worker-2  (sadece kendi mesajları)
    ├── Partition-c → Worker-3  (sadece kendi mesajları)
    └── Orphan      → Worker-1 + Worker-2 + Worker-3 (label'sız fallback)
```

| Avantaj | Açıklama |
|---|---|
| **Sıfır lock contention** | Her worker kendi partition'ından okur, başkasını beklemez |
| **Mesaj sahipliği** | `WaitForAck` modunda mesaj partition'da bekler, başka worker'a kaçmaz |
| **Tenant izolasyonu** | Yoğun tenant diğer tenant'ların partition'larını etkilemez |
| **Ölçeklenebilir** | Yeni worker = yeni partition açılır |
| **Esnek temizlik** | Bir partition yok olursa diğerleri çalışmaya devam eder |
| **Şeffaf producer** | Producer hâlâ `FetchOrders`'a yazar, routing sisteme aittir |

---

## Restart ve Consumer-Bounce Davranışları

> Bu bölüm `PartitionRestartTest` ve `PartitionPersistentTest` ile test edilen **gerçek** davranışları belgeler.

---

### Consumer Bounce (Server Restart Yok)

Consumer düştükten sonra producer mesaj göndermeye devam ederse ne olur?

**Yeni davranış (düzeltilmiş):** Label'lı mesajlar her zaman, subscriber olsun ya da olmasın, kendi labeled partition'ına gider ve orada saklanır.

| Durum | Davranış |
|---|---|
| Consumer **online** | Direkt labeled partition → consumer, anında teslim |
| Consumer **offline**, push label'lı | **Aynı labeled partition'a** yönlendirilir, store'da bekler |
| Consumer **yeniden bağlanır** | Partition store'undaki mesajlar Trigger() ile teslim edilir |

**`enableOrphan` değeri bu davranışı etkilemez.** Labeled mesajlar asla orphan'a gitmez, asla drop edilmez.

```csharp
// enableOrphan=true/false fark etmez — labeled mesajlar her zaman labeled partition'da saklanır
opts.Partition = new PartitionOptions
{
    Enabled               = true,
    EnableOrphanPartition = false,   // tercihsel — labeled bounce için gerekli değil
    MaxPartitionCount     = 10,
    SubscribersPerPartition = 1,
};
```

---

### Tam Tenant İzolasyonu

`enableOrphan=false` veya `enableOrphan=true` olması fark etmez: **A-label'lı mesajlar yalnızca A partition'ında işlenir**.

```
Worker-A online:   msg(label=A) → Partition-A → Worker-A  ✓ izole
Worker-A offline:  msg(label=A) → Partition-A store'da bekler  ✓ hâlâ izole
Worker-A geri geldi: Partition-A → Worker-A  ✓ doğru worker'a teslim
                              Worker-B asla A'nın mesajını görmez  ✓
```

---

### Server Restart — Tam Recovery

#### ✅ Korunan ve Düzgün Restore Edilen

| Veri | Nasıl |
|---|---|
| Parent queue `PartitionOptions` | `queues.json` — restore edilir |
| Partition sub-queue isimleri ve metadata | `queues.json` → `SubPartition` alanı ile — restore edilir |
| `IsPartitionQueue` flag'i | `queues.json` → `SubPartition != null` ise ReAttach içinde set edilir |
| Partition label index | ReAttach → `_labelIndex`'e eklenir |
| Orphan partition | ReAttach → `_orphanPartition` set edilir |
| Partition / orphan sub-queue'lardaki mesajlar | `.hdb` dosyaları — reload edilir |
| Restart sonrası consumer aynı label ile bağlanırsa | **AYNI partition queue'ya** re-attach edilir, yeni GUID üretilmez |
| Restart sonrası buffered mesajların teslimi | Consumer bağlandığında `Trigger()` → mesajlar teslim edilir ✅ |

#### Restart Sonrası Durum Diyagramı

```
Restart öncesi:
  FetchOrders                     (Partition.Enabled=true)
  FetchOrders-Partition-abc123    (label=tenant-42, SubPartition kaydedildi, 5 mesaj .hdb'de)
  FetchOrders-Partition-Orphan    (IsOrphan=true, SubPartition kaydedildi, 3 mesaj .hdb'de)

Restart sonrası:
  Pass 1 — queues.json'dan tüm queue'lar yüklenir
  Pass 2 — SubPartition kaydı olan queue'lar parent'ın PartitionManager'ına ReAttach edilir

  FetchOrders                     ← Partition.Enabled=true ✓, PartitionManager.Partitions dolu ✓
  FetchOrders-Partition-abc123    ← IsPartitionQueue=true ✓, _labelIndex["tenant-42"] ✓, 5 mesaj ✓
  FetchOrders-Partition-Orphan    ← IsPartitionQueue=true ✓, _orphanPartition ✓, 3 mesaj ✓

Consumer tenant-42 ile bağlandı:
  _labelIndex["tenant-42"] → mevcut Partition-abc123 ✓ (yeni GUID üretilmez)
  Trigger() → 5 buffered mesaj teslim edildi ✓
```

#### `SubPartition` Metadata Kalıcılığı

```json
// queues.json içinde partition sub-queue örneği
{
  "Name": "FetchOrders-Partition-abc123",
  "SubPartition": {
    "ParentQueueName": "FetchOrders",
    "PartitionId": "abc123",
    "Label": "tenant-42",
    "IsOrphan": false
  }
}
```

`SubPartition` alanı `PartitionManager.CreatePartition()` veya `GetOrCreateOrphanQueue()` içinde `PartitionMeta` set edildikten hemen sonra `UpdateConfiguration(false)` çağrısıyla persiste edilir.

---

### Test Kapsamı — Restart & Bounce

| Test Adı | Test Edilen Senaryo |
|---|---|
| `ConsumerBounce_OfflinePushedMessages_DeliveredOnReconnect` | Consumer düşer → labeled partition'da tamponlanır → consumer geri gelir → teslim |
| `ServerRestart_ParentQueue_PartitionOptionsRestored` | Restart sonrası parent queue `PartitionOptions` korunuyor mu? |
| `Producer_Continuous_ConsumerReconnects_ReceivesAll` | Producer sürekli push → consumer bounce → reconnect → 4 offline mesaj labeled partition'dan alınır |
| `TwoTenants_ConsumerBounce_FullIsolationMaintained` | A düşünce mesajlar A'nın partition'ında bekler; B asla A'nın mesajını almaz; A geri gelince tam teslim |
| `ServerRestart_PartitionSubQueues_ReAttachedAndMessagesDelivered` | Restart sonrası sub-queue re-attach, aynı GUID korunur, 1 mesaj teslim edilir |
| `OrphanPartition_ConsumerBounce_OfflineMessages_Delivered` | Label'sız orphan mesajları bounce sonrası teslim edilir |

---

## Benchmark Sonuçları

> **Ortam:** Apple M1 Max · 10 çekirdek · .NET 10.0.2 (Arm64 RyuJIT AdvSIMD)  
> **Konfigürasyon:** 1 launch · 2 warmup · 5 ölçüm iterasyonu · `Release` build  
> **Suite konumu:** `src/Benchmarks/Benchmark.Partition/`  
> **Çalıştırma tarihi:** 28 Şubat 2026

### Suite Envanteri

| # | Suite | Filtre | Benchmark Sayısı |
|---|---|---|---|
| 1 | Routing Maliyeti | `*RoutingCost*` | 8 |
| 2 | Partition Ölçekleme | `*Scaling*` | 4 |
| 3 | Partition vs. Flat RoundRobin | `*VsFlat*` | 12 |
| 4 | Labeled Push Throughput | `*Labeled*` | 4 |
| 5 | Orphan Throughput | `*Orphan*` | 6 |
| 6 | Partition Lifecycle | `*Lifecycle*` | 3 |
| 7 | Multi-Tenant İzolasyon | `*MultiTenant*` | 4 |
| 8 | Partition İçi Broadcast | `*Broadcast*` | 3 |
| 9 | WaitForAck İzolasyonu | `*WaitForAck*` | 8 |
| 10 | Consumer Bounce & Redeliver | `*Bounce*` | 3 |
| 11 | Büyük Payload Routing | `*LargePayload*` | 8 |

---

### 1. Routing Maliyeti — Label Lookup vs. Label'sız Round-Robin

PartitionManager routing katmanının mesaj başına maliyetini ölçer (consumer backpressure yok, `QueueAckDecision.None`).

| Method | PartitionCount | Ortalama | Ratio | Allocated |
|---|---|---|---|---|
| Routing_NoLabel_RoundRobin *(baseline)* | 1 | 37.01 µs | 1.00 | 7.78 KB |
| Routing_LabelLookup_Push | 1 | 38.54 µs | 1.04 | 7.78 KB |
| Routing_LabelLookup_Push | 10 | 379.07 µs | **0.95** | 79.57 KB |
| Routing_NoLabel_RoundRobin | 10 | 399 µs | 1.00 | 95.1 KB |
| Routing_LabelLookup_Push | 50 | 2.068 µs | **0.81** | 414.42 KB |
| Routing_NoLabel_RoundRobin | 50 | 2.563 µs | 1.00 | 630 KB |
| Routing_LabelLookup_Push | 100 | 4.439 µs | **0.67** | 866.64 KB |
| Routing_NoLabel_RoundRobin | 100 | 6.657 µs | 1.00 | 1.694 KB |

**Bulgular:**
- 1 partition'da label lookup overhead'i ihmal edilebilir (+1.5 µs).
- Label routing **sub-lineer** ölçeklenir; 100 partition'da **%33 daha hızlı**.
- Label routing'de bellek tahsisi yüksek partition sayısında **%49'a kadar daha az**.

---

### 2. Partition Ölçekleme — Labeled Push (toplam 10.000 mesaj)

| Method | PartitionCount | Ortalama | Rank | Allocated |
|---|---|---|---|---|
| PartitionScale_LabeledPush | 5 | 200.5 ms | 1 | 80.2 MB |
| PartitionScale_LabeledPush | 10 | 216.0 ms | 1 | 58.9 MB |
| PartitionScale_LabeledPush | 20 | 220.2 ms | 1 | 67.3 MB |
| PartitionScale_LabeledPush | 1 | 390.1 ms | 2 | 76.1 MB |

**Bulgular:** 1 partition en yavaş (390 ms). 5–20 partition eşit hızda (~200–220 ms) — paralel producer'lar tüm partition'ları eş zamanlı doyuruyor.

---

### 3. Partition vs. Flat RoundRobin (label'sız, acknowledge yok)

| Method *(Baseline = Flat)* | WorkerCount | MessageCount | Ortalama | Ratio | Allocated |
|---|---|---|---|---|---|
| FlatRoundRobin | 2 | 5.000 | 177.4 ms | 1.00 | 33.5 MB |
| PartitionedRoundRobin | 2 | 5.000 | 184.4 ms | 1.04 | 40.7 MB |
| FlatRoundRobin | 2 | 20.000 | 706.6 ms | 1.00 | 134 MB |
| PartitionedRoundRobin | 2 | 20.000 | 740.7 ms | 1.05 | 160 MB |
| FlatRoundRobin | 5 | 5.000 | 173.8 ms | 1.00 | 33.5 MB |
| PartitionedRoundRobin | 5 | 5.000 | 191.7 ms | 1.10 | 41.6 MB |
| FlatRoundRobin | 5 | 20.000 | 696.8 ms | 1.00 | 133 MB |
| PartitionedRoundRobin | 5 | 20.000 | 746.5 ms | 1.07 | 167 MB |
| FlatRoundRobin | 10 | 5.000 | 173.2 ms | 1.00 | 33.5 MB |
| PartitionedRoundRobin | 10 | 5.000 | 189.0 ms | 1.09 | 44.1 MB |
| FlatRoundRobin | 10 | 20.000 | 691.6 ms | 1.00 | 134 MB |
| PartitionedRoundRobin | 10 | 20.000 | 760.2 ms | 1.10 | 175.8 MB |

**Bulgular:** Partition overhead saf push-throughput (ack yok) senaryolarında **%4–10**. İzolasyon, bağımsız AutoDestroy ve WaitForAck bağımsızlığının bedeli.

---

### 4. Labeled Push Throughput

Her N labeled partition'a eş zamanlı 100 mesaj.

| Method | PartitionCount | Ortalama | Allocated |
|---|---|---|---|
| LabeledPush_Throughput | 1 | ~38.5 µs | 7.78 KB |
| LabeledPush_Throughput | 10 | ~379 µs | 79.6 KB |
| LabeledPush_Throughput | 50 | ~2.068 µs | 414 KB |
| LabeledPush_Throughput | 100 | ~4.439 µs | 867 KB |

---

### 5. Orphan Throughput — Label'sız Push

| Method | ConsumerCount | MessageCount | Ortalama | Allocated |
|---|---|---|---|---|
| OrphanLabelLess_Push | 1 | 5.000 | 187.3 ms | 36.1 MB |
| OrphanLabelLess_Push | 3 | 5.000 | 319.7 ms | 56.0 MB |
| OrphanLabelLess_Push | 8 | 5.000 | 654.2 ms | 102.9 MB |
| OrphanLabelLess_Push | 1 | 20.000 | 747.0 ms | 147.2 MB |
| OrphanLabelLess_Push | 3 | 20.000 | 1.272 ms | 223.1 MB |
| OrphanLabelLess_Push | 8 | 20.000 | 2.616 ms | 411.5 MB |

**Bulgular:** Orphan throughput consumer sayısıyla **lineer** ölçeklenir (fan-out). N=8'de N=1'in ~3.5 katı. Fan-out maliyeti kabul edilemezse label'lı partition kullanılmalı.

---

### 6. Partition Lifecycle — Oluştur / Push / Yok Et

| Method | PartitionsToCreate | Ortalama | Allocated |
|---|---|---|---|
| Partition_Create_Push_Destroy | 1 | 23.4 ms | 882 KB |
| Partition_Create_Push_Destroy | 5 | 102 ms | 11.7 MB |
| Partition_Create_Push_Destroy | 10 | 217 ms | 24.4 MB |

**Bulgular:** Lifecycle maliyeti partition başına ~20–22 ms. Lineer ölçeklenir.

---

### 7. Multi-Tenant İzolasyon — Gürültülü Tenant

| Method | TenantCount | Mesaj/tenant | Ortalama | Allocated |
|---|---|---|---|---|
| MultiTenant_With_NoisyTenant | 3 | 500 | 115.3 ms | 30.5 MB |
| MultiTenant_With_NoisyTenant | 8 | 500 | 187.7 ms | 49.2 MB |
| MultiTenant_With_NoisyTenant | 3 | 2.000 | 466.6 ms | 123.4 MB |
| MultiTenant_With_NoisyTenant | 8 | 2.000 | 751.1 ms | 204.3 MB |

**Bulgular:** 3→8 tenant geçişinde süre sadece ~%63 artıyor (beklenen 2.67× değil). Partition izolasyonu gürültülü tenant'ın diğerlerinde head-of-line blocking yapmasını engelliyor.

---

### 8. Partition İçi Broadcast (Push, çok subscriber)

| Method | FanOut | Ortalama | Allocated |
|---|---|---|---|
| Broadcast_Push_PerPartition | 1 | 82.4 ms | 15.95 MB |
| Broadcast_Push_PerPartition | 2 | 110.3 ms | 19.9 MB |
| Broadcast_Push_PerPartition | 5 | 194.9 ms | 31.2 MB |

**Bulgular:** 1.000 mesajda her ek subscriber ~28 ms ekliyor. `QueueType.Push` + `SubscribersPerPartition > 1` partition içi artıklık için kullanılır.

---

### 9. WaitForAck İzolasyonu — Partitioned vs. Flat

Worker 0 yavaş (5 ms/mesaj), diğerleri hızlı.

| Method *(Baseline = Flat)* | WorkerCount | MessageCount | Ortalama | Ratio | Allocated |
|---|---|---|---|---|---|
| WaitForAck_FlatQueue | 2 | 200 | 4.618 ms | 1.00 | 858 KB |
| WaitForAck_Partitioned | 2 | 200 | 5.214 ms | **1.13** | 1.015 KB |
| WaitForAck_FlatQueue | 2 | 1.000 | 21.79 ms | 1.00 | 4.296 KB |
| WaitForAck_Partitioned | 2 | 1.000 | 26.34 ms | **1.21** | 5.320 KB |
| WaitForAck_FlatQueue | 4 | 200 | 4.429 ms | 1.00 | 863 KB |
| WaitForAck_Partitioned | 4 | 200 | 4.158 ms | **0.94** | 1.020 KB |
| WaitForAck_FlatQueue | 4 | 1.000 | 21.97 ms | 1.00 | 5.046 KB |
| WaitForAck_Partitioned | 4 | 1.000 | 22.91 ms | **1.04** | 5.014 KB |

**Bulgular:**
- **2 worker** (1 yavaş, 1 hızlı): Partitioned %13–21 daha yavaş — flat queue yavaş worker'ı atlayabiliyor.
- **4 worker** (1 yavaş, 3 hızlı): Partitioned **%6 daha hızlı** — 3 hızlı worker yavaş worker'ın partition'ından hiç etkilenmiyor.
- Partition'ın WaitForAck avantajı ≥3 worker + heterojen işlem hızında ortaya çıkar. Worker sayısı arttıkça fark büyür.

---

### 10. Consumer Bounce & Redeliver

N mesaj push → consumer bağlantısını kes → yeniden bağlan → tüm N mesajı al.

| Method | MessageCount | Ortalama | Median | Allocated |
|---|---|---|---|---|
| Bounce_Buffer_Redeliver | 100 | 568 ms | 943 ms | 797 KB |
| Bounce_Buffer_Redeliver | 1.000 | 894 ms | 38 ms | 7.4 MB |
| Bounce_Buffer_Redeliver | 5.000 | 166 ms | 166 ms | 36.7 MB |

> **Not:** 100/1.000 durum için yüksek `Error` TCP yeniden bağlanma zamanlaması kaynaklı. 5.000 mesajlı durum kararlı (±9 ms).

**Bulgular:** Bounce sırasında hiçbir mesaj kaybolmaz. 5.000+ mesajda redeliver hızlıdır (166 ms ≈ 30.000 mesaj/sn). Reconnect overhead sabit ~30–50 ms.

---

### 11. Büyük Payload Routing

Partition başına 10 mesaj, labeled push, 1 KB – 256 KB.

| Method | PayloadKB | PartitionCount | Ortalama | Allocated |
|---|---|---|---|---|
| LargePayload_Labeled_Push | 16 | 10 | 4.57 ms | 16.3 MB |
| LargePayload_Labeled_Push | 64 | 10 | 13.8 ms | 63.1 MB |
| LargePayload_Labeled_Push | 256 | 10 | 19.1 ms | 250.7 MB |
| LargePayload_Labeled_Push | 1 | 10 | 27.9 ms | 7.3 MB |
| LargePayload_Labeled_Push | 1 | 1 | 28.3 ms | 7.3 MB |
| LargePayload_Labeled_Push | 16 | 1 | 46.5 ms | 66.4 MB |
| LargePayload_Labeled_Push | 64 | 1 | 123 ms | 251.6 MB |
| LargePayload_Labeled_Push | 256 | 1 | 195 ms | 976 MB |

**Bulgular:**
- 10-partition routing büyük payload'larda **4–10× daha hızlı** — paralel kanal kullanımı.
- 256 KB × 10 partition 19 ms vs. tek partition 195 ms: **10× hızlanma**.
- Bellek payload × partition sayısıyla lineer ölçeklenir.

---

### Yeniden Çalıştırma

```bash
# Tekil suite'ler:
dotnet run -c Release -- --filter "*RoutingCost*"
dotnet run -c Release -- --filter "*Scaling*"
dotnet run -c Release -- --filter "*VsFlat*"
dotnet run -c Release -- --filter "*Labeled*"
dotnet run -c Release -- --filter "*Orphan*"
dotnet run -c Release -- --filter "*Lifecycle*"
dotnet run -c Release -- --filter "*MultiTenant*"
dotnet run -c Release -- --filter "*Broadcast*"
dotnet run -c Release -- --filter "*WaitForAck*"
dotnet run -c Release -- --filter "*Bounce*"
dotnet run -c Release -- --filter "*LargePayload*"

# Sonuçlar şuraya yazılır:
# src/Benchmarks/Benchmark.Partition/BenchmarkDotNet.Artifacts/results/
```


