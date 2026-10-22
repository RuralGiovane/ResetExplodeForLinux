using System.Globalization;
using ResetExplode.Hardware;

internal static class PowerChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "reset-power-" + Guid.NewGuid().ToString("N"));
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            void Write(string path, string value)
            {
                string target = Path.Combine(root, "sys/class/power_supply", path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, value);
            }
            var collector = new LinuxPowerCollector(new LinuxFiles(root));
            Check(collector.Collect().Supplies.Count == 0 && collector.Collect().Notices.Count > 0, "Ausência não informada.");
            Write("BAT0/type", "Battery"); Write("BAT0/present", "1"); Write("BAT0/scope", "System");
            Write("BAT0/status", "Discharging"); Write("BAT0/capacity", "0"); Write("BAT0/cycle_count", "0");
            Write("BAT0/energy_now", "12500000"); Write("BAT0/charge_full", "5000000");
            Write("BAT0/voltage_now", "12000000"); Write("BAT0/current_now", "-1500000");
            Write("BAT0/power_now", "18000000"); Write("BAT0/temp", "-50");
            Write("AC/type", "Mains"); Write("AC/online", "1");
            Write("mouse/type", "Battery"); Write("mouse/scope", "Device"); Write("mouse/capacity", "150");
            Write("BAT1/type", "Battery"); Write("BAT1/present", "0"); Write("BAT1/capacity", "80");
            var snapshot = collector.Collect();
            var battery = snapshot.Supplies.Single(s => s.Name == "BAT0");
            string Value(string label) => battery.Metrics.Single(m => m.Name == label).Value;
            Check(Value("Energia atual") == "12,50 Wh" && Value("Carga elétrica completa") == "5,00 Ah", "Unidades de energia/carga incorretas.");
            Check(Value("Tensão") == "12,00 V" && Value("Corrente reportada") == "-1,50 A" && Value("Temperatura") == "-5,00 °C", "Escalas ou sinais perdidos.");
            Check(Value("Carga reportada") == "0%" && Value("Ciclos reportados") == "0", "Zeros válidos descartados.");
            Check(Value("Energia de projeto") == "Indisponível", "Dado ausente foi inventado.");
            Check(snapshot.Supplies.Single(s => s.Name == "AC").Metrics.Any(m => m.Name == "Online" && m.Value == "Sim"), "Estado da fonte perdido.");
            var peripheral = snapshot.Supplies.Single(s => s.Name == "mouse");
            Check(peripheral.Scope == "Periférico" && peripheral.Metrics.Any(m => m.Name == "Carga reportada" && m.Value == "Indisponível"), "Escopo ou percentual inválido incorreto.");
            Check(snapshot.Supplies.Single(s => s.Name == "BAT1").Metrics.Count == 1, "Bateria ausente mostrou métricas antigas.");
            Write("BAT0/status", "Charging"); Write("BAT0/capacity", "45"); Write("AC/online", "0");
            var changed = collector.Collect();
            Check(changed.Supplies.Single(s => s.Name == "BAT0").Metrics.Any(m => m.Name == "Estado" && m.Value == "Carregando") &&
                changed.Supplies.Single(s => s.Name == "BAT0").Metrics.Any(m => m.Name == "Carga reportada" && m.Value == "45%"),
                "Nova coleta manteve estado/carga antigos.");
            Check(changed.Supplies.Single(s => s.Name == "AC").Metrics.Any(m => m.Name == "Online" && m.Value == "Não"),
                "Estado da fonte foi inferido a partir do estado da bateria.");
            Check(Value("Estado") == "Descarregando" && Value("Carga reportada") == "0%", "Atualização modificou snapshot anterior.");
            Write("BAT0/energy_now", "-1"); Write("AC/online", "inválido");
            Check(collector.Collect().Supplies.Single(s => s.Name == "BAT0").Metrics.Any(m => m.Name == "Energia atual" && m.Value == "Indisponível"), "Energia negativa aceita.");
            Check(collector.Collect().Supplies.Single(s => s.Name == "AC").Metrics.Any(m => m.Name == "Online" && m.Value == "Indisponível"), "Online inválido aceito.");
            Directory.Delete(Path.Combine(root, "sys/class/power_supply/BAT0"), true);
            Check(!collector.Collect().Supplies.Any(s => s.Name == "BAT0"), "Dispositivo removido persistiu.");
            Write("BAT0/type", "Battery"); Write("BAT0/present", "1"); Write("BAT0/capacity", "60");
            var returned = collector.Collect().Supplies.Single(s => s.Name == "BAT0");
            Check(returned.Metrics.Any(m => m.Name == "Carga reportada" && m.Value == "60%") &&
                returned.Metrics.Any(m => m.Name == "Tensão" && m.Value == "Indisponível"),
                "Dispositivo reaparecido recuperou métricas antigas.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            bool stopped = false;
            try { collector.Collect(cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
            Check(stopped, "Cancelamento ignorado.");
            Console.WriteLine("OK: power_supply, unidades, sinais, zero, ausência, escopo, remoção e cancelamento.");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
