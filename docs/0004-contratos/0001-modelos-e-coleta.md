# Contratos de modelos e coleta

Estes são contratos internos C#, não endpoints REST. Os tipos efetivos estão nos fontes; esta página descreve a semântica necessária para consumi-los.

## Telemetria

Fonte: [TelemetryModels.cs](../../Linux/Hardware/Models/TelemetryModels.cs).

| Tipo/campo | Semântica |
| --- | --- |
| SensorReading.Id | Identificador do sensor |
| Kind / Component | Tipo da métrica e associação CPU/GPU/Other |
| Value / Unit / Error | Valor anulável, unidade e motivo de indisponibilidade quando disponível |
| MemoryStatus.TotalBytes / AvailableBytes | Bytes totais/disponíveis; usados calculados pela diferença |
| TelemetrySnapshot.Timestamp | Data da amostra |
| CollectionStartedTimestamp | Início monotônico da coleta, usado para idade térmica |
| Notices | Limites/falhas da leitura; não descartar ao apresentar |
| IGpuSensorSource.Read / Dispose | Consulta cancelável e liberação do backend |

## Invariantes de interpretação

Valor zero é válido e diferente de ausência. Não converter null para zero nem atribuir sensor genérico de placa-mãe/ACPI à CPU por adivinhação. A leitura de arquivos Linux é sequencial; hotplug pode produzir amostras parciais.

Os coletores específicos declaram seus records junto aos serviços em Hardware/Storage, Power, Maker, Network e Stability. Resultados têm datas/estados próprios: exportar não deve fabricar uma nova data de coleta.

## Extensão

Ao adicionar métrica, explicitar unidade, fonte, significado de indisponível e precisão. Acrescentar fixtures para zero, ausência, valor inválido, overflow e remoção quando aplicáveis. Não introduzir controle Avalonia dentro dos modelos de hardware.
