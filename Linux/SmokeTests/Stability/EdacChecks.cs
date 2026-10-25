using ResetExplode.Hardware;

internal static class EdacChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public static void Run()
    {
        string root = Directory.CreateTempSubdirectory("reset-edac-").FullName;
        void Write(string path, string text)
        {
            string full = Path.Combine(root, "sys/devices/system/edac/mc", path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }
        try
        {
            var collector = new LinuxEdacCollector(new LinuxFiles(root));
            Check(collector.Collect().Controllers.Count == 0 && collector.Collect().Status.Contains("indisponível"), "Ausência de EDAC virou aprovação.");
            Write("mc0/mc_name", "Controlador de teste");
            Write("mc0/ce_count", "0"); Write("mc0/ue_count", "0"); Write("mc0/seconds_since_reset", "100");
            Write("mc0/dimm0/dimm_ce_count", "999"); // Não somar controladores e filhos.
            Write("mc_wrong/ce_count", "888");
            var zero = collector.Collect();
            Check(zero.Controllers.Count == 1 && zero.Controllers[0].Corrected == 0 && zero.Status.Contains("Zero erros reportados"), "Zero real ou filtro incorreto.");
            Write("mc0/ce_count", ulong.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write("mc0/ue_count", "3");
            var errors = collector.Collect();
            Check(errors.Controllers[0].Corrected == ulong.MaxValue && errors.Controllers[0].Uncorrected == 3 && errors.Status.Contains("Há erros"), "Erros/uint64 não preservados.");
            Check(zero.Controllers[0].Corrected == 0, "Snapshot anterior alterado.");
            Write("mc0/ce_count", "-1"); Write("mc0/ue_count", "18446744073709551616"); Write("mc0/seconds_since_reset", "abc");
            var invalid = collector.Collect();
            Check(!invalid.Controllers[0].Complete && invalid.Controllers[0].SecondsSinceReset == null && invalid.Status.Contains("incompleta"), "Dados inválidos viraram zeros.");
            Write("mc0/ce_count", "0"); Write("mc0/ue_count", "0"); Write("mc0/seconds_since_reset", "2");
            Check(collector.Collect().Controllers[0].SecondsSinceReset == 2, "Reset reportado não atualizou.");
            File.Delete(Path.Combine(root, "sys/devices/system/edac/mc/mc0/ue_count"));
            Check(collector.Collect().Controllers[0].Uncorrected == null, "Contador removido ficou antigo.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { collector.Collect(cancelled.Token); throw new InvalidOperationException("Ignorou cancelamento."); }
            catch (OperationCanceledException) { }
            Console.WriteLine("OK: EDAC ausente, zeros, erros, uint64, dados inválidos, reset, remoção e snapshots.");
        }
        finally { Directory.Delete(root, true); }
    }
}
