# Limpeza de memória e estabilidade EDAC — 25/10/2026

## Estado

Implementados botão de limpeza e primeira etapa de estabilidade (EDAC). Usuário preencheu e aprovou o roteiro conjunto com [disco/carregamento](VALIDACAO_DISCO.md). O usuário também conferiu e aprovou todos os ajustes de usabilidade abaixo em 25/10/2026. Itens condicionais dependem do hardware disponível; não foram fornecidos contadores EDAC reais. Builds Avalonia/SmokeTests e suíte completa aprovados em .NET 8, sem avisos/erros; `git diff --check` aprovado.

## Liberar memória dos testes

Os arrays dos testes de RAM são variáveis locais, sem retenção intencional após a execução. Objetos grandes sem referências podem permanecer alocados até uma coleta apropriada do .NET. Isso é compatível com o sintoma reportado, mas não houve perfil de memória da sessão do usuário para certificar sua causa exata.

O botão **LIBERAR MEMÓRIA DOS TESTES**, no cartão RAM, solicita coleta completa e compactação do heap de objetos grandes fora das medições. Mostra memória gerenciada antes/depois; não representa RAM livre total, RSS, cache de arquivos ou bytes garantidamente devolvidos ao sistema operacional. A coleta pode causar breve pausa mesmo sendo solicitada fora da thread da UI. Não apaga resultados/histórico nem arquivos pessoais, não limpa caches globais, não usa privilégios e não força `malloc_trim`.

