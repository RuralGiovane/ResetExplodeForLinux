# Contratos de exportação HTML e JSON

Estado dos builders em 22/11/2026. Exportações são arquivos locais, não respostas de uma API web.

| Domínio | Identificação JSON | Esquema | Fonte |
| --- | --- | --- | --- |
| Benchmark/stress | platform = Linux; sem reportType | 1 | [BenchmarkReportBuilder](../../Linux/Core/Reports/BenchmarkReportBuilder.cs) |
| Armazenamento | reportType = linux-storage | 1 | [StorageReportBuilder](../../Linux/Hardware/Reports/StorageReportBuilder.cs) |
| Alimentação | reportType = linux-power | 1 | [PowerReportBuilder](../../Linux/Hardware/Reports/PowerReportBuilder.cs) |
| Maker | reportType = linux-serial | 1 | [SerialReportBuilder](../../Linux/Hardware/Reports/SerialReportBuilder.cs) |
| Rede | reportType = linux-network | 3 | [NetworkReportBuilder](../../Linux/Hardware/Reports/NetworkReportBuilder.cs) |
| Estabilidade | reportType = linux-stability | 1 | [StabilityReportBuilder](../../Linux/Hardware/Reports/StabilityReportBuilder.cs) |

## Regras comuns

GeneratedAt/appVersion identificam geração/versão; propriedades JSON usam camelCase. Datas de coleta/conclusão são distintas da exportação. Capture copia os dados necessários para que consultas posteriores não alterem o relatório.

Null, falha, cancelamento, consulta ausente e inventário vazio possuem significados distintos. Não inferir sucesso só porque o arquivo foi gerado. HTML escapa conteúdo coletado e é independente de recursos externos.

## Diferenças entre domínios

- Benchmark contém benchmarks com result e savedLocally. Deduplica por id, limita a 200 e remove métricas de resultados incompletos. Enums usam camelCase.
- Rede esquema 3 reúne inventory, latency, throughput e dnsResolution, com estados/metodologias. As medições são independentes. O campo methodology descreve ICMP; throughputMethodology e dnsMethodology descrevem as outras operações.
- Armazenamento preserva datas diferentes para inventário e última consulta SMART.
- Estabilidade inclui EDAC, journal, referência/leitura posterior, logs da sessão e comparação. Expõe metodologias de classificação e comparação. Não inclui automaticamente o histórico de stress.
- Rede/estabilidade usam conversor de enum sem a política camelCase aplicada aos valores; consumidores não devem presumir a mesma capitalização de enums do histórico de benchmark.

Dados de rede, paths de montagem, aliases seriais e mensagens do kernel podem identificar a máquina. A interface avisa antes de compartilhar.

## Evolução e validação

Não normalizar esses formatos silenciosamente. Uma alteração incompatível exige revisão do esquema e dos consumidores. Testar snapshots, escaping, null/zero, datas, precisão e estados incompletos em SmokeTests/Reports. Os records dos builders são a referência exata dos campos.
