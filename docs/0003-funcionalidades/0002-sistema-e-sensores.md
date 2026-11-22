# Sistema e sensores

**Estado Linux em 22/11/2026:** Implementado parcialmente.

## Finalidade

Exibir identificação de hardware e leituras ao vivo sem depender de WMI.

## Comportamento atual

LinuxSystemInfoCollector lê os-release, cpuinfo, DMI e meminfo. LinuxSensorMonitor calcula carga por diferenças de proc/stat, consulta hwmon/cpufreq e agrega GPU opcional. RAM usada é MemTotal menos MemAvailable. A UI filtra sensores e apresenta avisos.

## Código e referência Windows

- [Linux/Hardware/Telemetry/LinuxSystemInfoCollector.cs](../../Linux/Hardware/Telemetry/LinuxSystemInfoCollector.cs)
- [Linux/Hardware/Telemetry/LinuxSensorMonitor.cs](../../Linux/Hardware/Telemetry/LinuxSensorMonitor.cs)
- [Linux/Hardware/Models/TelemetryModels.cs](../../Linux/Hardware/Models/TelemetryModels.cs)

Referência Windows:

- [Windows/SystemInfoCollector.cs](../../Windows/SystemInfoCollector.cs)
- [Windows/SensorMonitor.cs](../../Windows/SensorMonitor.cs)

## Limites e diferenças

Não há inventário equivalente de DIMMs/SPD. Temperatura genérica de ACPI não deve ser atribuída à CPU. Carga exige amostras sucessivas; hotplug e permissões podem produzir leitura parcial.

## Validação e continuidade

Fixtures cobrem ausência, unidades, cultura, CPU/RAM/DMI, hotplug e cancelamento. Conferir os sensores suportados em cada máquina.

[Roteiro relacionado](../../docs/VALIDACAO_HARDWARE.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