Não há coleta forçada automática durante/depois das medições: a ação é manual, após sucesso, erro ou cancelamento. Bloqueios recíprocos cobrem benchmarks, testes ativos de rede, exportações e configurações enquanto limpa. Fechamento aguarda a tarefa. Fontes: [GC.Collect](https://learn.microsoft.com/en-us/dotnet/api/system.gc.collect?view=net-8.0) e [compactação de objetos grandes](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.gcsettings.largeobjectheapcompactionmode?view=net-8.0).

Teste dedicado `--memory-cleanup-only`: buffers temporários sem referências foram recuperados, inclusive após falha simulada; dados vivos preservados. Execução isolada observou 9,1 → 1,1 MiB gerenciados. Esses números usam buffers pequenos de teste, não a carga de RAM de 2 GiB nem RSS do app gráfico.

## Estabilidade — EDAC

Nova sub-aba **Sistema e sensores → Estabilidade**, com consulta manual dos controladores `/sys/devices/system/edac/mc/mc*`. Exibe nome, erros corrigidos (`ce_count`), não corrigidos (`ue_count`) e segundos desde reset (`seconds_since_reset`). Usa inteiros de 64 bits sem sinal e mantém ausência/dado inválido como indisponível. Não soma novamente DIMMs/canais aos totais do controlador e não reseta contadores.

Contadores são cumulativos no período reportado pelo driver; não se atribuem a um benchmark específico. Ausência de controladores/driver/permissão não vira zero. Zero reportado não certifica estabilidade. A coleta não é atômica. Atualização limpa dados anteriores e fechamento cancela/aguarda a consulta.

Esta etapa inicia o porte de `OcCheck`/`HardwareErrors`; eventos de GPU, logs do kernel, PCIe, deltas por sessão e relatórios dedicados continuam pendentes. Não implementa carga de estresse nem diagnóstico definitivo. Referência: [documentação RAS/EDAC do kernel](https://www.kernel.org/doc/html/v6.9/admin-guide/RAS/main.html).

Testes `--stability-only` cobrem ausência, zeros, erros, uint64 máximo, negativos/overflow, reset, remoção, cancelamento e snapshots. Consulta real `--live-edac` retornou **nenhum controlador acessível** neste ambiente; hardware com EDAC real ainda não validado.

## Conferência conjunta — aprovada pelo usuário

- [X] Executar RAM até sucesso; clicar LIBERAR MEMÓRIA DOS TESTES, conferir mensagem/contagem e histórico preservado. Observar também o processo no monitor do sistema, distinguindo memória gerenciada de RSS.
- [X] Cancelar RAM e repetir a limpeza. Se ocorrer falha de teste, conferir que a limpeza fica disponível depois do encerramento.
- [X] Repetir a limpeza sem novo teste: continua funcionando, sem promessa de redução a cada clique.
- [X] Durante benchmark, rede ou exportação, botão bloqueado; durante limpeza, não inicia novo teste/exportação. Fechar durante limpeza e reabrir normalmente.
- [X] Estabilidade: antes da consulta, mostrar que ainda não foi realizada. Atualizar e conferir dados disponíveis ou mensagem explícita de indisponibilidade.
- [X] Se hardware expõe EDAC, comparar contadores/nome/segundos com sysfs; zeros não aparecem como certificado de estabilidade.
- [X] Alternar sub-abas, redimensionar, repetir atualização e fechar durante consulta sem erro.
- [X] Concluir também o roteiro de disco/carregamento antes de marcar esta entrega conjunta como aprovada.

## Feedback incorporado antes do commit

- Aviso de que a recuperação de RAM pode ocorrer em etapas, com orientação para aguardar conclusão/atualização do monitor antes de repetir. Detalhes da limpeza em seção expansível “Mais detalhes”. A coleta continua manual, sem promessa de redução a cada clique.
- Progresso, estado e CANCELAR acompanham o cartão da ação executada (CPU, simulação, RAM ou disco/carregamento). Registro da sessão passa a ter cartão próprio.
- LIMPAR REGISTRO DA SESSÃO remove somente o texto da sessão, preservando histórico salvo/exportação.
- LIMPAR HISTÓRICO SALVO pede confirmação no app, aguarda gravações pendentes e substitui atomicamente o histórico por uma lista vazia. Só então limpa cópias salvas/pendentes em memória. Configurações e arquivos exportados permanecem intactos. Nova exportação inclui apenas execuções posteriores. Falha preserva os registros; versão desconhecida é recusada; JSON inválido é preservado em backup antes da substituição.
- Limpeza de histórico bloqueia testes, rede, exportações, configurações e limpeza de memória; fechamento aguarda a tarefa.

### Checklist posterior de legibilidade

- [X] Revisar os demais cartões do app com resumo simples do objetivo e seção expansível “Mais detalhes” para conteúdo técnico. Nesta entrega, o padrão foi aplicado somente à explicação de limpeza de RAM.

### Conferência dos ajustes novos — aprovada pelo usuário em 25/10/2026

- [X] Executar cada tipo de benchmark e conferir progresso/CANCELAR no cartão correspondente, inclusive cancelamento e nova execução em outro cartão.
- [X] Conferir aviso da RAM e abrir/fechar “Mais detalhes”.
- [X] Limpar sessão: texto vazio, histórico salvo e exportação preservados.
- [X] Limpar histórico: VOLTAR preserva registros; APAGAR HISTÓRICO remove salvos/pendentes, exportação fica vazia e continua vazia após reabrir. Novo teste inclui somente a nova execução.
- [X] Durante limpeza, ações concorrentes bloqueadas; fechamento/reabertura sem erro.

## Revisão de legibilidade — 25/10/2026

Cartões e seções de Sistema e sensores e Benchmark receberam resumos diretos; explicações técnicas longas ficam em “Mais detalhes”, recolhidas inicialmente. Seções com textos já curtos mantêm a explicação visível. Consumo de dados/serviço externo, dados pessoais exportados, uso de RAM, espaço/gravação em disco, limites de leitura EDAC e consequências da limpeza continuam visíveis. Controles, resultados e mensagens dinâmicas permanecem fora das seções expansíveis.

Build Avalonia Release aprovado sem avisos/erros. Verificação XML confirmou atributos de controles nomeados e eventos preservados, detalhes recolhidos e ausência de controles de ação dentro deles. Nenhuma lógica de benchmark/coleta/persistência alterada; suíte de lógica não reexecutada. Superdesign não iniciou dentro de 25 s; revisão aplicada diretamente ao Avalonia. Conferência gráfica desta revisão ainda não realizada.

- [ ] Percorrer todas as sub-abas de Sistema e sensores e a página Benchmark; conferir clareza dos resumos.
- [ ] Abrir/fechar “Mais detalhes” com mouse e teclado; conferir texto e rolagem com a janela no tamanho mínimo.
- [ ] Conferir que ações, resultados, avisos de consumo/espaço e confirmação de limpeza continuam visíveis com os detalhes recolhidos.

Os aceites das funcionalidades anteriores permanecem registrados acima.

## Aceite de legibilidade — 25/10/2026

Usuário informou que ficou melhor e autorizou avançar, adiando os ajustes visuais restantes. A revisão de apresentação foi aceita para continuidade; não foram especificados os ajustes restantes nem confirmados individualmente os cenários de teclado/tamanho mínimo acima.

## Logs do kernel — 25/10/2026

Implementada consulta manual em Sistema e sensores → Estabilidade → CONSULTAR LOGS. Usa `journalctl --kernel --boot=0 --priority=0..4 --lines=500 --reverse --output=json --no-pager`, sem shell nem elevação. Exibe até 500 registros mais recentes de emergência a aviso da inicialização atual, com horário local, prioridade numérica e mensagem. Não classifica GPU/PCIe/CPU nem conta falhas físicas: várias mensagens podem descrever um único evento ou uma condição sem falha de hardware. A coleta não é associada ao intervalo de um benchmark.

Prazo de 10 s, limite de 2 MiB de caracteres por stream e 8192 caracteres por mensagem exibida (com indicação de truncamento). JSON inválido, campos não textuais/multivalorados e timestamps inválidos são ignorados com aviso de leitura incompleta. Avisos em stderr indicam possível restrição/incompletude, sem exibir seu conteúdo bruto. Falha do comando descarta a saída; ausência de registros não certifica estabilidade. Retenção e permissões limitam o universo consultado. Registros podem conter nomes, caminhos e identificadores; não há exportação nesta etapa.

Atualização limpa o resultado anterior; cancelamento descarta o resultado em andamento. Fechamento cancela e aguarda o subprocesso. EDAC continua separado e sua consulta não foi alterada. Não há coleta automática, comandos administrativos, reset de contadores ou mudanças no journal.

Validação: builds Avalonia e SmokeTests Release aprovados sem avisos/erros; suíte completa aprovada, incluindo parsing, timestamps, prioridades, saída vazia, exit code, stderr, formato inválido, caracteres de controle, limites, ausência do executável, timeout e cancelamento de subprocessos simulados. Consulta real `--live-kernel-logs` retornou journal indisponível neste ambiente; nenhum registro real validado. Interação gráfica desta etapa pendente.

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release --no-build -- --stability-only
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release --no-build -- --live-kernel-logs
```

O diagnóstico real informa estado e quantidade; omite o conteúdo dos registros no terminal. Referência: [manual oficial do journalctl](https://www.freedesktop.org/software/systemd/man/256/journalctl.html).

- [ ] Consultar logs e conferir horário, prioridade e mensagens, ou indisponibilidade explícita.
- [ ] Comparar com o comando journalctl acima, executado pelo mesmo usuário (o journal pode mudar entre consultas).
- [ ] Repetir consulta e verificar que não duplica nem mantém resultados antigos em caso de falha.
- [ ] Se a consulta durar o suficiente, cancelar e consultar novamente; fechar durante uma consulta e reabrir sem erro.
- [ ] Conferir rolagem, seleção/cópia de texto e “Mais detalhes”; EDAC continua funcionando separadamente.

Próximas frentes: classificação contextual de eventos GPU/PCIe/CPU, períodos/deltas por sessão e relatórios de estabilidade. Ajustes visuais adicionais foram adiados pelo usuário; distribuição permanece por último.

## Classificação contextual de logs — 26/10/2026

Implementada a metodologia `kernel-context-v1`. Cada mensagem mantém horário, prioridade do journal e texto exibido na etapa anterior, acrescidos de contexto indicativo e motivo da associação. O resumo conta mensagens retornadas por contexto: CPU/memória, GPU/gráficos, PCIe e não classificadas. Uma mensagem pode pertencer a mais de um contexto; os totais das categorias podem exceder a quantidade de registros. Não são contagens de falhas físicas nem de eventos únicos.

As regras reconhecem prefixos de linhas: `mce:` e `EDAC MC<n>` para CPU/memória; `NVRM:`, drivers amdgpu/radeon/i915/xe/nouveau e DRM para gráficos; pcieport com endereço de dispositivo e marcadores AER/PCIe Bus Error para PCIe. EDAC genérico, nomes mencionados no meio do texto, mensagens de falta de memória e outras fontes sem marcador conhecido ficam não classificadas. Não há correlação entre mensagens separadas nem inferência de causa, gravidade física ou componente defeituoso. Mensagens de inicialização/configuração desses subsistemas também podem receber contexto; isso não as transforma em falhas.

Classificação limitada ao trecho visível de até 8192 caracteres, independente da cultura do sistema e sem ocultar mensagens desconhecidas. Formatos de drivers não reconhecidos podem ficar sem classificação. As regras associam texto, não autenticam a origem do registro. A seleção do journal continua restrita à inicialização atual, prioridades 0–4 e últimos 500 registros; ausência ou zero em uma categoria não prova ausência de ocorrências.

Referências primárias usadas para os marcadores: [EDAC/RAS do kernel](https://www.kernel.org/doc/html/latest/admin-guide/RAS/main.html), [machine checks](https://kernel.org/doc/html/v5.19/x86/x86_64/machinecheck.html), [PCIe AER](https://kernel.org/doc/html/latest/PCI/pcieaer-howto.html), [Xid/NVRM da NVIDIA](https://docs.nvidia.com/deploy/xid-errors/610/working-with-xid-errors.html) e [drivers gráficos Intel](https://dri.freedesktop.org/docs/drm/gpu/i915.html). O agrupamento e suas regras são decisões do aplicativo, não um diagnóstico fornecido por essas fontes.

Validação: builds Avalonia/SmokeTests Release sem avisos/erros e suíte completa aprovados. Casos simulados cobrem drivers, CPU/memória, PCIe, múltiplos contextos, mensagens benignas com marcadores, falsos positivos de palavras soltas, limite do trecho visível, cultura turca, preservação da mensagem e contagens. Não houve nova consulta real; a anterior retornou journal indisponível no ambiente do agente. Usuário autorizou avançar sem fornecer aceite da consulta de logs anterior; ambas as conferências gráficas permanecem pendentes.

- [ ] Se houver registros, conferir resumo, contexto/motivo e mensagem original em CONSULTAR LOGS.
- [ ] Conferir que registros não classificados continuam visíveis; ausência de registros deve informar que não há dados para classificar.
- [ ] Se houver mensagem com múltiplos contextos, conferir associação e contagens sem interpretar a soma como falhas únicas.

Próximas frentes: períodos/deltas por sessão e exportação de relatórios de estabilidade. Ajustes visuais adicionais e distribuição seguem as prioridades anteriores.

## Comparação manual por sessão — 26/10/2026

Em Estabilidade, CRIAR NOVA REFERÊNCIA captura EDAC, identificação da inicialização e limites de tempo monotônico. COMPARAR COM REFERÊNCIA faz nova leitura e consulta os logs. Comparações repetidas usam a mesma referência; criar outra substitui a anterior, e fechar o app a descarta. Não inicia benchmarks, zera contadores ou persiste a referência. Operação cancelável; fechamento cancela e aguarda. Consulta EDAC/logs avulsa continua independente.

Diferenças CE/UE só são mostradas para controladores presentes nas duas leituras, com mesmo identificador/nome, contadores completos e segundos desde reset disponíveis. Recuo de contadores, remoção/adição, nome alterado, identificação ambígua ou período inconsistente produzem indisponibilidade, nunca zero inventado. A progressão de segundos desde reset deve corresponder ao intervalo monotônico entre coletas, incluindo a duração da coleta e tolerância de 2 segundos para granularidade/leitura. Ainda assim, resets rápidos ou substituição com a mesma identidade podem não ser detectados: o resultado é diferença observada, sem garantia de continuidade. Ausência de controladores não invalida a consulta de logs.

Logs preservam `_BOOT_ID` e `__MONOTONIC_TIMESTAMP`; o boot é normalizado. O intervalo é aberto no fim da captura de referência e fechado no início da captura atual. Usa `CLOCK_MONOTONIC` Linux via `clock_gettime`, disponível neste destino de 64 bits. O horário civil é apenas informativo; ajuste de data/hora não altera a seleção. Trata-se do horário de recebimento no journal, não necessariamente da ocorrência original. Registros sem metadados suficientes são excluídos do intervalo com aviso; continuam disponíveis na consulta avulsa. Mudança/ausência de boot ou intervalo inválido impede comparação.

A consulta continua limitada aos últimos 500 avisos/erros da inicialização atual, filtrados localmente; não garante cobertura de todo o período. Eventos novos durante a leitura do journal podem ocupar parte dessa amostra. Retenção, acesso e leituras incompletas continuam explícitos. Falha do journal permite mostrar diferenças EDAC válidas e informa indisponibilidade dos logs. A comparação não atribui registros a um benchmark nem certifica estabilidade.

Referência: [campos do journal — boot e relógio monotônico](https://www.freedesktop.org/software/systemd/man/258/systemd.journal-fields.html).

Validação: builds Avalonia/SmokeTests Release sem avisos/erros; suíte completa aprovada. Casos simulados cobrem incrementos, zero, uint64, reset, período incoerente, controlador novo/removido/duplicado, mudança de nome, campos ausentes, reboot, relógio civil alterado, limites do intervalo, metadados e logs indisponíveis. Diagnóstico real `--live-stability-session` confirmou relógio monotônico e mesma inicialização, com zero controladores EDAC acessíveis; não consulta journal. Nenhuma comparação real de erros EDAC ou logs foi validada. Conferência gráfica pendente.

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release --no-build -- --stability-only
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release --no-build -- --live-stability-session
```

- [ ] Antes da referência, COMPARAR fica desabilitado; criar referência mostra data e disponibilidade EDAC.
- [ ] Aguardar alguns segundos e comparar: conferir diferenças ou indisponibilidade explícita, além de logs do intervalo ou seus limites de acesso.
- [ ] Repetir comparação mantendo a data inicial; criar nova referência substitui a anterior sem alterar contadores.
- [ ] Se houver tempo, cancelar comparação e repetir; fechar durante consulta e reabrir sem referência anterior.
- [ ] Conferir rolagem/legibilidade e que consultas avulsas continuam funcionando.

Próxima frente: exportação dos relatórios de estabilidade. Conferências gráficas de logs/classificação continuam pendentes; autorização para avançar não foi tratada como aceite.

## Relatórios de estabilidade HTML/JSON — 26/10/2026

Adicionados EXPORTAR ESTABILIDADE HTML e JSON. Relatório `linux-stability`, esquema 1, com data de geração, versão do app, metodologias `kernel-context-v1` e `linux-stability-session-v1`, limites e aviso sobre identificadores/mensagens potencialmente pessoais. Exporta apenas dados já capturados; não dispara EDAC, journalctl ou benchmark.

Conteúdo: consulta EDAC avulsa e seu estado; consulta avulsa de logs com classificações e metadados; referência e leitura posterior da sessão; amostra de logs usada na comparação; diferenças e mensagens do intervalo. Datas de cada coleta permanecem independentes. Os estados da interface distinguem operação ainda não realizada, em andamento, cancelada ou com falha. Iniciar nova consulta limpa o resultado exportável daquela operação; iniciar comparação limpa leitura posterior/amostra anteriores, preservando a referência. Criar nova referência também descarta a anterior. Não há substituição silenciosa por resultados antigos.

O snapshot copia coleções na thread da UI antes da geração do relatório e abertura do diálogo. Consultas podem continuar sem alterar o conteúdo já capturado. Se uma consulta estiver em andamento, o relatório registra esse estado, com os dados daquela operação ausentes. A comparação é reconstruída a partir das cópias da referência/leitura/amostra. Contadores uint64 permanecem exatos no JSON, valores indisponíveis ficam null e textos são escapados no HTML. HTML é independente, sem scripts/recursos externos; JSON inclui os dados estruturados e os avisos.

Usa o diálogo e o coordenador de exportação existentes: exclusão com benchmarks, testes ativos de rede, limpeza de memória/histórico e outras exportações; fechamento aguarda a tarefa. Cancelar o diálogo não salva arquivo. Falha de gravação informa possível arquivo incompleto, conforme o comportamento dos relatórios existentes.

Validação: builds Avalonia/SmokeTests Release sem avisos/erros e suíte completa aprovados. Testes cobrem snapshots independentes, escaping HTML, cultura pt-BR, uint64 máximo, datas, classificação, metadados, ausência de leitura, coleta vazia, cancelamento/falha/em andamento, referência sem comparação e rejeição de reboot. Interação gráfica, gravação pelo diálogo e abertura dos relatórios no sistema do usuário ainda pendentes.

- [ ] Exportar antes de consultar: relatório informa estados ausentes, sem inventar dados.
- [ ] Consultar EDAC/logs, criar referência e comparar; exportar HTML/JSON e conferir datas, estados, diferenças e mensagens.
- [ ] Abrir HTML no navegador e JSON em editor; conferir esquema 1, campos nulos quando indisponíveis e limites de amostragem.
- [ ] Após cancelamento/erro de consulta ou criação de nova referência, exportar e conferir que resultados antigos daquela operação não reaparecem.
- [ ] Cancelar o diálogo de salvar, repetir exportação e verificar bloqueios durante benchmark/rede/limpeza.
- [ ] Conferir fechamento durante exportação e reabertura normal.

Próxima validação: conferir em conjunto logs, classificação, comparação por sessão e relatórios. Estes aceites continuam pendentes; módulos restantes do porte e ajustes visuais seguem no plano, com distribuição por último.
