# Limpeza de memória e estabilidade EDAC — 25/10/2026

## Estado

Implementados botão de limpeza e primeira etapa de estabilidade (EDAC). Usuário preencheu e aprovou o roteiro conjunto com [disco/carregamento](VALIDACAO_DISCO.md). O aceite refere-se à versão anterior aos ajustes de usabilidade abaixo. Itens condicionais dependem do hardware disponível; não foram fornecidos contadores EDAC reais. Builds Avalonia/SmokeTests e suíte completa aprovados em .NET 8, sem avisos/erros; `git diff --check` aprovado.

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

- [ ] Revisar os demais cartões do app com resumo simples do objetivo e seção expansível “Mais detalhes” para conteúdo técnico. Nesta entrega, o padrão foi aplicado somente à explicação de limpeza de RAM.

### Conferência dos ajustes novos — ainda não realizada pelo usuário

- [ ] Executar cada tipo de benchmark e conferir progresso/CANCELAR no cartão correspondente, inclusive cancelamento e nova execução em outro cartão.
- [ ] Conferir aviso da RAM e abrir/fechar “Mais detalhes”.
- [ ] Limpar sessão: texto vazio, histórico salvo e exportação preservados.
- [ ] Limpar histórico: VOLTAR preserva registros; APAGAR HISTÓRICO remove salvos/pendentes, exportação fica vazia e continua vazia após reabrir. Novo teste inclui somente a nova execução.
- [ ] Durante limpeza, ações concorrentes bloqueadas; fechamento/reabertura sem erro.
