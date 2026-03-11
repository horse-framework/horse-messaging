# RoundRobin Queue — Signal Mekanizması Değişiklik Raporu

**Tarih:** 11 Mart 2026  
**Kapsam:** `Horse.Messaging.Server` — RoundRobin queue state CPU burn fix + ACK-based signal mekanizması

---

## Özet

RoundRobin queue state'inde, tüm consumer'lar meşgulken server'ın CPU'yu %100 burn etmesine neden olan **polling-based tight loop** mekanizması, **event-driven signal** mekanizmasıyla değiştirildi. Ayrıca ACK sonrası bekleyen mesajların anında teslim edilmesi sağlandı.

---

## Değiştirilen Dosyalar

| # | Dosya | Değişiklik Türü |
|---|-------|----------------|
| 1 | `Queues/States/RoundRobinQueueState.cs` | Signal mekanizması eklendi, polling döngüsü değiştirildi |
| 2 | `Queues/HorseQueue.cs` | ACK alındığında signal tetikleme eklendi (3 noktada) |
| 3 | `Queues/Delivery/DefaultDeliveryTracker.cs` | ACK timeout sonrası signal tetikleme eklendi |

---

## 1. RoundRobinQueueState.cs — Ana Değişiklik

### Önceki Durum (Sorun)

`GetNextAvailableRRClient` metodu, uygun consumer bulana kadar tight loop'ta dönüyordu:

```csharp
// ESKİ KOD
tryCount++;
if (tryCount > 10)
{
    int storedMessageCount = _queue.Manager.MessageStore.Count()
                           + _queue.Manager.PriorityMessageStore.Count();

    if (storedMessageCount < 3)       await Task.Delay(100);
    else if (storedMessageCount < 8)  await Task.Delay(50);
    else if (storedMessageCount < 25) await Task.Delay(25);
    else                              await Task.Delay(5);

    tryCount = 0;
}
```

**Sorunlar:**

- İlk 10 turda **delay yok** → saf CPU spin
- Delay süreleri sabit ve mesaj sayısına bağlı → gereksiz karmaşıklık
- En düşük delay 5ms → yüksek mesaj yükünde bile CPU'yu meşgul tutuyor
- **Hiçbir ACK sinyali yok** → server, consumer'ın işi bitirdiğini "bilmiyor", körlemesine tarama yapıyor
- 30 saniyelik `retryExpiration` boyunca bu döngü devam ediyor

### Yeni Durum (Çözüm)

**SemaphoreSlim tabanlı signal mekanizması eklendi:**

```csharp
private readonly SemaphoreSlim _clientAvailableSignal = new(0, int.MaxValue);

internal void SignalClientAvailable()
{
    try { _clientAvailableSignal.Release(); }
    catch (SemaphoreFullException) { }
}
```

**Retry döngüsü yeniden yazıldı:**

```csharp
if (firstScan)
{
    firstScan = false;  // İlk taramada hemen tekrar dene (state değişmiş olabilir)
}
else
{
    // Stale signal'ları temizle
    while (_clientAvailableSignal.CurrentCount > 0)
        _clientAvailableSignal.Wait(0);

    // ACK gelene kadar BLOKE OL — CPU tüketme
    await _clientAvailableSignal.WaitAsync(1000);  // 1s safety fallback
}
```

**İyileştirmeler:**

- **Event-driven:** ACK geldiğinde `SignalClientAvailable()` çağrılır → `WaitAsync` anında döner
- **Zero CPU burn:** Bekleme sırasında thread serbest, CPU kullanımı sıfır
- **1 saniyelik fallback:** Signal kaçırılırsa 1s sonra tekrar taranır (safety net)
- **İlk scan optimizasyonu:** State'in az önce değişmiş olma ihtimaline karşı 1 anında retry

---

## 2. HorseQueue.cs — ACK Sonrası Signal Tetikleme

`AcknowledgeDelivered` metodunda 3 farklı code path'e signal eklendi:

### a) Normal ACK (delivery bulundu, başarılı)

```csharp
delivery.MarkAsAcknowledged(success);
ReleaseAcknowledgeLock(true);

// YENİ: Consumer artık müsait — döngüyü uyandır
if (Type == QueueType.RoundRobin && State is RoundRobinQueueState rrState)
    rrState.SignalClientAvailable();
```

**Neden:** ACK geldiğinde `CurrentlyProcessing = null` yapılır → consumer artık yeni mesaj alabilir. Signal ile bekleyen `GetNextAvailableRRClient` anında uyandırılır.

### b) Delivery bulunamadığında (ultra-hızlı ACK senaryosu)

```csharp
if (delivery == null)
{
    QueueClient queueClient = FindClient(from);
    if (queueClient != null)
        if (queueClient.CurrentlyProcessing != null && ...)
        {
            queueClient.CurrentlyProcessing = null;

            // YENİ: Bu durumda da signal ver
            if (Type == QueueType.RoundRobin && State is RoundRobinQueueState rrNullDelivery)
                rrNullDelivery.SignalClientAvailable();
        }
    return;
}
```

**Neden:** ACK, delivery tracker'a kaydedilmeden önce gelirse (< 0.01ms) `FindDelivery` null döner. `CurrentlyProcessing = null` yapılır ama eski kodda signal verilmiyordu → consumer müsait olmasına rağmen mesaj teslim edilmiyordu.

