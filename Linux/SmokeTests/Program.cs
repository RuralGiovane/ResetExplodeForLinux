using ResetExplode;
using ResetExplode.Hardware;

if (args.Length == 1 && args[0] == "--memory-cleanup-only")
{
    MemoryCleanupChecks.Run();
    return;
}
if (args.Length == 1 && args[0] == "--stability-only")
{
    EdacChecks.Run();
    await KernelLogChecks.RunAsync();
    KernelLogClassificationChecks.Run();
    StabilitySessionChecks.Run();
    StabilityReportChecks.Run();
    return;
}
if (args.Length == 1 && args[0] == "--live-stability-session")
{
    var baseline = StabilitySession.Capture();
    var current = StabilitySession.Capture();
    if (baseline.BootId == null || baseline.BootId != current.BootId || current.StartMicroseconds < baseline.EndMicroseconds)
        throw new InvalidOperationException("Identificação/relógio da sessão indisponível.");
    Console.WriteLine($"Relógio monotônico e mesma inicialização confirmados. Controladores EDAC: {current.Edac.Controllers.Count}.");
    Console.WriteLine(StabilitySession.Compare(baseline, current, new(DateTimeOffset.UtcNow, [], "Journal não consultado neste diagnóstico.")).Description);
    return;
}
if (args.Length == 1 && args[0] == "--live-kernel-logs")
{
    var snapshot = await new LinuxKernelLogReader().ReadAsync();
    Console.WriteLine(snapshot.Status);
    Console.WriteLine($"Registros retornados: {snapshot.Entries.Count}. Conteúdo omitido neste diagnóstico.");
    return;
}
if (args.Length == 1 && args[0] == "--live-edac")
{
    var snapshot = new LinuxEdacCollector(new LinuxFiles()).Collect();
    Console.WriteLine(snapshot.Status);
    foreach (var controller in snapshot.Controllers) Console.WriteLine(controller.Description);
    return;
}

if (args.Contains("--live-disk-smoke"))
{
    if (args.Length != 2 || args[0] != "--live-disk-smoke")
    {
        Console.Error.WriteLine("Uso: --live-disk-smoke <pasta existente absoluta>; cria arquivos temporários e executa I/O direto reduzido.");
        Environment.ExitCode = 2; return;
    }
    try { DiskBenchmarkChecks.RunLive(args[1]); }
    catch (NotSupportedException ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 3; }
    catch (OperationCanceledException) { Console.Error.WriteLine("Diagnóstico cancelado."); Environment.ExitCode = 130; }
    catch (Exception ex) { Console.Error.WriteLine(ex.GetBaseException().Message); Environment.ExitCode = 1; }
    return;
}
if (args.Length == 1 && args[0] == "--disk-only")
{
    DiskBenchmarkChecks.Run();
    return;
}

if (args.Length == 1 && args[0] == "--memory-only")
{
    MemoryChecks.Run();
    return;
}

if (args.Length == 2 && args[0] == "--instance-probe")
{
    Environment.ExitCode = SingleInstanceChecks.Probe(args[1]);
    return;
}
if (args.Contains("--instance-only"))
{
    await SingleInstanceChecks.RunAsync();
    return;
}

if (args.Contains("--network-only"))
{
    NetworkChecks.Run();
    NetworkConfigurationChecks.Run();
    await NetworkLatencyChecks.RunAsync();
    await NetworkThroughputChecks.RunAsync();
    await NetworkDnsChecks.RunAsync();
    return;
}
// Opt-in explícito: consome 10 MB de download + 5 MB de upload, mais overhead.
if (args.Length == 1 && args[0] == "--live-throughput")
{
    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    Console.CancelKeyPress += cancel;
    Console.WriteLine("Cloudflare: 10 MB download + 5 MB upload sintético; até 20 s/etapa. Ctrl+C cancela.");
    try
    {
        using var http = NetworkThroughputTest.CreateClient();
        var result = await new NetworkThroughputTest(http).RunAsync(cancellation.Token);
        Console.WriteLine($"Download: {result.Download.MegabitsPerSecond:N2} Mbit/s; {result.Download.Bytes} bytes em {result.Download.Seconds:N3} s");
        Console.WriteLine($"Upload: {result.Upload.MegabitsPerSecond:N2} Mbit/s; {result.Upload.Bytes} bytes em {result.Upload.Seconds:N3} s");
        Console.WriteLine(result.Methodology);
    }
    catch (OperationCanceledException) { Console.Error.WriteLine("Teste cancelado, sem resultado parcial."); Environment.ExitCode = 130; }
    catch (Exception ex) { Console.Error.WriteLine($"Teste indisponível: {ex.GetBaseException().Message}"); Environment.ExitCode = 1; }
    finally { Console.CancelKeyPress -= cancel; }
    return;
}

