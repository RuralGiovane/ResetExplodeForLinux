# Validação dos benchmarks CPU/RAM — Linux

## Estado em 25/10/2026

Revisão da metodologia e correção de cobertura/cancelamento da RAM implementadas. Testes automatizados usam buffers pequenos; não certificam desempenho com 2 GiB, render completo ou equivalência ao Windows. Usuário confirmou “tudo funciona certinho”: aceite funcional da etapa CPU/RAM registrado. Não foram fornecidos valores individuais, condições de execução ou séries de três repetições; análise quantitativa de repetibilidade e comparação Windows/Linux continuam pendentes.

## Metodologia implementada

| Teste | Carga e medição |
| --- | --- |
| CPU single/multi | Cena compartilhada com Windows, 960 × 540, 1024 amostras/pixel, uma passada completa; 1 ou `Environment.ProcessorCount` threads. Sem aquecimento separado. Pontos = amostras / segundos / 10.000. |
| Simulação CPU | 30.000 entidades; aquecimento de 2 s fora da medição e janela de 10 s, terminando após o último frame iniciado. Pontos = FPS médio sintético. “1% low” é 1000 dividido pelo tempo do frame no índice `floor(n × 0,99)` da lista ordenada, limitado ao último índice; não é a média dos 1% mais lentos. |
| Latência RAM | Buffer de 256 MiB + vetor de ordem de 16 MiB, semente 42 e saltos de 64 bytes; preparação fora do cronômetro, 200.000 acessos de aquecimento e 20.000.000 acessos dependentes medidos. Cache/TLB também afetam o resultado; o buffer não é garantidamente maior que todo cache de CPU. |
| Banda RAM v2 | Origem e destino de 1 GiB cada; origem inicializada fora da medição. Três pares leitura/cópia, melhor taxa de cada operação independentemente. Leitura conta 1 GiB e cópia conta 2 GiB de tráfego lógico por repetição, em GB/s decimais. Sem aquecimento separado: primeiro toque no destino pode entrar no tempo. Todos os elementos são processados, inclusive divisões desiguais entre threads e caudas. |

Os cronômetros incluem coordenação das threads; a RAM verifica cancelamento entre blocos de até 65.536 elementos (512 KiB), além das etapas de preparação. Alocação e chamadas de runtime não são interrompíveis instantaneamente. Telemetria, atualização gráfica, GC, frequência, temperatura e outros processos podem afetar os tempos. A taxa de cópia não mede diretamente o tráfego físico da DRAM.

Histórico e relatórios dos novos testes incluem `methodology` e parâmetros de execução no contexto. Registros antigos permanecem como estavam, sem preenchimento retroativo. A banda corrigida usa `linux-ram-bandwidth-v2`; não misturar resultados anteriores sem metodologia com os novos. Algoritmos CPU compartilhados e base Windows não foram modificados. Comparabilidade entre plataformas ainda não foi validada.

## Roteiro de execução completa (aceite funcional registrado; medições pendentes)

Executar pela interface, que mantém a política térmica configurada. Não iniciar todos os testes em paralelo. Para banda, os buffers somam 2 GiB, além do consumo do app/runtime/sistema; registrar se houve pressão de memória ou swap.

1. Registrar commit/versão, CPU, RAM, distribuição/kernel, runtime, quantidade de threads, alimentação e governador/perfil de energia. Manter essas condições nas repetições; não alterar automaticamente o sistema.
2. Com a proteção térmica habilitada e sensor disponível, executar CPU single e multi até o fim. Conferir uma passada completa, 530.841.600 amostras e pontuação positiva/finita. O single pode levar bastante tempo; cancelamento não conta como validação completa.
3. Executar simulação completa: confirmar aquecimento, janela de medição de pelo menos 10 s, frames e FPS positivos/finitos. Não interpretar FPS sintético como desempenho de um jogo real.
4. Executar latência e banda: confirmar valores positivos/finitos e interface responsiva. Observar memória/swap e registrar qualquer erro ou interrupção.
5. Repetir cada teste três vezes nas mesmas condições, aguardando retorno à condição térmica inicial entre execuções. Registrar os três resultados; calcular mediana e amplitude relativa `(máximo - mínimo) / mediana × 100`. Investigar variações sem inventar um limite universal de aprovação.
6. Cancelar durante preparação/execução da RAM e iniciar outro teste depois: operação cancelada sem métricas parciais, controles liberados. Fechar durante RAM e reabrir: encerramento após cancelamento, sem conclusão tardia no histórico.
7. Exportar HTML/JSON e reabrir o app: valores, estado e contexto persistidos; conferir `methodology`, buffers, repetições, aquecimento e unidades. Registros antigos continuam legíveis.
8. Comparação Windows/Linux é uma etapa separada: mesma máquina, versões/cargas equivalentes e condições registradas. A versão Windows atual da banda ainda não contém a correção Linux; não declarar equivalência desse teste.

| Teste | Execução 1 | Execução 2 | Execução 3 | Mediana / amplitude | Observações |
| --- | --- | --- | --- | --- | --- |
| CPU single | Não informado | Não informado | Não informado | — | — |
| CPU multi | Não informado | Não informado | Não informado | — | — |
| Simulação CPU | Não informado | Não informado | Não informado | — | — |
| Latência RAM | Não informado | Não informado | Não informado | — | — |
| Banda leitura/cópia | Não informado | Não informado | Não informado | — | — |

## Verificação automatizada

```bash
dotnet build Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release --no-restore
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release --no-build -- --memory-only
```

O teste dedicado cobre soma de todos os elementos, cópia integral, caudas, divisões desiguais, taxas finitas/positivas e pré-cancelamento. Faz parte da suíte padrão. Os testes existentes do coordenador/proteção térmica cobrem descarte parcial, concorrência e interrupções simuladas; não aquecem deliberadamente o hardware.
