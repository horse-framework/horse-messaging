# Horse.Messaging.Client — API Consistency & CancellationToken Audit Report

## Executive Summary

Tüm client-side public API'lar tarandı. **3 ana problem** tespit edildi:

1. **CancellationToken propagation kırılıyor** — 7 public async metot CancellationToken almıyor
2. **`= default` kullanımı** — 120+ yerde `CancellationToken cancellationToken = default` var, zincir sessizce `CancellationToken.None` ile çalışıyor
3. **`CancellationToken.None` açık kullanımı** — GracefulShutdownService'te 2 yerde
4. **Optional parameter karmaşası** — Overload yerine `= null`, `= false`, `= default` ile uzayan parametre listeleri

---

## BÖLÜM 1: CancellationToken ALMAYAN PUBLIC ASYNC API'LAR

Bu metotlar async çalışıyor ama CancellationToken kabul etmiyor. Token zinciri burada **tamamen kırılıyor**.

### HorseClient.cs

| Metot | Satır | Sorun |
|-------|-------|-------|
| `SendAsync(HorseMessage, IList<KVP>)` | 871 | CT yok, `= null` optional header var |
| `SendAndGet<T>(HorseMessage)` | 1011 | CT yok, tracker bekliyor ama iptal edilemiyor → **SİLİNECEK**, `SendAsync<T>` ile değişecek |
| `SendAndGetAck(HorseMessage, IList<KVP>, CT)` | 951 | `= default` + `= null` → **SİLİNECEK**, `SendAsync(msg, true, ct)` ile değişecek |
| `SendPluginMessage<T>(string, object, ushort)` | 1034 | CT yok, tracker bekliyor → **SİLİNECEK**, `SendPluginAsync<T>` ile değişecek |
| `SendEnvelopedAndGetAck(...)` | 976 | 3 optional param → **SİLİNECEK**, `SendEnvelopedAsync` ile değişecek |
| `SendAck(HorseMessage)` | 1151 | CT yok |
| `SendNegativeAck(HorseMessage, string)` | 1139 | CT yok |
| `SendResponse(HorseMessage)` | 1162 | CT yok (SendAck'a delegate ediyor) |
| `SendNegativeResponse(HorseMessage, string)` | 1173 | CT yok (SendNegativeAck'a delegate ediyor) |
| `SendResponseAsync<TModel>(HorseMessage, TModel)` | 1060 | CT yok |
| `SendResponseAsync(HorseMessage, string)` | 1070 | CT yok |
| `SendResponseAsync(HorseMessage, Stream)` | 1080 | CT yok |

### EventOperator.cs

| Metot | Satır | Sorun |
|-------|-------|-------|
| `Subscribe(HorseEventType, string, bool)` | 45 | CT yok |
| `Unsubscribe(HorseEventType, string, bool)` | 62 | CT yok |

### HorseTransaction.cs

| Metot | Satır | Sorun |
|-------|-------|-------|
| `Begin()` | 125 | CT yok |
| `Begin<TModel>(TModel)` | 138 | CT yok |
| `Commit()` | 171 | CT yok |
| `Rollback()` | 193 | CT yok |
| `static Begin(HorseClient, string)` | 92 | CT yok |

### ConnectionOperator.cs

| Metot | Satır | Sorun |
|-------|-------|-------|
| `GetInstances()` | 26 | CT yok |
| `GetConnectedClients(string)` | 40 | CT yok |

### QueueOperator.cs — Admin metotları

| Metot | Satır | Sorun |
|-------|-------|-------|
| `Create(string)` | 176 | CT yok |
| `Create(string, IEnumerable<KVP>)` | 184 | CT yok |
| `Create(string, string, IEnumerable<KVP>)` | 193 | CT yok |
| `Create(string, Action<QueueOptions>, string, IEnumerable<KVP>)` | 203 | CT yok |
| `List(string)` | 239 | CT yok |
| `Remove(string)` | 257 | CT yok |
| `SetOptions(string, Action<QueueOptions>)` | 272 | CT yok |
| `ClearMessages(string, bool, bool)` | 296 | CT yok |
| `GetConsumers(string)` | 321 | CT yok |

### ChannelOperator.cs — Query metotları

| Metot | Satır | Sorun |
|-------|-------|-------|
| `List(string)` | 176 | CT yok |
| `GetSubscribers(string)` | 192 | CT yok |

### RouterOperator.cs — Admin metotları

| Metot | Satır | Sorun |
|-------|-------|-------|
| `Create(string, RouteMethod)` | 34 | CT yok |
| `List()` | 49 | CT yok |
| `Remove(string)` | 61 | CT yok |
| `AddBinding(...)` | 84 | CT yok |
| `GetBindings(string)` | 116 | CT yok |
| `RemoveBinding(string, string)` | 128 | CT yok |

---

## BÖLÜM 2: `CancellationToken.None` AÇIK KULLANIMLARI

| Dosya | Satır | Kullanım |
|-------|-------|----------|
| `GracefulShutdownService.cs` | 58 | `await Task.Delay(min, CancellationToken.None)` |
| `GracefulShutdownService.cs` | 68 | `await Task.Delay(250, CancellationToken.None)` |

**Not:** GracefulShutdownService host token iptal olduktan sonra çalışıyor. `CancellationToken.None` kullanım sebebi bu. Ama kendi shutdown CTS'i oluşturulmalı.

---

## BÖLÜM 3: OPTIONAL PARAMETER KARMAŞASI

### Problem: `= default` ve `= null` zincirleri

Örnek — `IHorseQueueBus.Push`:
```csharp
Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
    string partitionLabel = null,
    CancellationToken cancellationToken = default);
```

Problemler:
1. `waitForCommit = false` — Kullanıcı sadece CT geçmek isterse tüm ara parametreleri yazmak zorunda
2. `CancellationToken = default` — Kullanıcı CT geçmezse sessizce `CancellationToken.None` oluyor, propagation kırılıyor
3. `messageHeaders = null` — Her çağrıda null geçmek çirkin

### Etkilenen API Alanları

| Alan | `= default` CT sayısı | Optional param karmaşası |
|------|----------------------|--------------------------|
| QueueOperator (Push/Pull/Subscribe) | 12 | ✅ Ağır |
| IHorseQueueBus | 8 | ✅ Ağır |
| ChannelOperator | 8 | ✅ Orta |
| IHorseChannelBus | 6 | ✅ Orta |
| DirectOperator | 11 | ✅ Orta |
| IHorseDirectBus | 15 | ✅ Ağır |
| RouterOperator | 8 | ✅ Orta |
| IHorseRouterBus | 7 | ✅ Orta |
| IHorseCache / HorseCache | 17 | ✅ Ağır |
| HorseClient (core) | 4 | ✅ Hafif |
| **TOPLAM** | **~96** | |

---

## BÖLÜM 4: TUTARSIZLIKLAR

### 4.1 Aynı işlem farklı imzalar

**QueueOperator.Push vs IHorseQueueBus.Push:**
- `QueueOperator.Push<T>` — `partitionLabel` yok, header'la geçiliyor
- `IHorseQueueBus.Push<T>` — `partitionLabel` parametresi var, header'a otomatik ekliyor
- **Tutarsız:** Aynı işlem için 2 farklı API deneyimi

**Subscribe:**
- `QueueOperator.Subscribe(queue, verifyResponse, headers, ct)` — headers optional
- `QueueOperator.SubscribePartitioned(queue, label, verifyResponse, maxP, subsPerP, headers, ct)` — 7 parametre!

### 4.2 `verifyResponse` vs `waitForCommit` vs `waitAcknowledge` vs `waitForAcknowledge`

Aynı konsept, 4 farklı isim:
- `QueueOperator.Subscribe` → `verifyResponse` 
- `QueueOperator.Push` → `waitForCommit`
- `ChannelOperator.Publish` → `waitAcknowledge`
- `RouterOperator.Publish` → `waitForAcknowledge`

### 4.3 Event API'sı tamamen farklı

`EventOperator.Subscribe/Unsubscribe` hiç CancellationToken almıyor ve `verifyResponse` default false.
Diğer tüm operator'larda CT var.

---

## BÖLÜM 5: ÖNERİLEN OVERLOAD STRATEJİSİ

### İlke: "Parametre sıralama kuralı"

Her async public API şu sıralamayı takip etmeli:
```
1. Zorunlu iş parametreleri (queue, model, content, vb.)
2. CancellationToken (ZORUNLU, = default YOK)
```

Optional parametreler (headers, waitForCommit vb.) **overload'lar** ile sağlanmalı.

---

### 5.1 HorseClient Core Metotları — Overload Stratejisi

HorseClient, tüm operator'ların temelini oluşturuyor. 

**ÖNEMLİ KARAR:** `SendAndGetAck` ve `SendAndGet<T>` metotları **tamamen kaldırılıyor**. 
Bunların yaptığı her şey `SendAsync` overload'ları ile karşılanacak:
- Ack bekleme → `SendAsync(msg, waitAcknowledge: true, ct)` 
- Response deserialize → `SendAsync<T>(msg, ct)`

Bu sayede HorseClient'ta tek bir gönderim API ailesi kalıyor: **`SendAsync`**.

Mevcut bağımlılık zinciri ve migrasyon:

```
QueueOperator.Push     → Client.WaitResponse(msg, wait, ct)           ← CT = default kaldırılacak
ChannelOperator.Publish→ Client.WaitResponse(msg, wait, ct)           ← CT = default kaldırılacak
DirectOperator.Send    → Client.SendAndGetAck(msg, ct)                ← SendAsync(msg, true, ct) olacak
                       → Client.SendAsync(msg)                        ← SendAsync(msg, ct) olacak
DirectOperator.Request → Client.Request(msg, ct)                      ← CT = default kaldırılacak
RouterOperator.Publish → Client.WaitResponse(msg, wait, ct)           ← CT = default kaldırılacak
                       → Client.Request(msg, ct)                      ← CT = default kaldırılacak
CacheOperator.Get/Set  → Client.SendAndGetAck(msg, ct)                ← SendAsync(msg, true, ct) olacak
                       → Client.SendAndGet<T>(msg)                    ← SendAsync<T>(msg, ct) olacak
ConnectionOperator     → Client.SendAndGet<T>(msg)                    ← SendAsync<T>(msg, ct) olacak
EventOperator          → Client.WaitResponse(msg, verifyResponse)     ← CT eklenmeli!
HorseClient (AutoAck)  → SendAsync(msg.CreateAcknowledge())           ← SendAsync(msg, ConsumeToken) olacak
QueueConsumerExecutor  → client.SendAndGetAck(clone)                  ← SendAsync(clone, true, ct) olacak
```

#### 5.1.1 `SendAsync` — Birleşik mesaj gönderme API'sı

**KALDIRILACAKLAR:**
- `SendAndGetAck(HorseMessage, IList<KVP>, CT)` → **SİLİNİYOR**
- `SendAndGet<T>(HorseMessage)` → **SİLİNİYOR**
- `SendEnvelopedAndGetAck(...)` → `SendEnvelopedAsync(...)` olarak yeniden adlandırılıyor

**Şu anda:**
```csharp
// Fire-and-forget
public async Task<HorseResult> SendAsync(HorseMessage message, 
    IList<KeyValuePair<string, string>> additionalHeaders = null)

// Ack bekleme (ayrı metot — SİLİNECEK)
public Task<HorseResult> SendAndGetAck(HorseMessage message, 
    IList<KeyValuePair<string, string>> additionalHeaders = null,
    CancellationToken cancellationToken = default)

// Deserialize response (ayrı metot — SİLİNECEK)
public async Task<HorseModelResult<T>> SendAndGet<T>(HorseMessage message)
```

**Önerilen — Tek `SendAsync` ailesi:**
```csharp
// ── Fire-and-forget ──
public Task<HorseResult> SendAsync(HorseMessage message, CancellationToken cancellationToken)
public Task<HorseResult> SendAsync(HorseMessage message, 
    IList<KeyValuePair<string, string>> additionalHeaders, CancellationToken cancellationToken)

// ── Ack/response bekleme (SendAndGetAck'ın yerine) ──
public Task<HorseResult> SendAsync(HorseMessage message, bool waitForAcknowledge, CancellationToken cancellationToken)
public Task<HorseResult> SendAsync(HorseMessage message, bool waitForAcknowledge, 
    IList<KeyValuePair<string, string>> additionalHeaders, CancellationToken cancellationToken)

// ── Typed response deserialize (SendAndGet<T>'nin yerine) ──
public Task<HorseModelResult<T>> SendAsync<T>(HorseMessage message, CancellationToken cancellationToken)
```

**İç implementasyon:** 
- `SendAsync(msg, false, ct)` → sadece gönderir, ack beklemez
- `SendAsync(msg, true, ct)` → `WaitResponse(msg, true, ct)` çağırır (eski SendAndGetAck logic)
- `SendAsync<T>(msg, ct)` → tracker'da bekler + response deserialize eder (eski SendAndGet<T> logic)
- `SendAsync(msg, ct)` → `SendAsync(msg, false, ct)` delegate eder

**Migrasyon haritası:**

| Eski çağrı | Yeni çağrı |
|---|---|
| `client.SendAndGetAck(msg, ct: ct)` | `client.SendAsync(msg, true, ct)` |
| `client.SendAndGetAck(msg, headers, ct)` | `client.SendAsync(msg, true, headers, ct)` |
| `client.SendAndGet<T>(msg)` | `client.SendAsync<T>(msg, ct)` |
| `client.SendAsync(msg)` | `client.SendAsync(msg, ct)` |
| `client.SendAsync(msg, headers)` | `client.SendAsync(msg, headers, ct)` |

**Etkilenen çağrı siteleri:**

| Dosya | Eski | Yeni |
|---|---|---|
| DirectOperator.cs (4 yer) | `_client.SendAndGetAck(message, cancellationToken: ct)` | `_client.SendAsync(message, true, ct)` |
| DirectOperator.cs (4 yer) | `_client.SendAsync(message)` | `_client.SendAsync(message, ct)` |
| HorseCache.cs (12+ yer) | `_client.SendAndGetAck(message, cancellationToken: ct)` | `_client.SendAsync(message, true, ct)` |
| HorseCache.cs (Get) | `_client.SendAndGet<T>(message)` | `_client.SendAsync<T>(message, ct)` |
| ConnectionOperator.cs (2 yer) | `_client.SendAndGet<List<T>>(message)` | `_client.SendAsync<List<T>>(message, ct)` |
| QueueOperator.cs (2 yer) | `Client.SendAndGet<List<T>>(message)` | `Client.SendAsync<List<T>>(message, ct)` |
| ChannelOperator.cs (2 yer) | `Client.SendAndGet<List<T>>(message)` | `Client.SendAsync<List<T>>(message, ct)` |
| RouterOperator.cs (2 yer) | `_client.SendAndGet<List<T>>(message)` | `_client.SendAsync<List<T>>(message, ct)` |
| QueueConsumerExecutor.cs (1 yer) | `client.SendAndGetAck(clone)` | `client.SendAsync(clone, true, ct)` |
| RequestHandlerExecutor.cs (3 yer) | `client.SendAsync(responseMessage)` | `client.SendAsync(responseMessage, ct)` |
| HorseClient.cs (AutoAck, 2 yer) | `SendAsync(msg.CreateAcknowledge())` | `SendAsync(msg.CreateAcknowledge(), ConsumeToken)` |

#### 5.1.2 `SendEnvelopedAsync` (eski `SendEnvelopedAndGetAck`)

**Şu anda:**
```csharp
public Task<HorseResult> SendEnvelopedAndGetAck(IEnumerable<HorseMessage> messages, 
    string envelopeTarget = null,
    IList<KeyValuePair<string, string>> additionalHeaders = null,
    CancellationToken cancellationToken = default)
```

**Önerilen — yeniden adlandırılıyor:**
```csharp
public Task<HorseResult> SendEnvelopedAsync(IEnumerable<HorseMessage> messages, CancellationToken cancellationToken)
public Task<HorseResult> SendEnvelopedAsync(IEnumerable<HorseMessage> messages, 
    string envelopeTarget, CancellationToken cancellationToken)
public Task<HorseResult> SendEnvelopedAsync(IEnumerable<HorseMessage> messages, 
    string envelopeTarget, IList<KeyValuePair<string, string>> additionalHeaders, 
    CancellationToken cancellationToken)
```

#### 5.1.3 `SendPluginMessage<T>` → `SendPluginAsync<T>`

**Şu anda:**
```csharp
public async Task<HorseModelResult<T>> SendPluginMessage<T>(string pluginName, object requestModel, 
    ushort contentType = 0)
```
CT hiç yok, isim tutarsız.

**Önerilen — yeniden adlandırılıyor:**
```csharp
public Task<HorseModelResult<T>> SendPluginAsync<T>(string pluginName, object requestModel, 
    CancellationToken cancellationToken)
public Task<HorseModelResult<T>> SendPluginAsync<T>(string pluginName, object requestModel, 
    ushort contentType, CancellationToken cancellationToken)
```

#### 5.1.4 `Request` — Gönder + ham response bekle

**Şu anda:**
```csharp
public Task<HorseMessage> Request(HorseMessage message, CancellationToken cancellationToken = default)
```

**Önerilen:**
```csharp
public Task<HorseMessage> Request(HorseMessage message, CancellationToken cancellationToken)
```
Tek overload yeterli. Sadece `= default` kaldırılıyor.

**Etki:**
- `DirectOperator.Request` 4 yerde — zaten CT geçiyor ✅
- `RouterOperator.PublishRequest` 2 yerde — zaten CT geçiyor ✅
- `HorseTransaction.Begin/Commit/Rollback` — CT geçmiyor! ← düzeltilmeli

#### 5.1.5 `SendAck` / `SendNegativeAck` — Acknowledge gönder

**Şu anda:**
```csharp
public Task<HorseResult> SendAck(HorseMessage message)
public async Task<HorseResult> SendNegativeAck(HorseMessage message, string reason = null)
```
İkisinde de CT yok.

**Önerilen:**
```csharp
// SendAck
public Task<HorseResult> SendAck(HorseMessage message, CancellationToken cancellationToken)

// SendNegativeAck — reason optional → overload
public Task<HorseResult> SendNegativeAck(HorseMessage message, CancellationToken cancellationToken)
public Task<HorseResult> SendNegativeAck(HorseMessage message, string reason, CancellationToken cancellationToken)
```

**Etki:**
- `SendResponse(msg)` → `SendAck(msg, ct)` — CT almalı
- `SendNegativeResponse(msg, reason)` → `SendNegativeAck(msg, reason, ct)` — CT almalı
- Test'lerde consumer callback içinde `client.SendAck(m)` çağrıları — CT eklenmeli

#### 5.1.6 `SendResponse` / `SendNegativeResponse` — Alias'lar

**Şu anda:**
```csharp
public Task<HorseResult> SendResponse(HorseMessage message)
public Task<HorseResult> SendNegativeResponse(HorseMessage message, string reason = null)
```

**Önerilen:**
```csharp
public Task<HorseResult> SendResponse(HorseMessage message, CancellationToken cancellationToken)
public Task<HorseResult> SendNegativeResponse(HorseMessage message, CancellationToken cancellationToken)
public Task<HorseResult> SendNegativeResponse(HorseMessage message, string reason, CancellationToken cancellationToken)
```

#### 5.1.7 `SendResponseAsync` — Typed/string/stream response

**Şu anda:**
```csharp
public async Task<HorseResult> SendResponseAsync<TModel>(HorseMessage requestMessage, TModel responseModel)
public async Task<HorseResult> SendResponseAsync(HorseMessage requestMessage, string responseContent)
public async Task<HorseResult> SendResponseAsync(HorseMessage requestMessage, Stream content)
```
Hiçbirinde CT yok.

**Önerilen:**
```csharp
public Task<HorseResult> SendResponseAsync<TModel>(HorseMessage requestMessage, TModel responseModel, CancellationToken cancellationToken)
public Task<HorseResult> SendResponseAsync(HorseMessage requestMessage, string responseContent, CancellationToken cancellationToken)
public Task<HorseResult> SendResponseAsync(HorseMessage requestMessage, Stream content, CancellationToken cancellationToken)
```

**Etki:**
- `RequestHandlerExecutor.cs:65,90,102` — `client.SendAsync(responseMessage)` → `client.SendResponseAsync(...)` kullanmalı ve CT geçmeli

#### 5.1.8 `WaitResponse` — Internal backbone

**Şu anda:**
```csharp
protected internal Task<HorseResult> WaitResponse(HorseMessage message, bool waitForResponse,
    CancellationToken cancellationToken = default)
```

**Önerilen:**
```csharp
protected internal Task<HorseResult> WaitResponse(HorseMessage message, bool waitForResponse,
    CancellationToken cancellationToken)
```
`= default` kaldırılıyor. Bu metot tüm operator'ların temel taşı.

**Etki:**
- Tüm operator'lar zaten CT geçiyor ✅
- `EventOperator.Subscribe/Unsubscribe` → CT geçmiyor! `Client.WaitResponse(message, verifyResponse)` → 3. argüman eksik
- `RouterOperator.Create/Remove/AddBinding/RemoveBinding` → CT geçmiyor! `_client.WaitResponse(message, true)` → 3. argüman eksik
- `QueueOperator.Create/Remove/SetOptions/ClearMessages` → CT geçmiyor!

---

### 5.1 ÖZET — HorseClient Yeni Core API

Kaldırılan metotlar:
- ❌ `SendAndGetAck` (tüm overload'lar)
- ❌ `SendAndGet<T>`
- ❌ `SendEnvelopedAndGetAck` (yeniden adlandırıldı)
- ❌ `SendPluginMessage<T>` (yeniden adlandırıldı)

Yeni/güncellenen metotlar:
```csharp
// ── SendAsync ailesi (5 overload) ──
Task<HorseResult> SendAsync(HorseMessage message, CancellationToken cancellationToken)
Task<HorseResult> SendAsync(HorseMessage message, IList<KVP> additionalHeaders, CancellationToken cancellationToken)
Task<HorseResult> SendAsync(HorseMessage message, bool waitForAcknowledge, CancellationToken cancellationToken)
Task<HorseResult> SendAsync(HorseMessage message, bool waitForAcknowledge, IList<KVP> additionalHeaders, CancellationToken cancellationToken)
Task<HorseModelResult<T>> SendAsync<T>(HorseMessage message, CancellationToken cancellationToken)

// ── Envelope ──
Task<HorseResult> SendEnvelopedAsync(IEnumerable<HorseMessage> messages, CancellationToken cancellationToken)
Task<HorseResult> SendEnvelopedAsync(..., string envelopeTarget, CancellationToken cancellationToken)
Task<HorseResult> SendEnvelopedAsync(..., string envelopeTarget, IList<KVP> additionalHeaders, CancellationToken cancellationToken)

// ── Plugin ──
Task<HorseModelResult<T>> SendPluginAsync<T>(string pluginName, object requestModel, CancellationToken cancellationToken)
Task<HorseModelResult<T>> SendPluginAsync<T>(string pluginName, object requestModel, ushort contentType, CancellationToken cancellationToken)

// ── Request ──
Task<HorseMessage> Request(HorseMessage message, CancellationToken cancellationToken)

// ── Ack/Response ──
Task<HorseResult> SendAck(HorseMessage message, CancellationToken cancellationToken)
Task<HorseResult> SendNegativeAck(HorseMessage message, CancellationToken cancellationToken)
Task<HorseResult> SendNegativeAck(HorseMessage message, string reason, CancellationToken cancellationToken)
Task<HorseResult> SendResponse(HorseMessage message, CancellationToken cancellationToken)
Task<HorseResult> SendNegativeResponse(HorseMessage message, CancellationToken cancellationToken)
Task<HorseResult> SendNegativeResponse(HorseMessage message, string reason, CancellationToken cancellationToken)
Task<HorseResult> SendResponseAsync<TModel>(HorseMessage requestMessage, TModel responseModel, CancellationToken cancellationToken)
Task<HorseResult> SendResponseAsync(HorseMessage requestMessage, string responseContent, CancellationToken cancellationToken)
Task<HorseResult> SendResponseAsync(HorseMessage requestMessage, Stream content, CancellationToken cancellationToken)

// ── Internal backbone (= default kaldırıldı) ──
protected internal Task<HorseResult> WaitResponse(HorseMessage message, bool waitForResponse, CancellationToken cancellationToken)
```

---

### 5.2 Senkron Metotlar (CT gerektirmeyen)

Bu metotlar senkron çalışıyor, CT eklenmeyecek:

| Metot | Sebep |
|-------|-------|
| `Send(HorseMessage, IList<KVP>)` | Senkron, fire-and-forget |
| `Send(HorseMessage, Action<bool>)` | Senkron callback |
| `SendBulk(IEnumerable<HorseMessage>, Action)` | Senkron bulk |
| `SendRaw(byte[])` | Ham senkron |
| `SendRaw(ReadOnlySpan<byte>)` | Ham senkron |

### 5.3 Raw Async Metotlar (düşük seviye)

| Metot | Karar |
|-------|-------|
| `SendRawAsync(byte[])` | Socket düzeyinde, CT eklenmeyecek (socket zaten kendisi kapatılınca iptal oluyor) |
| `SendRawAsync(ReadOnlyMemory<byte>)` | Aynı |
| `SendAsync(byte[] rawData)` | Aynı — raw byte gönderme |

---

### 5.4 QueueOperator Overload Stratejisi

#### Push<T> (model)
```csharp
Task<HorseResult> Push<T>(T model, CancellationToken cancellationToken)
Task<HorseResult> Push<T>(T model, bool waitForCommit, CancellationToken cancellationToken)
Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, CancellationToken cancellationToken)
Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
```

#### Push (raw binary)
```csharp
Task<HorseResult> Push(string queue, MemoryStream content, CancellationToken cancellationToken)
Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit, CancellationToken cancellationToken)
Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)

// byte[] convenience
Task<HorseResult> Push(string queue, byte[] data, CancellationToken cancellationToken)
Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit, CancellationToken cancellationToken)
Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
Task<HorseResult> Push(string queue, byte[] data, string messageId, bool waitForCommit, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
```

#### Subscribe / Unsubscribe
```csharp
Task<HorseResult> Subscribe(string queue, bool verifyResponse, CancellationToken cancellationToken)
Task<HorseResult> Subscribe(string queue, bool verifyResponse, 
    IEnumerable<KeyValuePair<string, string>> headers, CancellationToken cancellationToken)

Task<HorseResult> Unsubscribe(string queue, bool verifyResponse, CancellationToken cancellationToken)
Task<HorseResult> UnsubscribeFromAllQueues(CancellationToken cancellationToken)
```

#### SubscribePartitioned
```csharp
Task<HorseResult> SubscribePartitioned(string queue, string partitionLabel, bool verifyResponse, CancellationToken cancellationToken)
Task<HorseResult> SubscribePartitioned(string queue, string partitionLabel, bool verifyResponse, 
    int? maxPartitions, int? subscribersPerPartition, CancellationToken cancellationToken)
Task<HorseResult> SubscribePartitioned(string queue, string partitionLabel, bool verifyResponse, 
    int? maxPartitions, int? subscribersPerPartition, 
    IEnumerable<KeyValuePair<string, string>> additionalHeaders, CancellationToken cancellationToken)
```

#### Pull
```csharp
Task<PullContainer> Pull(PullRequest request, CancellationToken cancellationToken)
Task<PullContainer> Pull(PullRequest request, 
    Func<int, HorseMessage, Task> actionForEachMessage, CancellationToken cancellationToken)
```

#### Admin metotları
```csharp
Task<HorseResult> Create(string queue, CancellationToken cancellationToken)
Task<HorseResult> Create(string queue, Action<QueueOptions> optionsAction, CancellationToken cancellationToken)
Task<HorseResult> Create(string queue, Action<QueueOptions> optionsAction, string queueManagerName, CancellationToken cancellationToken)
Task<HorseResult> Create(string queue, Action<QueueOptions> optionsAction, string queueManagerName, 
    IEnumerable<KeyValuePair<string, string>> additionalHeaders, CancellationToken cancellationToken)

Task<HorseModelResult<List<QueueInformation>>> List(CancellationToken cancellationToken)
Task<HorseModelResult<List<QueueInformation>>> List(string filter, CancellationToken cancellationToken)

Task<HorseResult> Remove(string queue, CancellationToken cancellationToken)
Task<HorseResult> SetOptions(string queue, Action<QueueOptions> optionsAction, CancellationToken cancellationToken)
Task<HorseResult> ClearMessages(string queue, bool clearPriorityMessages, bool clearMessages, CancellationToken cancellationToken)
Task<HorseModelResult<List<ClientInformation>>> GetConsumers(string queue, CancellationToken cancellationToken)
```

---

### 5.5 IHorseQueueBus Overload Stratejisi

```csharp
// Push — raw
Task<HorseResult> Push(string queue, MemoryStream content, CancellationToken cancellationToken);
Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit, CancellationToken cancellationToken);
Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken);
Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken);

// Push — model  
Task<HorseResult> Push<T>(T model, CancellationToken cancellationToken) where T : class;
Task<HorseResult> Push<T>(T model, bool waitForCommit, CancellationToken cancellationToken) where T : class;
Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, CancellationToken cancellationToken) where T : class;
Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;
Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;

// Pull
Task<PullContainer> Pull(PullRequest request, CancellationToken cancellationToken);
Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage, CancellationToken cancellationToken);
```

---

### 5.6 IHorseCache Overload Stratejisi

```csharp
// Get
Task<HorseCacheData<TData>> Get<TData>(string key, CancellationToken cancellationToken);
Task<HorseCacheData<string>> GetString(string key, CancellationToken cancellationToken);
Task<HorseCacheData<byte[]>> GetData(string key, CancellationToken cancellationToken);

// GetIncrementalValue
Task<HorseCacheData<int>> GetIncrementalValue(string key, CancellationToken cancellationToken);
Task<HorseCacheData<int>> GetIncrementalValue(string key, int increment, CancellationToken cancellationToken);
Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, CancellationToken cancellationToken);
Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, int increment, CancellationToken cancellationToken);

// Set<TData>
Task<HorseResult> Set<TData>(string key, TData data, CancellationToken cancellationToken);
Task<HorseResult> Set<TData>(string key, TData data, string[] tags, bool persistent, CancellationToken cancellationToken);
Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, CancellationToken cancellationToken);
Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);
Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, TimeSpan warningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

// SetString — aynı pattern
Task<HorseResult> SetString(string key, string data, CancellationToken cancellationToken);
Task<HorseResult> SetString(string key, string data, string[] tags, bool persistent, CancellationToken cancellationToken);
Task<HorseResult> SetString(string key, string data, TimeSpan duration, CancellationToken cancellationToken);
Task<HorseResult> SetString(string key, string data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);
Task<HorseResult> SetString(string key, string data, TimeSpan duration, TimeSpan warningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

// SetData — aynı pattern
Task<HorseResult> SetData(string key, byte[] data, CancellationToken cancellationToken);
Task<HorseResult> SetData(string key, byte[] data, string[] tags, bool persistent, CancellationToken cancellationToken);
Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, CancellationToken cancellationToken);
Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);
Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, TimeSpan warningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

// List / Remove / Purge
Task<HorseModelResult<List<CacheInformation>>> List(CancellationToken cancellationToken);
Task<HorseModelResult<List<CacheInformation>>> List(string filter, CancellationToken cancellationToken);
Task<HorseResult> Remove(string key, CancellationToken cancellationToken);
Task<HorseResult> Purge(CancellationToken cancellationToken);
Task<HorseResult> PurgeByTag(string tag, CancellationToken cancellationToken);
```

---

### 5.7 ChannelOperator Overload Stratejisi

```csharp
// Create
Task<HorseResult> Create(string channel, CancellationToken cancellationToken);
Task<HorseResult> Create(string channel, bool verifyResponse, CancellationToken cancellationToken);
Task<HorseResult> Create(string channel, Action<ChannelOptions> options, bool verifyResponse, CancellationToken cancellationToken);

// Delete
Task<HorseResult> Delete(string channel, CancellationToken cancellationToken);
Task<HorseResult> Delete(string channel, bool verifyResponse, CancellationToken cancellationToken);

// Subscribe / Unsubscribe
Task<HorseResult> Subscribe(string channel, bool verifyResponse, CancellationToken cancellationToken);
Task<HorseResult> Subscribe(string channel, bool verifyResponse, 
    IEnumerable<KeyValuePair<string, string>> headers, CancellationToken cancellationToken);
Task<HorseResult> Unsubscribe(string channel, bool verifyResponse, CancellationToken cancellationToken);
Task<HorseResult> UnsubscribeFromAllChannels(CancellationToken cancellationToken);

// List / GetSubscribers
Task<HorseModelResult<List<ChannelInformation>>> List(CancellationToken cancellationToken);
Task<HorseModelResult<List<ChannelInformation>>> List(string filter, CancellationToken cancellationToken);
Task<HorseModelResult<List<ClientInformation>>> GetSubscribers(string channelName, CancellationToken cancellationToken);

// Publish
Task<HorseResult> Publish(object jsonObject, CancellationToken cancellationToken);
Task<HorseResult> Publish(object jsonObject, bool waitAcknowledge, CancellationToken cancellationToken);
Task<HorseResult> Publish(string channel, object jsonObject, bool waitAcknowledge, CancellationToken cancellationToken);
Task<HorseResult> Publish(string channel, object jsonObject, bool waitAcknowledge, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

Task<HorseResult> PublishString(string channel, string content, CancellationToken cancellationToken);
Task<HorseResult> PublishString(string channel, string content, bool waitAcknowledge, CancellationToken cancellationToken);
Task<HorseResult> PublishString(string channel, string content, bool waitAcknowledge, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

Task<HorseResult> PublishData(string channel, MemoryStream content, CancellationToken cancellationToken);
Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitAcknowledge, CancellationToken cancellationToken);
Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitAcknowledge, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
```

---

### 5.8 DirectOperator Overload Stratejisi

```csharp
// Send — model
Task<HorseResult> Send<T>(T model, bool waitAcknowledge, CancellationToken cancellationToken);
Task<HorseResult> Send<T>(T model, bool waitAcknowledge, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

// SendById — model
Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitAcknowledge, CancellationToken cancellationToken);
Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitAcknowledge, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

// SendByName / SendByType — model (delegate to SendById)
Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitAcknowledge, CancellationToken cancellationToken);
Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitAcknowledge, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitAcknowledge, CancellationToken cancellationToken);
Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitAcknowledge, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

// SendAsync — raw  
Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge, CancellationToken cancellationToken);
Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

// Request
Task<HorseResult<TResponse>> Request<TResponse>(object model, CancellationToken cancellationToken);
Task<HorseResult<TResponse>> Request<TResponse>(object model, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object model, CancellationToken cancellationToken);
Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object model, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

Task<HorseMessage> Request(string target, ushort contentType, CancellationToken cancellationToken);
Task<HorseMessage> Request(string target, ushort contentType, string content, CancellationToken cancellationToken);
Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content, CancellationToken cancellationToken);
Task<HorseMessage> Request(string target, ushort contentType, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
Task<HorseMessage> Request(string target, ushort contentType, string content, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
```

---

### 5.9 RouterOperator Overload Stratejisi

```csharp
// Admin
Task<HorseResult> Create(string name, RouteMethod method, CancellationToken cancellationToken);
Task<HorseModelResult<List<RouterInformation>>> List(CancellationToken cancellationToken);
Task<HorseResult> Remove(string name, CancellationToken cancellationToken);
Task<HorseResult> AddBinding(string routerName, string type, string name, string target, 
    BindingInteraction interaction, CancellationToken cancellationToken);
Task<HorseResult> AddBinding(string routerName, string type, string name, string target, 
    BindingInteraction interaction, RouteMethod bindingMethod, ushort? contentType, int priority, CancellationToken cancellationToken);
Task<HorseModelResult<List<BindingInformation>>> GetBindings(string routerName, CancellationToken cancellationToken);
Task<HorseResult> RemoveBinding(string routerName, string bindingName, CancellationToken cancellationToken);

// Publish — raw
Task<HorseResult> Publish(string routerName, byte[] data, CancellationToken cancellationToken);
Task<HorseResult> Publish(string routerName, byte[] data, bool waitForAcknowledge, CancellationToken cancellationToken);
Task<HorseResult> Publish(string routerName, byte[] data, string messageId, bool waitForAcknowledge, 
    ushort contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

// Publish — model
Task<HorseResult> Publish<T>(T model, CancellationToken cancellationToken) where T : class;
Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class;
Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class;
Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;
Task<HorseResult> Publish<T>(string routerName, T model, ushort? contentType, bool waitForAcknowledge, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;
Task<HorseResult> Publish<T>(string routerName, T model, string messageId, ushort? contentType, bool waitForAcknowledge, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

// PublishRequest
Task<HorseMessage> PublishRequest(string routerName, string message, CancellationToken cancellationToken);
Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType, CancellationToken cancellationToken);
Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken);
Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request, CancellationToken cancellationToken);
Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request, ushort? contentType, 
    IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
```

---

### 5.10 EventOperator Overload Stratejisi

```csharp
Task<HorseResult> Subscribe(HorseEventType eventType, string target, CancellationToken cancellationToken);
Task<HorseResult> Subscribe(HorseEventType eventType, string target, bool verifyResponse, CancellationToken cancellationToken);
Task<HorseResult> Unsubscribe(HorseEventType eventType, string target, CancellationToken cancellationToken);
Task<HorseResult> Unsubscribe(HorseEventType eventType, string target, bool verifyResponse, CancellationToken cancellationToken);
```

---

### 5.11 HorseTransaction Overload Stratejisi

```csharp
public Task Begin(CancellationToken cancellationToken);
public Task Begin<TModel>(TModel model, CancellationToken cancellationToken);
public Task<bool> Commit(CancellationToken cancellationToken);
public Task<bool> Rollback(CancellationToken cancellationToken);

// static factory
public static Task<HorseTransaction> Begin(HorseClient client, string name, CancellationToken cancellationToken);
```

`Dispose` → `Rollback(client.ConsumeToken)` çağıracak.

---

### 5.12 ConnectionOperator Overload Stratejisi

```csharp
Task<HorseModelResult<List<NodeInformation>>> GetInstances(CancellationToken cancellationToken);
Task<HorseModelResult<List<ClientInformation>>> GetConnectedClients(CancellationToken cancellationToken);
Task<HorseModelResult<List<ClientInformation>>> GetConnectedClients(string typeFilter, CancellationToken cancellationToken);
```

---

### 5.13 Consumer/Handler Interface'leri

```csharp
// IQueueConsumer<TModel>
Task Consume(HorseMessage message, TModel model, HorseClient client, CancellationToken cancellationToken);

// IChannelSubscriber<TModel>
Task Handle(HorseMessage message, TModel model, HorseClient client, CancellationToken cancellationToken);

// IDirectMessageHandler<TModel>
Task Handle(HorseMessage message, TModel model, HorseClient client, CancellationToken cancellationToken);

// IHorseRequestHandler<TRequest, TResponse>
Task<TResponse> Handle(TRequest request, HorseMessage rawMessage, HorseClient client, CancellationToken cancellationToken);

// IHorseInterceptor  
Task Intercept(HorseMessage message, HorseClient client, CancellationToken cancellationToken);
```

---

## BÖLÜM 6: CancellationToken.None ve GracefulShutdown

### GracefulShutdownService
```csharp
// MEVCUT:
await Task.Delay(min, CancellationToken.None);
// ÖNERİ: kendi shutdown CTS'i oluştur
using var shutdownCts = new CancellationTokenSource(maxWait);
await Task.Delay(min, shutdownCts.Token);
```

### HorseClientBuilder.Shutdown
```csharp
// Aynı pattern — kendi CTS'i oluşturulmalı
```

---

## BÖLÜM 7: UYGULAMA PLANI

### Adım 1: HorseClient Core (temel taşı — önce bu değişmeli)

HorseClient tüm zincirin başlangıç noktası. 

**Kaldırılacaklar:**
- ❌ `SendAndGetAck` (tüm overload'lar) — `SendAsync(msg, true, ct)` ile değişecek
- ❌ `SendAndGet<T>` — `SendAsync<T>(msg, ct)` ile değişecek
- ❌ `SendEnvelopedAndGetAck` → `SendEnvelopedAsync` olarak yeniden adlandırılacak
- ❌ `SendPluginMessage<T>` → `SendPluginAsync<T>` olarak yeniden adlandırılacak

**Yeni SendAsync ailesi oluşturulacak:**
1. `SendAsync(HorseMessage, CT)` — fire-and-forget
2. `SendAsync(HorseMessage, IList<KVP>, CT)` — headers ile fire-and-forget
3. `SendAsync(HorseMessage, bool waitForAcknowledge, CT)` — ack bekleme (eski SendAndGetAck)
4. `SendAsync(HorseMessage, bool waitForAcknowledge, IList<KVP>, CT)` — headers + ack bekleme
5. `SendAsync<T>(HorseMessage, CT)` — typed response (eski SendAndGet<T>)

**Diğer güncellemeler:**
6. `SendEnvelopedAsync` — 3 overload
7. `SendPluginAsync<T>` — 2 overload
8. `Request(msg, CT)` — `= default` kaldır
9. `WaitResponse(msg, wait, CT)` — `= default` kaldır
10. `SendAck(msg, CT)` — CT ekle
11. `SendNegativeAck` — 2 overload (reason ile/siz)
12. `SendResponse` / `SendNegativeResponse` — CT ekle
13. `SendResponseAsync` 3 overload — CT ekle
14. Internal AutoAcknowledge → `ConsumeToken` kullan

**Build kıracak:** Tüm operator'lar, tüm bus'lar, tüm test/sample projeler

### Adım 2: Operator sınıfları (consumer-facing API)

Her operator'ı Bölüm 5'teki overload planına göre güncelle:

1. `QueueOperator` — Push (model 5 + raw 8 + byte 4), Subscribe 2, SubscribePartitioned 3, Pull 2, Admin 10 = **~34 overload**
2. `ChannelOperator` — Create 3, Delete 2, Sub/Unsub 4, List/GetSubs 3, Publish 10 = **~22 overload**
3. `DirectOperator` — Send 2, SendById 2, SendByName 2, SendByType 2, SendAsync 2, Request 10 = **~20 overload**
4. `RouterOperator` — Admin 7, Publish raw 3, Publish model 6, PublishRequest 7 = **~23 overload**
5. `EventOperator` — Subscribe 2, Unsubscribe 2 = **4 overload**
6. `ConnectionOperator` — 3 overload

### Adım 3: Interface'ler (breaking change)

1. `IHorseQueueBus` — Push (raw 4 + model 5), Pull 2, PushBulk (değişmez, void) = **~11**
2. `IHorseChannelBus` — Create, Delete, Publish 4, PublishString 3, PublishData 3 = **~13**
3. `IHorseDirectBus` — Send/SendById/SendByName/SendByType/SendAsync + Request = **~20**
4. `IHorseRouterBus` — Publish + PublishRequest = **~13**
5. `IHorseCache` — Get 3, GetIncremental 4, Set 5x3, List 2, Remove 1, Purge 2 = **~27**
6. `IQueueConsumer<T>` — Consume metodu CT ekle
7. `IChannelSubscriber<T>` — Handle CT ekle
8. `IDirectMessageHandler<T>` — Handle CT ekle
9. `IHorseRequestHandler<TReq,TRes>` — Handle CT ekle
10. `IHorseInterceptor` — Intercept CT ekle

### Adım 4: Bus implementasyonları

`HorseQueueBus`, `HorseChannelBus`, `HorseDirectBus`, `HorseRouterBus`, `HorseCache`
— interface değişikliklerini karşıla, operator çağrılarına CT geçir

### Adım 5: HorseTransaction

`Begin`, `Commit`, `Rollback` → CT ekle
`Dispose` → `Rollback(client.ConsumeToken)` 
`static Begin` → CT ekle

### Adım 6: Internal executor'lar

- `QueueConsumerExecutor` — `client.SendAndGetAck(clone)` → CT geçir
- `RequestHandlerExecutor` — `client.SendAsync(responseMessage)` → CT geçir
- `DirectHandlerExecutor` — CT propagation
- `ChannelSubscriberExecutor` — CT propagation
- `EventSubscriberExecutor` — CT propagation
- `InterceptorRunner` — CT propagation

### Adım 7: GracefulShutdown

- `GracefulShutdownService.cs` → kendi `CancellationTokenSource` oluştur
- `HorseClientBuilder.Shutdown` → aynı

### Adım 8: Extensions.Client projesi

- `HorseConnectService`, `HorseRegistrar` vb. — operator API değişikliklerini karşıla

### Adım 9: Server projesi (NodeClient vb.)

- `NodeClient.SendMessage` → `_outgoingClient.SendAsync(message)` CT geçilmeli

### Adım 10: Test projeleri

- `Test.Common/TestCancellation.cs` utility oluştur (timeout'lu CTS)
- Tüm test'lerde `TestCancellation.Token` kullan
- ~900+ call-site güncelle

### Adım 11: Sample ve Benchmark projeleri

- Sample'larda `client.ConsumeToken` veya kendi CTS
- Benchmark'larda kendi CTS

---

## Etki Analizi

| Değişiklik | Dosya sayısı | Tahmini call-site etkisi |
|-----------|-------------|-------------------------|
| HorseClient core | 1 | Tüm operator'lar + bus'lar |
| Operator sınıfları | 6 | Tüm çağıranlar |
| Interface'ler | ~10 | Tüm implementasyonlar + kullanıcı kodları |
| Bus implementasyonları | ~5 | Transparent |
| Transaction | 1 | Transaction kullanıcıları |
| Internal executor'lar | ~5 | Internal, dış etkisi yok |
| Extensions.Client | ~3 | Internal wiring |
| Server (NodeClient) | 1 | Internal |
| Test projeleri | ~50+ dosya | ~900+ call site |
| Sample/Benchmark | ~15 dosya | ~30 call site |

**Toplam tahmini: ~100 dosya, ~1000 call-site değişikliği**