if (args.Contains("--live-dns"))
{
    if (args.Length != 2 || args[0] != "--live-dns")
    {
        Console.Error.WriteLine("Uso: --live-dns <domínio/host>"); Environment.ExitCode = 2; return;
    }
    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    Console.CancelKeyPress += cancel;
    try
    {
        var result = await new NetworkDnsTest().RunAsync(args[1], cancellation.Token);
        Console.WriteLine($"{result.Host}: {result.ElapsedMilliseconds:N2} ms; {result.Addresses.Count} endereço(s)");
        foreach (string address in result.Addresses) Console.WriteLine(address);
        Console.WriteLine(result.Methodology);
    }
    catch (OperationCanceledException) { Console.Error.WriteLine("Resolução cancelada."); Environment.ExitCode = 130; }
    catch (Exception ex) { Console.Error.WriteLine($"Resolução indisponível: {ex.GetBaseException().Message}"); Environment.ExitCode = 1; }
    finally { Console.CancelKeyPress -= cancel; }
    return;
}

if (args.Contains("--live-latency"))
{
    if (args.Length != 2 || args[0] != "--live-latency")
    {
        Console.Error.WriteLine("Uso: --live-latency <IPv4 ou IPv6>");
        Environment.ExitCode = 2;
        return;
    }
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    Console.CancelKeyPress += cancel;
    try
    {
        var result = await new NetworkLatencyTest().RunAsync(args[1], cancellation.Token);
        Console.WriteLine($"{result.Target}: {result.Received}/{result.Probes.Count} respostas; perda ICMP {result.LossPercent:N1}%");
        Console.WriteLine($"RTT médio: {result.AverageMs?.ToString("N1") ?? "indisponível"} ms; jitter: {result.JitterMs?.ToString("N1") ?? "indisponível"} ms");
        foreach (var group in result.Probes.GroupBy(p => p.Status)) Console.WriteLine($"{group.Key}: {group.Count()}");
    }
    catch (OperationCanceledException) { Console.Error.WriteLine("Teste cancelado."); Environment.ExitCode = 130; }
    catch (Exception ex) { Console.Error.WriteLine($"Teste indisponível: {ex.GetBaseException().Message}"); Environment.ExitCode = 1; }
    finally { Console.CancelKeyPress -= cancel; }
    return;
}

if (args.Contains("--live-network"))
{
    var inventory = new LinuxNetworkCollector(new LinuxFiles()).Collect();
    foreach (var item in inventory.Interfaces)
    {
        Console.WriteLine(item.Name);
        foreach (var metric in item.Metrics) Console.WriteLine($"  {metric.Name}: {metric.Value}");
    }
    Console.WriteLine($"DNS local: {inventory.Resolver.Description}");
    foreach (var notice in inventory.Notices) Console.WriteLine(notice);
    return;
}

// Diagnóstico opt-in: nunca consultar discos na suíte padrão.
if (args.Contains("--live-smart"))
{
    if (args.Length != 2 || args[0] != "--live-smart")
    {
        Console.Error.WriteLine("Uso: --live-smart <nome do dispositivo, por exemplo nvme0n1>");
        Environment.ExitCode = 2;
        return;
    }
    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    Console.CancelKeyPress += cancel;
    try
    {
        var report = await new LinuxSmartReader().ReadAsync(args[1], cancellation.Token);
        Console.WriteLine($"Dispositivo: {report.Device}");
        Console.WriteLine($"Estado: {report.Status}");
        foreach (var metric in report.Metrics) Console.WriteLine($"{metric.Name}: {metric.Value}");
        foreach (var notice in report.Notices) Console.WriteLine(notice);
        // Código 0 indica relatório produzido, não aprovação da saúde do dispositivo.
    }
    catch (ArgumentException)
    {
        Console.Error.WriteLine("Nome inválido. Informe somente o nome do dispositivo, sem /dev/ ou caminho.");
        Environment.ExitCode = 2;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Consulta cancelada.");
        Environment.ExitCode = 130;
    }
    finally { Console.CancelKeyPress -= cancel; }
    return;
}

if (args.Contains("--serial-only"))
{
    SerialChecks.Run();
    return;
}
if (args.Contains("--live-serial"))
{
    var inventory = new LinuxSerialCollector(new LinuxFiles()).Collect();
    foreach (var port in inventory.Ports) Console.WriteLine($"{port.Title} · {port.Details} · {port.HardwareDescription} · {port.AliasDescription}");
    foreach (var group in inventory.Groups) Console.WriteLine(group.Description);
    foreach (var notice in inventory.Notices) Console.WriteLine(notice);
    return;
}
if (args.Contains("--power-only"))
{
    PowerChecks.Run();
    return;
}
if (args.Contains("--smart-only"))
{
    await SmartChecks.RunAsync();
    await SmartProcessChecks.RunAsync();
    return;
}
if (args.Contains("--mounts-only"))
{
    MountChecks.Run();
    return;
}
if (args.Contains("--inventory-only"))
{
    StorageInventoryChecks.Run();
    MountChecks.Run();
    return;
}
if (args.Contains("--report-only"))
{
    ReportChecks.Run();
    StorageReportChecks.Run();
    PowerReportChecks.Run();
    SerialReportChecks.Run();
    NetworkReportChecks.Run();
    return;
}
if (args.Contains("--storage-only"))
{
    PersistenceChecks.Run();
    return;
}
if (args.Contains("--thermal-only"))
{
    await ThermalChecks.RunAsync();
    return;
}
SerialChecks.Run();
StorageInventoryChecks.Run();
MountChecks.Run();
PowerChecks.Run();
TelemetryChecks.Run();
if (args.Contains("--telemetry-only")) return;
if (args.Contains("--live-telemetry"))
{
    using var monitor = new LinuxSensorMonitor(new LinuxFiles(), new LinuxNvml());
    monitor.Read();
    await Task.Delay(1100);
    var snapshot = monitor.Read();
    Console.WriteLine($"CPU: {snapshot.CpuLoadPercent?.ToString("N1") ?? "indisponível"}% · temperatura: {snapshot.CpuTemperature?.ToString("N1") ?? "indisponível"} °C");
    foreach (var sensor in snapshot.Sensors)
        Console.WriteLine($"{sensor.Hardware} · {sensor.Label}: {sensor.DisplayValue} {sensor.Error}");
    foreach (var notice in snapshot.Notices) Console.WriteLine(notice);
    return;
}

