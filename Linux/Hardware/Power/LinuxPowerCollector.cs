namespace ResetExplode.Hardware;

public sealed record PowerSupply(string Name, string Type, string Scope, IReadOnlyList<SystemEntry> Metrics);
public sealed record PowerInventory(DateTimeOffset Timestamp, IReadOnlyList<PowerSupply> Supplies, IReadOnlyList<string> Notices);

/// <summary>Somente leitura de power_supply; sem políticas de carga ou inferência de saúde.</summary>
public sealed class LinuxPowerCollector(LinuxFiles files)
{
    public PowerInventory Collect(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var supplies = new List<PowerSupply>();
        foreach (string path in files.Directories("/sys/class/power_supply", "*"))
        {
            cancellation.ThrowIfCancellationRequested();
            string? Read(string key) => files.Read(path + "/" + key);
            long? Number(string key) => LinuxFiles.Integer(Read(key));
            var metrics = new List<SystemEntry>();
            void Text(string label, string? value) => metrics.Add(new(label, string.IsNullOrWhiteSpace(value) ? "Indisponível" : value));
            void Measure(string label, string key, double divisor, string unit, bool signed = false)
            {
                long? value = Number(key);
                Text(label, value is long n && (signed || n >= 0) ? $"{n / divisor:N2} {unit}" : null);
            }
            string Flag(string? value) => value switch { "0" => "Não", "1" => "Sim", _ => "Indisponível" };
            string type = Read("type") ?? "Indisponível";
            string scope = Read("scope") switch { "System" => "Sistema", "Device" => "Periférico", _ => "Escopo indisponível" };
            if (type == "Battery")
            {
                string? present = Read("present");
                Text("Presente", Flag(present));
                if (present != "0")
                {
                    Text("Estado", Read("status") switch
                    {
                        "Charging" => "Carregando", "Discharging" => "Descarregando", "Full" => "Carga completa",
                        "Not charging" => "Não carregando", _ => null
                    });
                    long? capacity = Number("capacity");
                    Text("Carga reportada", capacity is >= 0 and <= 100 ? $"{capacity}%" : null);
                    Text("Modelo", Read("model_name"));
                    Text("Fabricante", Read("manufacturer"));
                    Text("Tecnologia", Read("technology"));
                    long? cycles = Number("cycle_count");
                    Text("Ciclos reportados", cycles is >= 0 ? cycles.Value.ToString("N0") : null);
                    Measure("Energia atual", "energy_now", 1_000_000, "Wh");
                    Measure("Energia em carga completa", "energy_full", 1_000_000, "Wh");
                    Measure("Energia de projeto", "energy_full_design", 1_000_000, "Wh");
                    Measure("Carga elétrica atual", "charge_now", 1_000_000, "Ah");
                    Measure("Carga elétrica completa", "charge_full", 1_000_000, "Ah");
                    Measure("Carga elétrica de projeto", "charge_full_design", 1_000_000, "Ah");
                    Measure("Tensão", "voltage_now", 1_000_000, "V");
                    Measure("Corrente reportada", "current_now", 1_000_000, "A", signed: true);
                    Measure("Potência reportada", "power_now", 1_000_000, "W");
                    Measure("Temperatura", "temp", 10, "°C", signed: true);
                }
            }
            else
            {
                // Online é um atributo desta fonte, não uma conclusão sobre a alimentação de todo o PC.
                Text("Online", Flag(Read("online")));
                Measure("Tensão", "voltage_now", 1_000_000, "V");
                Measure("Corrente reportada", "current_now", 1_000_000, "A", signed: true);
            }
            supplies.Add(new(Path.GetFileName(path), type, scope, metrics));
        }
        cancellation.ThrowIfCancellationRequested();
        return new(DateTimeOffset.Now, supplies, supplies.Count == 0
            ? ["Nenhuma fonte/bateria encontrada ou power_supply indisponível. Isso não indica falha de hardware."] : []);
    }
}
