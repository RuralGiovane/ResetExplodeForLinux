# Contrato de persistência local

Fontes: [PersistenceModels.cs](../../Linux/Core/Persistence/PersistenceModels.cs) e [LocalPersistence.cs](../../Linux/Core/Persistence/LocalPersistence.cs).

## Destinos

| Arquivo | Base XDG | Fallback |
| --- | --- | --- |
| ResetExplode/settings.json | XDG_CONFIG_HOME absoluto | ~/.config |
| ResetExplode/benchmarks.json | XDG_STATE_HOME absoluto | ~/.local/state |

Variável XDG relativa não substitui o fallback. O histórico fica separado da configuração. Não há credenciais nessa camada.

## Configuração e histórico

LinuxSettings usa schemaVersion 1, thermalEnabled e thermalLimit (60–100 °C). BenchmarkHistory usa schemaVersion 1 e entries, limitado às últimas 200 execuções.

Cada BenchmarkHistoryEntry contém id, date, test, name, outcome, summary, score anulável, metrics, context, thermalEnabled e thermalLimit. Enums persistidos usam camelCase. Estados: completed, notStarted, thermalStopped, cancelled e failed.

Somente completed pode ter métricas/score de resultado. Uma conclusão de stress pode ter score nulo: completar o prazo não é uma nota de desempenho nem um certificado de estabilidade.

## Escrita e recuperação

Lock por arquivo, substituição atômica e validação antecedem a publicação dos dados. Leituras limitadas a 2 MiB. Arquivos inválidos ou versões desconhecidas recebem tratamento de preservação; não sobrescrever cegamente formatos que o leitor não conhece.

A UI conserva gravações pendentes quando falha o armazenamento. Exportação pode incluí-las com indicação de que ainda não foram salvas no histórico local. Limpar texto da sessão não equivale a apagar o histórico.

Ao evoluir o esquema, definir compatibilidade/migração e testar leitura de versões antigas/desconhecidas antes de mudar schemaVersion.