NetworkChecks.Run();
NetworkConfigurationChecks.Run();
await NetworkLatencyChecks.RunAsync();
await NetworkThroughputChecks.RunAsync();
await NetworkDnsChecks.RunAsync();
await SingleInstanceChecks.RunAsync();
await SmartChecks.RunAsync();
await SmartProcessChecks.RunAsync();
PersistenceChecks.Run();
ReportChecks.Run();
StorageReportChecks.Run();
PowerReportChecks.Run();
SerialReportChecks.Run();
NetworkReportChecks.Run();
await ThermalChecks.RunAsync();

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task MustCancel(Func<Task> action)
{
    try { await action(); }
    catch (OperationCanceledException) { return; }
    throw new InvalidOperationException("O teste cancelado retornou como concluído.");
}

var runner = new BenchmarkRunner();
MemoryChecks.Run();
MemoryCleanupChecks.Run();
EdacChecks.Run();
await KernelLogChecks.RunAsync();
KernelLogClassificationChecks.Run();
StabilitySessionChecks.Run();
StabilityReportChecks.Run();
DiskBenchmarkChecks.Run();
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    bool started = false;
    await MustCancel(() => runner.RunAsync(_ => { started = true; return 1; }, cancelled.Token));
    Check(!started && !runner.IsRunning, "Um trabalho previamente cancelado foi executado.");
    await MustCancel(() => Task.Run(() => MemoryBench.MeasureLatencyNs(cancelled.Token)));
    await MustCancel(() => Task.Run(() => MemoryBench.MeasureBandwidth(cancelled.Token)));
}

// Cancelamento de um render real, sem reduzir cena, resolução ou amostragem originais.
var renderer = new BenchRenderer();
using (var limit = new CancellationTokenSource())
{
    await MustCancel(() => runner.RunAsync(ct =>
        {
            limit.CancelAfter(TimeSpan.FromMilliseconds(300));
            return renderer.Run(1, TimeSpan.Zero, ct);
        }, limit.Token)
        .WaitAsync(TimeSpan.FromSeconds(30)));
    Check(!runner.IsRunning, "O render deixou o coordenador ocupado.");
    Check(renderer.Active.Length == 1 && renderer.Active.All(x => x == -1),
        "O render não iniciou ou uma thread ficou ativa após o cancelamento.");
}

// Rejeita concorrência e descarta retorno parcial de algoritmos que observam cancelamento.
using (var release = new ManualResetEventSlim())
using (var started = new ManualResetEventSlim())
using (var cancel = new CancellationTokenSource())
{
    var first = runner.RunAsync(_ => { started.Set(); release.Wait(TimeSpan.FromSeconds(10)); return 42; }, cancel.Token);
    try
    {
        Check(started.Wait(TimeSpan.FromSeconds(5)), "O trabalho não iniciou.");
        bool rejected = false;
        try { await runner.RunAsync(_ => 0, CancellationToken.None); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "O coordenador aceitou dois testes simultâneos.");
        cancel.Cancel();
    }
    finally { release.Set(); }
    await MustCancel(() => first);
}

// Uma simulação curta exercita o algoritmo original, incluindo o aquecimento de 2 s.
using (var limit = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
{
    var result = await runner.RunAsync(ct => new GameSimBench().Run(TimeSpan.FromMilliseconds(200), ct), limit.Token);
    Check(result.Frames > 0 && double.IsFinite(result.AvgFps) && result.AvgFps > 0,
        "A simulação não produziu uma medição válida.");
}

Check(await runner.RunAsync(_ => 7, CancellationToken.None) == 7, "O coordenador não foi liberado.");
try { await runner.RunAsync<int>(_ => throw new IOException("Falha de teste"), CancellationToken.None); }
catch (IOException) { }
Check(!runner.IsRunning, "Uma falha deixou o coordenador ocupado.");
Console.WriteLine("OK: pré-cancelamento, render/cancelamento, exclusão mútua, descarte parcial e simulação.");