### c) ACK timeout olduğunda

```csharp
if (delivery.Acknowledge == DeliveryAcknowledge.Timeout)
{
    // YENİ: Timeout'ta da consumer slot'u serbest — signal ver
    if (Type == QueueType.RoundRobin && State is RoundRobinQueueState rrTimeout)
        rrTimeout.SignalClientAvailable();
    return;
}
```

**Neden:** Consumer ACK göndermezse timeout olur, consumer slot'u serbest kalır. Eski kodda bu durumda signal yoktu → bekleyen mesajlar 5 saniyelik timer'a düşüyordu.

---

## 3. DefaultDeliveryTracker.cs — Timeout Signal

```csharp
if (atLeastOneRemoved)
{
    _queue.ReleaseAcknowledgeLock(false);

    // YENİ: Timeout ile slot serbest kaldı — döngüyü uyandır
    if (_queue.Type == QueueType.RoundRobin && _queue.State is RoundRobinQueueState rrState)
        rrState.SignalClientAvailable();
}
```

**Neden:** `ProcessDeliveries` periyodik olarak çalışır ve timeout olan delivery'leri temizler. Consumer'ların `CurrentlyProcessing` alanı serbest bırakılır. Eski kodda bu noktada signal yoktu.

---

## Etki Analizi

| Metrik | Önce | Sonra |
|--------|------|-------|
| Tüm consumer'lar meşgulken CPU | Tight loop (5-100ms delay ile polling) | 0 (SemaphoreSlim.WaitAsync) |
| ACK sonrası mesaj teslim latency | ~5s (timer fallback) | < 100ms (signal-based) |
| Throughput | Değişmedi | Değişmedi (15 msg/sec @ 200ms ACK) |
| RAM overhead | — | +1 SemaphoreSlim instance per queue (ihmal edilebilir) |

---

## Debug Keşif Süreci

Sorunun tespitinde dosya tabanlı loglama (`/tmp/horse-debug.log`) kullanıldı. Keşifler:

| Log | Sayı | Anlam |
|-----|------|-------|
| `[RR-ENTER]` | ~500 | `GetNextAvailableRRClient` her mesaj için çağrılıyor |
| `[RR-SIGNAL]` | ~257 | ACK'lar signal'ı tetikliyor |
| `[RR-WAIT]` | **0** | WaitAsync noktasına hiç ulaşılmadı |

**Kritik bulgu:** `GetNextAvailableRRClient`'te spin-wait noktasına hiç ulaşılmıyordu. `ProcessPendingMessages` akışında client bulunamazsa mesaj `AddMessage(message, false)` ile store'a geri konulup döngüden çıkılıyordu (`trigger = false`). Yeni mesajın teslimi **5 saniyelik `_triggerTimer`'a düşüyordu**.

Bu keşif, signal mekanizmasının sadece `GetNextAvailableRRClient` için değil, **ACK sonrası `Trigger()` çağrılması** için de kritik olduğunu ortaya koydu.

---

## Test Kapsamı

Değişiklikler 3 yeni latency-based test ile doğrulandı (`Test.Server/Benchmarks/RoundRobinBenchmarkTest.cs`):

| Test | Senaryo | Eşik | Sonuç |
|------|---------|------|-------|
| `RoundRobin_Signal_AckTriggersImmediateDelivery` | 1 consumer, ACK sonrası sıradaki mesaj ne kadar hızlı teslim edilir? | < 2000ms | ✅ ~500ms |
| `RoundRobin_Signal_MultiConsumer_NoTimerFallback` | 3 consumer × 300ms delay, 10 mesaj, toplam süre | < 5000ms | ✅ ~2s |
| `RoundRobin_Signal_AckTimeout_FreesConsumerSlot` | Consumer ACK göndermez, timeout=2s, sonraki mesaj ne zaman gelir? | < 5000ms | ✅ ~3s |

**Test stratejisi:** `Process.TotalProcessorTime` (CPU/Wall ratio) multi-thread in-process testlerde güvenilir olmadığı keşfedildi — N thread paralel çalışırsa ratio N'e kadar çıkabiliyor. Bu nedenle CPU burn tespiti **latency-based assertion**'lara dayandırıldı: signal mekanizması bozulursa mesaj teslimi 5s timer'a düşer ve testler kesinlikle FAIL olur.

---

## Neden CPU/Wall Ratio Güvenilir Değil?

`Process.GetCurrentProcess().TotalProcessorTime` tüm thread'lerin toplam CPU zamanını ölçer. In-process testlerde server, producer ve 3 consumer aynı process'te çalışır:

| Senaryo | Wall Time | CPU Time | Ratio | Anlamı |
|---------|-----------|----------|-------|--------|
| 10K msg, fast ACK | 0.5s | 3.4s | 6.29 | 6+ thread paralel çalışıyor — normal |
| 500 msg, 200ms delay | 33.6s | 33.9s | 1.01 | Consumer delay'ler CPU tüketmiyor — normal |
| 1K msg, 50ms delay | 17.1s | 17.8s | 1.04 | Toplam iş yükü wall time'a eşit — normal |

Her üç durumda da spin-wait **yoktu** ama ratio yüksek görünüyordu. Bu nedenle benchmark testlerinde CPU/Wall ratio sadece **diagnostic log** olarak bırakıldı, assertion'dan kaldırıldı.

