# Registro de alterações — RESET EXPLODE para Linux

Este arquivo acompanha a migração construída a partir do projeto Windows, agora no fork `ResetExplodeForLinux/` (anteriormente `ResetExplode/`). O nome da aplicação permanece `ResetExplode`. Para cada alteração, registrar data, arquivos afetados, o que mudou, motivo, validação e pendências. Distinguir trabalho planejado de funcionalidades implementadas e verificadas.

## 2026-10-25 — Aceite conjunto e ajustes de usabilidade

Usuário preencheu os roteiros de disco/carregamento e limpeza/EDAC como aprovados e pediu ajustes antes do commit. Observações transitórias removidas do roteiro após incorporar as ações: aviso da recuperação de RAM em etapas, explicação expansível na RAM, controles de progresso/cancelamento no cartão ativo, registro da sessão separado e botões independentes para limpar sessão/histórico salvo. Revisão geral de legibilidade dos demais cartões fica em checklist posterior.

`MainWindow.axaml`/code-behind passam a mover o mesmo painel de execução entre os cartões, preservando controles e fluxo de cancelamento. `MainWindow.HistoryCleanup.cs` oferece limpeza da sessão e confirmação para histórico salvo, aguarda persistência em andamento e limpa cópias locais/pendências somente após sucesso. `LocalPersistence.ClearHistory` usa lock, validação de versão, backup de JSON inválido e escrita atômica. Bloqueios recíprocos e fechamento aguardam a operação. Arquivos exportados e configurações são preservados; nenhuma limpeza real de dados do usuário foi acionada pelo agente.

Builds Avalonia/SmokeTests aprovados em Release/.NET 8, zero avisos/erros. Testes de persistência ampliados cobrem limpeza/reabertura, exportação vazia, nova gravação sem retorno de registros antigos, lock, versão desconhecida, preservação de configuração e backup de arquivo inválido. Suíte completa e `git diff --check` aprovados. Novos ajustes visuais ainda não conferidos pelo usuário; aceites anteriores não foram aplicados retroativamente. CLI Superdesign não iniciou dentro do prazo; alterações realizadas diretamente no Avalonia, sem artefato remoto.

Documentação atualizada para o commit local da entrega acumulada (RAM, disco/carregamento, limpeza, EDAC e usabilidade). Nenhum push solicitado.

## 2026-10-25 — Limpeza manual de memória e primeira etapa de estabilidade

Usuário adiou conferência gráfica de disco para validar entregas juntas e relatou RAM ainda ocupada após sucesso/falha. Revisão encontrou buffers locais sem retenção intencional; coleta tardia do .NET é compatível com o sintoma, sem diagnóstico de vazamento na sessão do usuário. `BenchmarkMemoryCleanup.cs` e `MainWindow.MemoryCleanup.cs` adicionam botão para coleta completa/compactação LOH sob demanda, contagem de memória gerenciada antes/depois e preservação de dados vivos/histórico. Exclusão com benchmarks/rede/exportações/configurações integrada às entradas existentes; fechamento aguarda a limpeza. Não altera caches globais nem faz limpeza automática durante medições.

Primeira frente de estabilidade: `Hardware/Stability/LinuxEdacCollector.cs` e `MainWindow.Stability.cs`, com nova sub-aba Estabilidade e consulta manual de controladores EDAC, CE/UE e segundos desde reset. Sem somar DIMMs aos controladores, sem resets, sem zero inventado quando dados estão ausentes. Sem equivalência a WHEA ou certificado de estabilidade. Logs/GPU/PCIe e demais partes de OcCheck permanecem pendentes.

Testes em `MemoryCleanupChecks.cs`/`EdacChecks.cs` e comandos dedicados adicionados à suíte. Builds Avalonia/SmokeTests em Release/.NET 8 aprovados, zero avisos/erros; suíte completa aprovada. Limpeza isolada observou 9,1 → 1,1 MiB gerenciados; dentro da suíte 34,4 → 2,3 MiB, preservando dados vivos. Não são medidas do app gráfico com carga de 2 GiB. EDAC real retornou nenhum controlador acessível; suporte real com contadores permanece não validado. `git diff --check` aprovado.

README/plano e `VALIDACAO_ESTABILIDADE_MEMORIA.md` atualizados. Conferência gráfica conjunta (disco, limpeza e EDAC) pendente a pedido do usuário. Sem commit/push.

## 2026-10-25 — Benchmarks de disco e carregamento Linux

Adicionados `Linux/Core/Storage/LinuxDirectFile.cs`, `BenchmarkWorkspace.cs` e `Benchmarks/DiskBenchmarks.cs`. Backend Linux x64 com buffers nativos alinhados, arquivos exclusivos, O_DIRECT/O_DSYNC, rejeição de transferências curtas e suporte consultado por statx. Btrfs usa consulta de setor via ioctl e configura/verifica NoCoW/NoCompress somente nos arquivos novos, evitando o caminho de escrita com checksum/cache. Nenhuma alteração de arquivos existentes, diretórios do usuário, configurações de montagem ou cache global. Limpeza não recursiva de nomes rastreados; falha impede resultado de sucesso e informa a pasta residual.

`MainWindow.DiskBench.cs`, AXAML e coordenação da janela integram pasta de destino, disco sequencial/aleatório, carregamento de pacote/assets, progresso, cancelamento, política térmica CPU, exclusão com rede/exportação e fechamento. Histórico/relatórios reutilizados; `BenchmarkMethodology.cs` registra cargas, unidades e limites. Core habilita unsafe para o buffer alinhado; modo suplementar de validação .NET 10 acompanha essa configuração. Windows e fontes compartilhados preservados.

`DiskBenchmarkChecks.cs` e CLI adicionam suíte simulada padrão/`--disk-only` e diagnóstico real opt-in `--live-disk-smoke <pasta>`. Testes cobrem dados/contagens, alinhamento, I/O curto, falhas sequenciais/paralelas, cancelamento, pré-cancelamento, espaço insuficiente, exclusividade e limpeza que preserva arquivos não rastreados. Diagnóstico real confirma round-trip de bytes, EOF rejeitado, criação exclusiva e cargas reduzidas de disco/carregamento.

Validação: builds Avalonia e SmokeTests em Release/.NET 8 com zero avisos/erros; suíte completa aprovada. Diagnóstico real reduzido aprovado no Btrfs do workspace; `/tmp` (tmpfs) recusado por falta de suporte confirmado. Sem diretórios de benchmark residuais no workspace. `git diff --check` aprovado. Referências oficiais, metodologia e roteiro em `docs/VALIDACAO_DISCO.md`; READMEs/plano atualizados. Cargas padrão e validação gráfica permanecem pendentes, assim como outros sistemas de arquivos e comparação Windows/Linux. Sem commit/push; distribuição continua por último.

## 2026-10-25 — Aceite funcional CPU/RAM

Usuário confirmou “tudo funciona certinho” após a entrega de correções e metodologia CPU/RAM. Aceite funcional registrado no roteiro, plano e README Linux. Não foram informadas métricas individuais nem séries de três repetições; análise quantitativa de repetibilidade e comparação Windows/Linux continuam pendentes. Próxima frente funcional: disco/carregamento sem cache; empacotamento permanece por último.

Alteração somente documental, verificada com `git diff --check`; builds e suíte aprovados na implementação anterior. Sem novo commit ou push.

## 2026-10-25 — Revisão de metodologia CPU/RAM

Corrigido `Linux/Core/Benchmarks/MemoryBench.cs`: partições proporcionais cobrem todos os elementos, leitura inclui caudas de 1–3 elementos e cópia/leitura/inicialização verificam cancelamento por blocos de 512 KiB. Preparação e aquecimento da latência também verificam cancelamento. Volume padrão de RAM, repetições e agregação preservados; mudança de execução identificada como `linux-ram-bandwidth-v2`. Código Windows/compartilhado preservado.

Adicionado `BenchmarkMethodology.cs`, integrado ao contexto no `MainWindow.axaml.cs`, para registrar parâmetros e limitações no histórico/HTML/JSON sem reescrever registros antigos. `MemoryChecks.cs` exercita os mesmos laços com buffers pequenos, threads não divisoras, caudas, soma, cópia, taxas e pré-cancelamento; acesso interno restrito ao assembly de testes via csproj. Comando `--memory-only` e integração à suíte padrão adicionados. README, plano e novo `VALIDACAO_BENCHMARKS.md` descrevem metodologia e roteiro de execução completa.

Validação: builds SmokeTests e Avalonia em Release com `--no-restore`, zero avisos/erros; teste dedicado de RAM e suíte completa aprovados em .NET 8. `git diff --check` aprovado. Não executados render completo, buffers padrão de 2 GiB ou interação gráfica nesta etapa; esses aceites e comparação entre plataformas continuam pendentes. Sem commit/push.

## 2026-10-25 — Prioridade dos itens obrigatórios

Usuário adiou o empacotamento/instalação para o final, priorizando as pendências obrigatórias do porte. Plano e README Linux corrigidos para não indicar distribuição como próxima frente. Nenhum código de empacotamento foi criado. Alteração somente documental; verificada com `git diff --check`, sem necessidade de executar testes da aplicação.

## 2026-10-25 — Aceite final do resumo e commit da entrega

Usuário aprovou o resumo (“ficou perfeito”) e solicitou commit, além de discutir distribuição como aplicativo instalável/portátil. Aceite registrado na checklist e estado atual atualizado. Entrega reúne ICMP, throughput HTTP limitado, resolução de nomes, relatórios de rede esquema 3, sub-abas e Visão geral. Builds Avalonia/SmokeTests sem avisos/erros e suíte completa aprovados na última implementação; esta etapa altera somente documentação e prepara o commit solicitado. Nenhum push solicitado.

Distribuição registrada como próxima frente proposta: runtime incluído, pacote portátil, comando e integração ao menu com instalador/desinstalador por usuário. Nenhum empacotamento foi implementado nesta entrega.

## 2026-10-25 — Visão geral com resumo do sistema

Usuário aprovou a organização em sub-abas e pediu enriquecer Visão geral no estilo fastfetch antes do commit. `MainWindow.axaml` agora inclui logo/nome da máquina, tabela compacta com sistema/kernel/arquitetura/CPU/threads/placa-mãe/BIOS/RAM, GPU/fonte e espaço usado na raiz, além de leituras ao vivo. `MainWindow.Overview.cs` projeta dados já coletados, com campos indisponíveis explícitos; `MainWindow.Telemetry.cs` e `MainWindow.Storage.cs` atualizam o resumo nos mesmos ciclos existentes e limpam valores dinâmicos quando há erro. Não executa programas externos nem adiciona coleta.

`bash Linux/validate.sh` aprovado: builds Avalonia/SmokeTests sem avisos/erros e suíte completa aprovada. `git diff --check` aprovado. Conferência visual do novo cartão pendente na sessão do usuário; limitação de display registrada na entrega anterior permanece. Próxima etapa combinada é commit após o ajuste; sem staging, commit ou push pelo agente.

## 2026-10-25 — Sub-abas de Sistema e sensores

Solicitação do usuário: reduzir a página longa e dedicar uma sub-aba a cada cartão, mantendo Sistema e sensores como seção principal. `MainWindow.axaml` agora usa Grid com cabeçalho fixo e TabControl: Visão geral, Computador, Sensores, Rede, Armazenamento, Alimentação e Maker. Cada TabItem contém o cartão original em ScrollViewer próprio; WrapPanel permite quebra dos cabeçalhos. `Styles/Theme.axaml` ajusta tipografia/espaçamento e destaque da seleção. Resultado ICMP e metodologia movidos para junto dos controles de latência.

Preservados nomes, eventos, bindings e serviços existentes; nenhuma coleta ou teste ativo novo é disparado pela navegação. Build Avalonia com --no-restore aprovado, zero avisos/erros. Comparação XML com o estado anterior confirmou todos os controles nomeados/atributos preservados, sem duplicidade, e sete cartões em sete sub-abas. `git diff --check` aprovado. Tentativa gráfica com estado/configuração temporários falhou em `XOpenDisplay failed`; roteiro visual pendente. Não reexecutada a suíte de lógica, pois esta entrega altera somente apresentação. CLI do Superdesign não concluiu inicialização e foi interrompido; reorganização realizada diretamente no Avalonia, sem artefato remoto.

## 2026-10-25 — Aceite da resolução de nomes

Usuário confirmou a validação do roteiro de resolução de nomes: consulta externa, entradas inválidas, exportação HTML/JSON esquema 3 e controles gráficos. Cenários condicionais (IDN, cancelamento/fechamento durante consulta) seguem a disponibilidade indicada no roteiro; domínios e valores individuais não foram informados. Checklists local/versionada, READMEs e plano atualizados. Este aceite substitui a pendência de validação do usuário registrada na implementação abaixo.

Atualização somente documental; builds sem avisos/erros, suíte completa e consulta local real já aprovados na implementação anterior. Alterações prontas para commit manual, sem staging, commit ou push pelo agente. Bufferbloat, metodologia de capacidade máxima e demais frentes do porte permanecem pendentes.

## 2026-10-25 — Resolução de nomes e relatório esquema 3

Adicionados `NetworkDnsTest`, `MainWindow.Dns.cs`, controles manuais e diagnóstico opt-in `--live-dns`. Consulta pelo resolvedor do sistema com nome explícito/IDN, relógio monotônico, IPv4/IPv6 deduplicados e token de cancelamento após 5 s. Encerramento efetivo depende do resolvedor; tarefa aguardada no fechamento. Cache/hosts/NSS/VPN não são confundidos com medição isolada de servidor DNS. Falha/cancelamento limpa resultados antigos e não publica resultado parcial; sem configuração de DNS ou consulta automática.

Relatório de rede esquema 3 inclui resolução, estado, metodologia e cópia de endereços; HTML/JSON preservam demais resultados com datas próprias. Bloqueios recíprocos com ICMP/throughput/benchmarks/exportação. Builds Avalonia/SmokeTests sem avisos/erros e suíte completa aprovados, incluindo testes simulados de entrada, IDN, respostas, erros, timeout, cancelamento e exportação. Consulta real `--live-dns localhost` aprovada no ambiente do agente: retornou `::1` e `127.0.0.1`, em 14,29 ms observados. Isso valida integração com o resolvedor local, sem comprovar DNS externo. CLI recompilado sem avisos/erros após inclusão do diagnóstico. Interação gráfica e resolução externa aguardam validação do usuário.

## 2026-10-25 — Aceite de download/upload HTTP

Usuário confirmou “tudo validado” para o roteiro de download/upload: transferência real, resultados, exportação HTML/JSON esquema 2, cancelamento, bloqueios, fechamento e apresentação. Cenários condicionais seguem a disponibilidade indicada no roteiro; valores individuais das medições não foram informados. Checklists local/versionada, READMEs e plano atualizados. Este aceite substitui a pendência de validação do usuário registrada na implementação abaixo; não altera o registro de que o agente não executou teste de volume externo.

Atualização somente documental; builds sem avisos/erros e suíte completa aprovados na implementação anterior. Alterações prontas para commit manual, sem staging, commit ou push pelo agente. Permanecem fora desta entrega bufferbloat, medição de capacidade máxima, Maker conectado, build Windows e demais módulos pendentes do porte.

## 2026-10-25 — Download/upload HTTP e relatório de rede esquema 2

Novo `NetworkThroughputTest`: Cloudflare, GET de 10 MB e POST de 5 MB sintéticos, sequenciais, timeout de 20 s por etapa, duração monotônica, taxa em Mbit/s. Exige corpo completo e sucesso HTTP, rejeita compressão e tamanhos declarados inesperados; sem redirecionamento/cookies. Upload contabilizado apenas após envio completo e resposta de sucesso. Cancelamento, falha ou timeout descarta as duas medições.

`MainWindow.Throughput.cs` e cartão de rede: destino/consumo explícitos, início manual, cancelamento, exclusão com ICMP/benchmarks/exportações e espera no fechamento. Novo teste limpa a transferência anterior, preservando ICMP. `NetworkReportBuilder` esquema 2 adiciona resultado e estado HTTP com bytes/duração/taxas, datas e metodologia. Testes simulados e diagnóstico opt-in `--live-throughput` adicionados. Servidor público e roteiro gráfico ainda não validados nesta etapa; suíte não envia tráfego real. `bash Linux/validate.sh` aprovado: builds Avalonia/SmokeTests sem avisos/erros e suíte completa aprovada. A primeira execução encontrou uma expectativa de teste que não considerava a codificação HTML do acento; expectativa corrigida e suíte reexecutada com sucesso. `git diff --check` aprovado.

## 2026-10-25 — Aceite da exportação de rede

Usuário confirmou “tudo validado” para o roteiro de exportação HTML/JSON de rede: conteúdo, estados sem resultado, cancelamento, substituição de arquivo, exclusão com testes/benchmarks e fechamento. Checklist marcada nas cópias local e versionada; READMEs e plano atualizados. Este aceite substitui a pendência gráfica registrada na implementação abaixo.

Atualização somente documental; builds e suíte completa aprovados na implementação anterior. Alterações prontas para commit manual, sem staging, commit ou push pelo agente. Próxima frente funcional de rede: testes de download/upload, com definição de destino e metodologia.

## 2026-10-25 — Exportação de rede HTML/JSON

Implementados `NetworkReportBuilder`, botões e integração ao diálogo de exportação. Snapshot independente de interfaces/métricas, DNS, avisos e resultado ICMP, com datas separadas e esquema JSON `linux-network` versão 1. HTML escapa textos e contém metodologia/limites das métricas. Aviso junto aos botões informa a inclusão de IPs, gateways, DNS e destino.

Estado da sessão preserva inventário e teste concluído; nova tentativa limpa o teste anterior. Falha/cancelamento não exporta métricas antigas ou parciais. Falha da coleta limpa inventário e preserva mensagem, sucesso posterior limpa erro. Exportação mantém exclusão com benchmarks/teste ICMP e espera no fechamento. Apresentação, diálogos, substituição de arquivo e fechamento aguardam validação gráfica do usuário. Testes automatizados adicionados para dados, snapshot, escaping e estados sem resultado. `bash Linux/validate.sh` aprovado: builds Avalonia/SmokeTests com zero avisos/erros e suíte completa aprovada. `git diff --check` aprovado.

## 2026-10-25 — Aceite dos testes ativos de rede

Usuário confirmou “tudo validado” após executar o roteiro da entrega de latência, jitter e perda ICMP. Checklist marcada nas cópias local e versionada; README principal/Linux e plano atualizados. Valores individuais das medições não foram informados. Este aceite substitui a pendência de validação do usuário registrada na implementação abaixo; o diagnóstico de timeouts do ambiente do agente permanece como registro histórico.

Atualização somente documental; builds e suíte aprovados na implementação anterior. Alterações prontas para commit manual, sem staging, commit ou push pelo agente. Download/upload e exportação de rede permanecem etapas futuras.

## 2026-10-25 — Testes ativos de latência, jitter e perda ICMP

Implementados serviço `NetworkLatencyTest`, cartão integrado ao inventário, destino IPv4/IPv6 explícito, 10 tentativas, timeout de 2 s e intervalo de 500 ms. RTT mín./médio/máx., jitter entre tentativas consecutivas com sucesso, perda ICMP e estados retornados. Erros locais e cancelamento não publicam resultados parciais. Exclusão com benchmarks/exportações, cancelamento e espera no fechamento. Diagnóstico opt-in `--live-latency <IP>`; suíte padrão não envia pacotes.

Builds Avalonia/SmokeTests sem avisos/erros e suíte completa aprovados. Testes simulados cobrem resultados mistos, zero, perda total, jitter sem atravessar falhas, entradas inválidas, pré-cancelamento, cancelamento durante envio/pausa e erro local. Apresentação, interação gráfica e destinos externos ainda pendentes. Sem staging, commit ou push pelo agente.


Diagnóstico real no ambiente do agente: `--live-latency 127.0.0.1` concluiu com 10 estados `TimedOut`, RTT/jitter indisponíveis. A causa não foi determinada; este resultado valida a conclusão sem respostas, mas não valida recepção ICMP real nem conectividade da máquina do usuário.

## 2026-10-24 — Aceite final e preparação para commit manual

Usuário marcou e confirmou toda a checklist de endereços IPv4/IPv6, gateways, DNS, atualização, legibilidade e fechamento/reabertura. Fluxo SMART administrativo, instância única, inventário de rede, GPU/NVML e cenários gráficos anteriores também aprovados. Maker com hardware conectado continua pendente por falta de dispositivo.

Atualizados README principal/Linux e estado final do plano. Checklists do diretório de trabalho copiadas para docs/VALIDACAO_GRAFICA.md e docs/VALIDACAO_HARDWARE.md; originais permanecem disponíveis no diretório solicitado. Links atuais do README Linux apontam para as cópias versionadas. Notas históricas anteriores mantêm caminhos e pendências da época.

Última alteração de código: builds Avalonia e SmokeTests com --no-restore aprovados, zero avisos/erros, suíte completa aprovada; usuário confirmou a interação real. Finalização apenas documental, com git diff --check aprovado e nenhuma alteração em Windows/Shared. Preparado commit manual conforme o formato da entrega anterior. Nenhum staging, commit ou push executado pelo agente.

## 2026-10-24 — Inventário de rede aprovado e IPv4/IPv6, gateways e DNS

Usuário confirmou o inventário de rede e indicou commits após a próxima alteração. Registrado aceite e implementada expansão do inventário; sem staging/commit nesta etapa.

`Linux/Hardware/Network/LinuxNetworkAddresses.cs`: enumeração local por API .NET, endereços unicast IPv4/IPv6, prefixos e escopo, gateways reportados, campos indisponíveis quando não suportados. `LinuxResolverConfiguration.cs`: parser de nameserver em resolv.conf, preservação de ordem, deduplicação, IPv4/IPv6, comentários, distinção entre ausência e lista vazia, contagem de entradas inválidas. Não realiza consultas DNS.

`LinuxNetworkCollector.cs`: associação exata por nome, rejeição de associação ambígua, isolamento de falhas da API, snapshot textual independente e DNS separado. `Views/MainWindow.Network.cs`/`MainWindow.axaml`: campos integrados ao cartão e atualização existente. `SmokeTests/Network/NetworkConfigurationChecks.cs`, `NetworkChecks.cs` e `Program.cs`: fontes simuladas isoladas, prefixos/escopo, múltiplos endereços, ausência versus falha, alteração/remoção, DNS inválido e snapshots; diagnóstico --live-network inclui DNS.

Builds Avalonia e SmokeTests com --no-restore aprovados, zero avisos/erros; suíte completa e git diff --check aprovados. Coleta real preservou oito interfaces/contadores e leu DNS local. Endereços/gateways indisponíveis no ambiente; comando independente `ip -brief address` confirmou bloqueio netlink com Operation not permitted. Validação real dos novos campos e interação gráfica pendentes em `../../VALIDACAO_GRAFICA.md`. Gateways não representam todas as tabelas/rotas e DNS não determina configuração específica por aplicativo/VPN. Base Windows preservada.

## 2026-10-24 — Instância única aprovada e inventário de rede

Checklist de instância única marcada e confirmada pelo usuário, incluindo restauração, aviso SMART, benchmark em andamento e reabertura. Registrado o aceite em roteiro, README e plano.

Iniciado módulo de rede previsto no porte de MainWindow.Net/NetworkTest. `Linux/Hardware/Network/LinuxNetworkCollector.cs` lê sysfs: interfaces, operstate/carrier, MTU, velocidade/duplex e oito contadores RX/TX. Zero e uint64 máximo preservados; ausências, negativos, overflow e sentinelas tratados como indisponíveis. Snapshots independentes, listas somente de leitura, cancelamento entre leituras e nenhuma inferência de conectividade/throughput.

`Linux/Views/MainWindow.Network.cs`, `MainWindow.axaml` e `MainWindow.axaml.cs`: cartão integrado à página de sistema com coleta inicial/manual fora da UI, exclusão de atualizações sobrepostas, cancelamento e espera no fechamento. `Linux/SmokeTests/Network/NetworkChecks.cs` e `Program.cs`: fixtures com cultura pt-BR, estados, limites, remoção/reaparecimento e snapshot; modos --network-only/--live-network. Base Windows preservada.

Builds Avalonia e SmokeTests com --no-restore aprovados, zero avisos/erros; suíte completa e git diff --check aprovados. Leitura real encontrou oito interfaces (Wi-Fi, Ethernet, loopback e bridges); estados e contadores retornados. Apresentação/atualização gráfica pendentes no roteiro externo `../../VALIDACAO_GRAFICA.md`. IP/gateway/DNS, testes ativos e exportação não implementados nesta etapa. Sem tráfego de teste, alteração de configuração ou privilégios.

## 2026-10-24 — SMART aprovado pelo usuário e instância única Linux

Usuário confirmou toda a checklist do novo fluxo SMART. Registrada a aprovação no roteiro externo: aviso/consentimento, autenticação, cancelamentos, dados reais, exportação e fechamento. GPU/NVML já aprovada; Maker conectado permanece pendente por falta de hardware.

Implementada a pendência de instância única do plano. `Linux/Core/Lifecycle/SingleInstance.cs` mantém exclusão entre processos por arquivo privado bloqueado no estado XDG, com pedidos vazios de ativação identificados por GUID e confirmação de recebimento. Segunda abertura aguarda até três segundos, informa o resultado e encerra antes de inicializar Avalonia/coletores. O lock é mantido até o término da primeira instância, liberado também pelo sistema após encerramento abrupto; o arquivo não é apagado para preservar o inode compartilhado. Pedidos/confirmações abandonados são removidos após um minuto.

`Linux/Program.cs`, `App.axaml.cs` e `Views/MainWindow.axaml.cs` integram ativação no dispatcher, pedidos durante inicialização, restauração de janela minimizada, foco em diálogo próprio e respeito ao encerramento. Falha de acesso ao estado impede abrir uma instância sem controle e é informada no terminal. Sem alterações na base Windows.

`Linux/SmokeTests/Core/SingleInstanceChecks.cs` e `Program.cs` adicionam testes reais entre processos: exclusão, duas ativações consecutivas, identidades independentes, reabertura e recuperação após encerramento abrupto. Modo `--instance-only` e suíte completa aprovados. Builds Avalonia/Core/Hardware/SmokeTests com dependências locais (`--no-restore`) aprovados, zero avisos/erros; `git diff --check` aprovado. Documentados comportamento e limites no README e plano; nova checklist em `../../VALIDACAO_GRAFICA.md`.

Ativação/restauração gráfica ainda requer confirmação do usuário. O gerenciador de janelas pode recusar foco. Instâncias com diretórios XDG de estado distintos são independentes. Confirmação do pedido indica encaminhamento à UI, não garantia de foco concedido pelo desktop.

## 2026-10-24 — Confirmação e autorização administrativa para SMART

Usuário confirmou leitura SMART com sudo no terminal e solicitou aviso prévio no app. `Linux/Views/MainWindow.Smart.cs` agora apresenta diálogo modal com dispositivo, finalidade da permissão, Cancelar e Prosseguir. Cancelar/Escape/fechar não inicia processo; apenas confirmar chama o leitor privilegiado. Seleção e botão permanecem bloqueados durante o fluxo e são liberados ao terminar.

`Linux/Hardware/Storage/LinuxSmartReader.cs` adiciona modo administrativo explícito com pkexec, sem shell, argumentos separados e executáveis em caminhos de sistema. Autenticação fica com polkit, sem coletar senha no app. Timeout elevado limita a consulta a 15 segundos, seguido de encerramento forçado após dois segundos; prazo externo de dois minutos cobre autorização. Cancelamento/recusa sem JSON não é interpretado como bitmask SMART. CLI permanece sem elevação. Processo já elevado pode depender do timeout ao fechar; I/O de kernel não interrompível permanece uma limitação.

`Linux/SmokeTests/Storage/SmartProcessChecks.cs` cobre argumentos do lançador, retorno bem-sucedido, cancelamento, recusa, timeout e preservação de bitmask SMART coincidente com código de autorização. Simulação sem privilégios reais. Builds Avalonia e SmokeTests com `--no-restore` aprovados, zero avisos/erros, e suíte completa aprovada. `validate.sh` com restauração falhou por acesso negado ao NuGet; dependências existentes permitiram validar tudo sem restauração. `git diff --check` aprovado. Fluxo gráfico/polkit real pendente; checklist atualizado em `../../VALIDACAO_HARDWARE.md`. Nenhuma política polkit, permissão de dispositivo ou instalação alterada.

## 2026-10-24 — Causa de falha SMART identificada

Usuário forneceu erro direto do smartctl para /dev/nvme0n1: Permission denied. Falha de abertura por permissão confirmada nessa tentativa; saúde do disco e leitura real pelo app continuam pendentes. Roteiro atualizado com consulta manual privilegiada limitada ao smartctl. Nenhuma elevação executada pelo agente ou alteração de permissões/código.

## 2026-10-24 — Retorno da validação de hardware pelo usuário

GPU NVIDIA reconhecida no terminal, métricas do coletor disponíveis e sensores atualizando no app: validação manual aprovada. Exportação Maker confirmada para o inventário disponível; testes com porta conectada pendentes porque o usuário não possui dispositivo. SMART retornou LEITURA INCOMPLETA por falha de abertura/identificação; causa ainda indeterminada, sem aprovação de leitura real, UI SMART ou exportação de métricas reais.

Atualizado `../../VALIDACAO_HARDWARE.md`, preservando as anotações do usuário e acrescentando diagnóstico direto do smartctl para obter a causa específica. Código revisado confirma que a mensagem apresentada agrega falhas de abertura/identificação; não comprova falta de permissão. Sem alterações de código ou testes adicionais.

## 2026-10-24 — Diagnóstico real de hardware e roteiro complementar

Executados `--live-serial`, `--live-telemetry`, `--live-smart nvme0n1` no binário existente e `nvidia-smi -L`. CPU/hwmon/cpufreq retornaram dados reais; os testes simulados incluídos em live-telemetry passaram. Nenhuma porta serial observada. Disco presente em sysfs, ausente em /dev: SMART retornou LEITURA INCOMPLETA, sem avaliação válida. NVML falhou com código 9 e nvidia-smi também falhou; nenhum dispositivo /dev/nvidia disponível. Essas limitações não comprovam defeito de hardware ou driver na sessão normal do usuário.

Criado `../../VALIDACAO_HARDWARE.md` no diretório de trabalho, com evidências e passos de terminal/UI para SMART, GPU e serial. Validação dessas integrações com dispositivos acessíveis permanece pendente. Sem alteração de código, instalação, elevação ou nova compilação.

## 2026-10-24 — Roteiro gráfico concluído

Usuário confirmou funcionamento dos três cenários restantes: cancelamento de benchmark com retorno da interface e histórico sem pontuação parcial; bloqueio pela pré-checagem térmica com motivo e interface utilizável; fechamento durante execução com cancelamento e encerramento. Todos os itens de `../../VALIDACAO_GRAFICA.md` estão aprovados por validação manual do usuário.

Atualizados roteiro, estado do plano e README principal. Nenhum código alterado ou teste reexecutado nesta confirmação. Permanecem fora deste aceite hardware serial conectado, SMART real, NVML, metodologia dos benchmarks completos e build Windows. Próxima frente: validação das integrações com hardware disponível e continuidade dos módulos pendentes do porte.

## 2026-10-24 — Validação gráfica básica confirmada pelo usuário

Usuário aprovou abertura, navegação, sensores, inventários, exportações HTML/JSON, cancelamento do diálogo e fechamento em repouso/durante atualização. Confirmou também bloqueio de início de benchmarks e novas exportações durante exportação, além da substituição de JSON existente sem conteúdo residual. Navegação e atualização de dados durante exportação são permitidas.

Atualizado `../../VALIDACAO_GRAFICA.md` (movido anteriormente para o diretório de trabalho a pedido do usuário), com resultados e passos para cancelamento, pré-checagem térmica e fechamento durante benchmark. Esses três cenários gráficos permanecem pendentes. O binário existente de SmokeTests executado com `--thermal-only` passou: limite, pré-checagem, perda/atraso de leitura, cancelamento e descarte parcial. Sem alterações no código ou nova compilação nesta etapa.

## 2026-10-23 — Diagnóstico do acesso gráfico e roteiro

Conexões aos sockets `/tmp/.X11-unix/X0`, `/tmp/.X11-unix/X1` e `/run/user/1000/wayland-1` retornaram errno 1 (`Operation not permitted`). O arquivo Xauthority configurado existe e é legível; `xdpyinfo -display :0` também não conseguiu abrir o display. O acesso gráfico está bloqueado neste ambiente. Não foi alterada a configuração da sessão nem o código da aplicação.

Adicionado `docs/VALIDACAO_GRAFICA.md`, com comando de abertura, cenários de navegação, atualização, exportação/cancelamento/substituição de arquivos e fechamento. Todos permanecem explicitamente pendentes de execução na sessão gráfica. README Linux aponta para o roteiro.

## 2026-10-23 — Compilação Avalonia validada

**Resultado:** `bash Linux/validate.sh` concluiu os builds Avalonia e SmokeTests (incluindo Core/Hardware), com zero avisos/erros, e toda a suíte passou no SDK 8.0.132/runtime 8.0.32. `bash Linux/validate.sh --offline-net8` também passou. Nenhuma alteração de código foi necessária.

A consulta a `https://api.nuget.org/v3/index.json` falhou por resolução de DNS, mas as dependências locais permitiram compilar. A tentativa de execução com `timeout 15s dotnet Linux/bin/Release/net8.0/ResetExplode.dll` terminou antes do prazo, com código 134 e exceção `XOpenDisplay failed` durante a inicialização Avalonia X11. DISPLAY estava configurado; isso não comprova acesso à sessão gráfica.

Atualizados README raiz, README Linux e plano para distinguir compilação aprovada de interação gráfica pendente. Próximo passo: executar em sessão com display acessível e verificar telas, diálogos de exportação e fechamento. Build Windows, hardware conectado e benchmarks completos não foram executados nesta continuação.

## 2026-10-22 — Exportação Maker e organização do repositório

**Resultado:** relatório Maker HTML/JSON implementado; projeto organizado por plataforma e responsabilidade. Suíte completa .NET 8 aprovada antes e depois das movimentações, com zero avisos/erros. Nenhum commit ou staging realizado; a entrega prepara os arquivos para commit manual pelo usuário.

### Exportação Maker

- `Linux/Hardware/Reports/SerialReportBuilder.cs`: esquema `linux-serial` v1, cópia de portas/USB/aliases/grupos/avisos, datas separadas, HTML escapado, JSON estruturado, estados ausente/vazio/falha.
- `Linux/Views/MainWindow.Serial.cs`: mantém última coleta concluída e erro; falha limpa snapshot, sucesso posterior limpa erro.
- `Linux/Views/MainWindow.Export.cs`, `MainWindow.axaml`, `MainWindow.axaml.cs`: botões HTML/JSON, informação sobre identificadores by-id, tipo Maker integrado ao diálogo e bloqueio durante benchmarks/exportações.
- `Linux/SmokeTests/Reports/SerialReportChecks.cs` e `Program.cs`: casos de snapshot profundo, escaping, identificação, aliases, booleanos/nulos, datas e falhas, no modo `--report-only` e conjunto padrão.

### Organização

- Base WPF movida da raiz para `Windows/`, incluindo drivers e publicação local; imagens compartilhadas continuam em `Assets/`.
- Renderizador/simulação movidos para `Shared/Benchmarks/` e vinculados aos projetos Windows/Linux. Conferidos 65 arquivos de código/XAML Windows/compartilhados contra HEAD: conteúdo preservado byte a byte.
- Telas Linux em `Views/`, tema em `Styles/`, Core/Hardware/SmokeTests divididos por assunto. Projetos Linux e `validate.sh` mantêm seus caminhos.
- Ajustados referências de fontes/recursos, URI do tema, coleta recursiva do modo suplementar .NET 10, workflows Linux/Windows e gitignore de drivers/artefatos.
- Guia e registro completos agora em `docs/`, dentro da raiz Git. Os documentos externos são apontadores. Entradas antigas preservam caminhos históricos.
- Novo README raiz, `Shared/README.md` e `docs/ESTRUTURA.md`; README Windows preserva suas instruções com caminhos atualizados.

### Validação e limites

`bash Linux/validate.sh --offline-net8` aprovado após organização, com zero avisos/erros e todos os cenários. `bash -n`, XML, nomes de controles, eventos, referências de projetos/fontes/recursos e `git diff --check` aprovados. Seleção recursiva de fontes do modo .NET 10 conferida estaticamente; esse runtime não está instalado nesta continuação.

Build/execução Avalonia e WPF não validados aqui; as referências foram verificadas estaticamente. Diálogos de exportação e interação gráfica continuam pendentes. O relatório não abre dispositivos nem coleta hardware ao exportar; os caminhos by-id podem conter números de série. A reorganização não altera a metodologia dos benchmarks nem as funcionalidades Windows. `Windows/publicar.bat` passa a gerar `ResetExplode-dist/` na raiz do repositório, ignorada pelo Git. O workflow Windows continua gerando `dist/` na raiz.

## 2026-10-22 — Maker: associação de aliases persistentes by-id

**Resultado:** caminhos `/dev/serial/by-id` associados às portas observadas e exibidos no cartão/terminal. Core/Hardware/SmokeTests compilados em .NET 8 com zero avisos/erros; suíte completa aprovada.

| Arquivos relativos ao fork | Alterações e motivo |
| --- | --- |
| `Linux/Hardware/LinuxFiles.cs` | Resolução somente de leitura de links para arquivos existentes, com tratamento de erros e destino fora da raiz de leitura. |
| `Linux/Hardware/LinuxSerialCollector.cs` | Coleção de aliases por porta, ordenação estável, snapshot somente de leitura e aviso de entradas enumeradas sem associação. Exige destino exato e porta observada em `/dev`. |
| `Linux/MainWindow.axaml`, `Linux/SmokeTests/Program.cs` | Apresentação de aliases no cartão e em `--live-serial`. |
| `Linux/SmokeTests/SerialAliasChecks.cs`, `Linux/SmokeTests/SerialChecks.cs` | Links relativos/absolutos/encadeados, múltiplos aliases, espaços, links quebrados/circulares, arquivo regular, porta fora do escopo, destino fora da raiz, troca de destino, remoção e snapshot anterior. Incluídos na suíte e em `--serial-only`. |
| `Linux/README.md`, guia e registro | Escopo, comandos existentes, identificadores nos nomes e pendências atualizados. |

**Validações:** `bash Linux/validate.sh --offline-net8` passou, zero avisos/erros. XML/binding verificados estaticamente. Diagnóstico real executado com sucesso, sem portas observadas neste ambiente; não comprova associação em hardware conectado.

**Limites:** não abre portas nem altera links/permissões. Ausência de aliases não implica defeito; diretório indisponível pode produzir lista vazia. Coleta não atômica e sem garantia de identidade após reconexão. Nomes by-id podem conter números de série; não há exportação/persistência automática. Hardware conectado e build/execução Avalonia continuam pendentes. Próxima extensão independente: relatório do inventário Maker.

## 2026-10-22 — Maker: identificação USB e driver associado

**Resultado:** identificação USB adicionada ao coletor, cartão Maker e terminal. Core/Hardware/SmokeTests compilados em .NET 8 com zero avisos/erros e suíte completa aprovada. Interface verificada estaticamente; hardware serial conectado não disponível para confirmação.

| Arquivos relativos ao fork | Alterações e motivo |
| --- | --- |
| `Linux/Hardware/LinuxFiles.cs` | Resolução de diretórios/links para caminhos da raiz de leitura; trata ausência, falha e destino fora da raiz. |
| `Linux/Hardware/LinuxSerialCollector.cs` | Modelo de identidade USB com caminhos de origem, fabricante/produto reportados, VID/PID validados e driver mais próximo da porta/interface. Percorre pais em sysfs com limite e cancelamento, parando no dispositivo USB identificado mais próximo. |
| `Linux/MainWindow.axaml`, `Linux/SmokeTests/Program.cs` | Apresentação dos dados no cartão e em `--live-serial`. |
| `Linux/SmokeTests/SerialUsbChecks.cs`, `Linux/SmokeTests/SerialChecks.cs` | Fixtures com links relativos, estruturas USB serial/CDC ACM, origem do driver, hub ancestral, atributos ausentes/inválidos, link de driver quebrado, link circular, remoção e snapshot preservado. Incluídas em `--serial-only` e suíte padrão. |
| `Linux/README.md`, guia e registro | Comportamento, fontes e limites atualizados. |

**Validação:** `bash Linux/validate.sh --offline-net8` aprovado, zero avisos/erros. XML e binding da descrição USB verificados. `--live-serial` executado; nenhuma porta observada no ambiente, mantendo uucp identificado e dialout indisponível.

**Limites:** não abre portas, não lê o atributo de número de série, não infere produto pelo VID e não altera drivers/permissões. Campos reportados não comprovam autenticidade ou funcionamento. Snapshot não atômico; remoção durante a leitura pode resultar em informação parcial. Aliases by-id, exportação e ações de configuração continuam pendentes, assim como hardware conectado e build/execução Avalonia. Base Windows preservada.

## 2026-10-22 — Maker: inventário inicial de portas seriais Linux

**Resultado:** coletor e testes compilados em .NET 8 com zero avisos/erros; suíte completa aprovada. Cartão de interface implementado com verificação estática, ainda sem build/execução Avalonia.

| Arquivos relativos ao fork | Alterações e motivo |
| --- | --- |
| `Linux/Hardware/LinuxSerialCollector.cs` | Modelos de portas/grupos e coleta somente de leitura. Cruza ttyUSB/ttyACM em sysfs e `/dev`, valida major:minor e consulta associação ativa a dialout/uucp. Ausência/ambiguidade não vira acesso confirmado. |
| `Linux/MainWindow.Serial.cs` | Coleta inicial/manual em segundo plano, sem sobreposição, apresentação de estados/falhas e cancelamento. |
| `Linux/MainWindow.axaml`, `Linux/MainWindow.axaml.cs` | Cartão Maker reutilizando estilos existentes; abertura independente do inventário de discos e espera/cancelamento no fechamento. |
| `Linux/SmokeTests/SerialChecks.cs`, `Linux/SmokeTests/Program.cs` | Cenários temporários e modos `--serial-only`/`--live-serial`; testes incluídos na suíte padrão. |
| `Linux/README.md`, guia e registro | Comandos, fontes de dados, comportamento e limites documentados. |

**Validações:** `bash Linux/validate.sh --offline-net8` passou com zero avisos/erros. Testes cobrem fontes distintas, deduplicação, nomes inválidos, grupos efetivo/suplementares, rejeição de GID real/salvo como associação ativa, dados ausentes/malformados/ambíguos, remoção, snapshots e cancelamento. XML, nomes únicos e referências dos novos controles verificados. Coleta real não observou portas; uucp identificado como GID 984 não ativo, dialout indisponível.

**Limitações:** nenhuma porta foi aberta, nenhuma configuração alterada. Associação a grupo não comprova acesso efetivo. Usa somente grupos locais, sem NSS; não verifica ACLs, comunicação ou porta ocupada. Identificação USB/driver, aliases by-id, exportação, regras udev e gestão de grupos não implementados. Hardware serial conectado e ciclo gráfico continuam pendentes. Base Windows `UsbCleaner.cs` revisada e preservada. A indisponibilidade NuGet registrada anteriormente continua impedindo a validação Avalonia.

## 2026-10-22 — Diagnóstico explícito com smartctl instalado

**Resultado:** integração executada com smartctl 7.5 real. `nvme0n1` aparece em sysfs, mas o nó `/dev/nvme0n1` não está disponível neste ambiente; a consulta produziu LEITURA INCOMPLETA e aviso de falha de abertura, sem métricas ou aprovação indevidas. Isso não valida a saúde/leitura efetiva do disco.

| Arquivos relativos ao fork | Alterações e motivo |
| --- | --- |
| `Linux/SmokeTests/Program.cs` | Modo opt-in `--live-smart <nome>` chama o leitor real, apresenta somente dados tratados e aceita cancelamento por Ctrl+C. Rejeita argumentos extras/ausentes; não consulta discos na suíte padrão. |
| `Linux/Hardware/LinuxSmartReader.cs` | Rejeita também `.` e `..`, que passavam pela lista de caracteres permitidos e identificavam diretórios em vez de dispositivos. |
| `Linux/SmokeTests/SmartChecks.cs` | Cenários de nomes vazios, caminhos, pontos e metacaracteres. |
| `Linux/README.md`, guia e registro | Documentados comando, semântica de saída e limite da validação real. |

**Validação:** `bash Linux/validate.sh --offline-net8` passou com zero avisos/erros e toda a suíte aprovada. Diagnóstico com `nvme0n1` retornou relatório e saída 0; ausência de nome, `..` e combinação com outra opção retornaram 2. Saída 0 documentada como relatório gerado, não como disco saudável. Cancelamento do leitor é coberto pelos testes de processo; envio interativo de Ctrl+C neste novo modo não foi testado.

**Pendências:** leitura efetiva de saúde em ambiente com o dispositivo acessível, permissões reais, bridges e interação gráfica. Avalonia continua pendente pelo acesso NuGet registrado nas etapas anteriores. Nenhuma configuração do sistema/disco ou implementação Windows foi alterada.

## 2026-10-22 — Validação do processo SMART e espera de encerramento

**Resultado:** suíte completa aprovada em .NET 8, com novos testes que executam um programa SMART simulado. Build de Core/Hardware/SmokeTests com zero avisos e zero erros.

| Arquivos relativos ao fork | Alterações e motivo |
| --- | --- |
| `Linux/Hardware/LinuxSmartReader.cs` | Construtor interno permite injetar executável e prazo nos testes; construtor público mantém smartctl e 15 segundos. Após solicitar encerramento, aguarda a saída do processo antes de liberar recursos/retornar. |
| `Linux/SmokeTests/SmartProcessChecks.cs` | Executável temporário com caminho contendo espaço; confere argumentos, código de saída, JSON inválido, executável ausente, omissão de stderr, drenagem das pipes, limite em ambas as saídas, timeout e cancelamento em andamento. Confere que o PID terminou antes do retorno. |
| `Linux/SmokeTests/Program.cs` | Inclui testes de processo em `--smart-only` e na suíte padrão. |
| `Linux/README.md`, guia e registro | Atualizados escopo validado e pendências. |

**Validação:** `bash Linux/validate.sh --offline-net8` passou. Os testes usam `/bin/sh`, `head` e `sleep`, com arquivos temporários removidos ao terminar; não consultam dispositivos nem alteram o PATH. O teste de encerramento usa `exec sleep` no mesmo PID; não comprova encerramento de descendentes ou comportamento de I/O de kernel não interrompível.

**Pendências:** nova verificação do NuGet falhou na resolução DNS de `api.nuget.org`; build Avalonia/UI permanecem bloqueados. Consulta com smartctl/disco real, permissões reais, suporte de bridges e fechamento pela janela ainda não foram validados. A integração de processo agora tem cobertura real com respostas simuladas, sem ser apresentada como validação de hardware.

## 2026-10-22 — Validação dos projetos originais em .NET 8

**Resultado:** Core, Hardware e SmokeTests compilados com SDK 8.0.132, zero avisos/erros e suíte completa aprovada no runtime 8.0.32. Diferentemente da verificação anterior em assembly único .NET 10, esta execução preserva os projetos e referências originais.

| Arquivos | Alterações e motivo |
| --- | --- |
| `ResetExplodeForLinux/Linux/validate.sh` | Novo `--offline-net8`: compila os projetos originais sem fontes NuGet, desabilita download de targeting packs e executa a suíte. Configuração temporária removida ao terminar. Fluxo padrão/CI continua exigindo Avalonia. |
| `ResetExplodeForLinux/Linux/README.md`, guia e registro | Atualizados resultados, comando reproduzível e pendências. |

**Validações:** build e suíte completa executados diretamente e novamente pelo novo comando, todos com saída 0; `bash -n` aprovado. Persistência, relatórios, inventário/montagens, alimentação, SMART simulado, telemetria simulada, política térmica, cancelamento real de render e simulação curta passaram. Coleta `--live-telemetry` em .NET 8 confirmou CPU/hwmon/cpufreq; NVML retornou código 9.

**Limites do ambiente:** a inicialização do SDK/NuGet encontrou falha no mutex `NuGet-Migrations`. Para a sessão, foram usados diretórios temporários para CLI/cache/dados e marcadores de primeira execução/migração em `/tmp/reset-session`, sem modificar a configuração do usuário. O restore normal então retornou NU1301; a tentativa foi interrompida após confirmar a falha. O build offline também exigiu `EnableTargetingPackDownload=false` para evitar download de ASP.NET Ref não utilizado pelos projetos. Não houve solicitação nem rejeição de aprovação.

**Pendências:** build Avalonia, abertura da janela, diálogos, atualização automática gráfica, SMART com processo real e benchmarks completos. Nenhum algoritmo da base Windows foi alterado. Próximo passo: `bash Linux/validate.sh` em ambiente com pacotes NuGet acessíveis e validação dos fluxos da interface.

## 2026-10-22 — Primeira execução acumulada dos testes e comando de validação

**Resultado:** compilação suplementar de todos os fontes Core/Hardware/SmokeTests em .NET 10 concluída com **zero avisos e zero erros**. A suíte completa passou, incluindo persistência, relatórios, telemetria, armazenamento/montagens, alimentação, SMART simulado, política térmica, cancelamento de render real e simulação curta. Nenhuma correção de lógica foi necessária nos cenários executados.

| Arquivos relativos ao fork | Alterações e explicação |
| --- | --- |
| `Linux/validate.sh` | Comando normal compila Avalonia, compila testes e executa a suíte. Modo explícito `--offline-net10` cria projeto temporário com os mesmos fontes, sem NuGet, e remove artefatos ao terminar. Não altera o alvo net8.0. |
| `.github/workflows/linux.yml` | Passa a usar o comando normal de validação com .NET 8, mantendo build da interface obrigatório. |
| `Linux/README.md`, guia e registro | Estado de validação atualizado, comandos reproduzíveis e distinção entre validação suplementar e alvo original. |

### Evidências e limites

- SDK 10.0.113/runtime 10.0.13 instalados; SDK/runtime .NET 8 ausentes localmente.
- Build original foi tentado. Restrições de escrita nos diretórios do usuário foram contornadas com caches em `/tmp`; restauração permaneceu bloqueada por NU1301 (NuGet inacessível/acesso negado). Não houve aprovação rejeitada nem solicitação de elevação.
- Projeto temporário net10.0 compilou e executou toda a suíte com saída 0. O novo `validate.sh --offline-net10` reproduziu o resultado, também com saída 0; sintaxe shell verificada com `bash -n`.
- `--live-telemetry` executado no assembly temporário: leituras reais de CPU, hwmon e cpufreq disponíveis. NVML retornou código 9 ao inicializar; leitura NVIDIA não validada.
- Projeto temporário junta os fontes em um assembly: não substitui build das referências originais, .NET 8 ou Avalonia. UI/diálogos, atualização periódica gráfica, SMART com processo real e benchmarks completos continuam pendentes.
- Os cenários térmicos usam leituras simuladas; a suíte não executa render completo nem banda RAM de 2 GiB. Os registros históricos anteriores continuam preservados.

Próxima validação no ambiente com SDK/runtime .NET 8 e NuGet acessível: `bash Linux/validate.sh`, depois iniciar a aplicação e verificar os fluxos gráficos.

## 2026-10-22 — Exportação de bateria e alimentação

**Estado:** implementação escrita; build, testes C# e validação gráfica pendentes por ausência do SDK .NET.

| Arquivos relativos ao fork | Alterações e explicação |
| --- | --- |
| `Linux/Hardware/PowerReportBuilder.cs` | Relatório HTML/JSON versionado com cópia das coleções e métricas, datas separadas, avisos e erro de coleta. Escapa textos HTML e preserva valores/unidades apresentados. |
| `Linux/MainWindow.Power.cs` | Mantém última coleta concluída para exportar; falha limpa snapshot e guarda motivo, sucesso posterior limpa erro. |
| `Linux/MainWindow.Export.cs` | Seleção explícita do tipo de relatório (benchmarks, armazenamento, alimentação), reutilizando diálogo, escrita e tratamento de erros. |
| `Linux/MainWindow.axaml`, `Linux/MainWindow.axaml.cs` | Botões HTML/JSON e status no cartão de alimentação, com bloqueio durante benchmarks/exportações. |
| `Linux/SmokeTests/PowerReportChecks.cs`, `Linux/SmokeTests/Program.cs` | Testes de cópia profunda, escaping, valores/unidades, datas, ausência, coleta vazia e erro; incluídos em `--report-only` e conjunto padrão. |
| `Linux/README.md`, guia e registro | Escopo, formato e limitações de validação documentados. |

Não há nova leitura de hardware ao exportar. Atualizações automáticas não modificam o snapshot capturado. Métricas do JSON mantêm textos/unidades da coleta, sem promessa de histórico numérico ou diagnóstico. Relatórios anteriores e base Windows preservados.

**Validação:** verificações estáticas de XML, referências, eventos e whitespace. SDK .NET ausente; testes C# não executados. Validar `--report-only`, conjunto completo, exportação durante atualização automática e os três tipos de relatório na interface.

## 2026-10-22 — Atualização automática de bateria e alimentação

**Estado:** implementação escrita; build, testes C# e ciclo gráfico ainda não validados. SDK .NET não encontrado no PATH nem nos diretórios comuns verificados.

| Arquivos relativos ao fork | Alterações e explicação |
| --- | --- |
| `Linux/MainWindow.Power.cs` | Loop iniciado uma única vez, leitura imediata e espera cancelável de cinco segundos após cada ciclo. Botão manual e loop compartilham a tarefa ativa, evitando sobreposição. Falhas de coleta não encerram as tentativas seguintes. |
| `Linux/MainWindow.axaml.cs` | Inicia o monitor de alimentação independentemente do inventário de discos. Fechamento cancela e aguarda o loop e a coleta antes de descartar recursos. |
| `Linux/MainWindow.axaml` | Texto informa atualização automática e uso do botão para antecipar leitura. |
| `Linux/SmokeTests/PowerChecks.cs` | Adicionados cenários de mudança de carga/estado, independência entre fonte e bateria, snapshots anteriores preservados e reaparecimento sem métricas antigas. |
| `Linux/README.md`, guia e registro | Atualizados comportamento, limitações e estado de validação. |

O intervalo real inclui coleta e processamento da interface. O monitor continua nas outras páginas e durante benchmarks; permanece separado do loop térmico. Exportação da alimentação e políticas de energia seguem pendentes. Nenhum algoritmo Windows foi alterado.

**Validação:** verificações estáticas de XML, referências locais, eventos e whitespace. Testes C# não executados; os cenários de `--power-only` verificam o coletor, não o agendamento da UI. Em ambiente com .NET, executar o conjunto e verificar atualização após mudanças na alimentação, clique manual durante coleta, repetição após falha e fechamento durante leitura/espera.

## 2026-10-22 — Bateria e alimentação via power_supply

**Estado:** implementação escrita; build, testes C# e validação gráfica pendentes por ausência do SDK .NET.

| Arquivos relativos ao fork | Alterações e explicação |
| --- | --- |
| `Linux/Hardware/LinuxPowerCollector.cs` | Modelos e leitura por fonte/bateria em sysfs. Estado, presença, carga, ciclos e grandezas com unidades distintas; dados inválidos/ausentes não viram zero. |
| `Linux/MainWindow.Power.cs` | Coleta inicial/manual em segundo plano, sem sobreposição, com mensagens de falha e cancelamento no fechamento. |
| `Linux/MainWindow.axaml`, `Linux/MainWindow.axaml.cs` | Cartão de bateria/alimentação, apresentação por fonte e integração à abertura/fechamento da janela. |
| `Linux/SmokeTests/PowerChecks.cs`, `Linux/SmokeTests/Program.cs` | Testes com sysfs temporário: pt-BR, unidades, sinais, zero, ausência, escopo, dados inválidos, remoção e cancelamento. Modo `--power-only` e conjunto padrão. |
| `Linux/README.md`, guia e registro | Documentados comportamento, limitações e comandos. |

A base Windows `BatteryInfo.cs` foi lida e preservada. Energia (Wh) e carga elétrica (Ah) não são confundidas; corrente e temperatura podem preservar sinais negativos. O coletor não infere conexão global à tomada, desgaste, autonomia ou saúde a partir de leituras parciais. Não altera políticas ou limites de carga. Coleta periódica e exportação de alimentação permanecem pendentes.

**Validação:** revisão estática de XML, referências, eventos e whitespace. SDK .NET ausente; testes C# e UI não executados. Validar `--power-only`, conjunto completo e atualização em máquina real com bateria/fonte.

## 2026-10-22 — Exportação de armazenamento e última consulta SMART

**Estado:** implementação escrita; compilação, testes C# e validação gráfica pendentes por ausência do SDK .NET.

| Arquivos relativos ao fork | Alterações e explicação |
| --- | --- |
| `Linux/Hardware/StorageReportBuilder.cs` | Relatório próprio HTML/JSON com dispositivos, montagens, avisos e última consulta SMART; cópia das coleções, datas independentes, escaping HTML e valores numéricos de capacidade preservados. |
| `Linux/MainWindow.Storage.cs`, `Linux/MainWindow.Smart.cs` | Mantêm modelos e horários da sessão para exportação. Falha de inventário limpa o modelo; nova consulta SMART limpa o resultado anterior. |
| `Linux/MainWindow.Export.cs` | Reutiliza diálogo e escrita para escolher exportação de benchmarks ou armazenamento; mantém status separado por área. |
| `Linux/MainWindow.axaml`, `Linux/MainWindow.axaml.cs` | Botões HTML/JSON no cartão de armazenamento e integração ao bloqueio durante benchmarks/exportações. |
| `Linux/SmokeTests/StorageReportChecks.cs`, `Linux/SmokeTests/Program.cs` | Verificações de snapshot independente, escaping, precisão de inteiros, distinção de espaço disponível/livre, datas independentes e ausência de SMART, incluídas em `--report-only` e conjunto padrão. |
| `Linux/README.md`, guia e registro | Documentados escopo, dados incluídos e pendências de validação. |

A exportação não coleta hardware novamente e não associa automaticamente um SMART antigo ao inventário atual. Relatórios de armazenamento e benchmarks são separados. Inclui caminhos e origens de montagem, que podem conter identificadores do sistema; não exporta campos de série nem JSON bruto do smartctl. Base Windows preservada.

**Validação:** revisão estática de XML, referências, eventos e whitespace. SDK .NET ausente; testes C# não executados. Validar `--report-only`, conjunto completo e exportação gráfica com inventário vazio, consulta SMART falha e arquivo existente.

## 2026-10-22 — Consulta SMART manual por dispositivo

**Estado:** implementação escrita; build, testes C#, consulta pela UI e validação de processo pendentes. `smartctl --version` confirmou smartmontools 7.5; o SDK .NET segue ausente.

| Arquivos relativos ao fork | Alterações e explicação |
| --- | --- |
| `Linux/Hardware/LinuxSmartReader.cs` | Execução sem shell de smartctl JSON, prazo de 15 s, limite de saída, drenagem simultânea e encerramento solicitado em cancelamento. Parser de estados SMART, bits de saída e métricas disponíveis, incluindo alertas/endurance/erros NVMe. |
| `Linux/MainWindow.Smart.cs` | Consulta manual única por vez, resultado identificado por dispositivo/horário e falhas apresentadas na interface. |
| `Linux/MainWindow.Storage.cs`, `Linux/MainWindow.axaml` | Seleção dos dispositivos inteiros do inventário e botão CONSULTAR SMART. Não há seleção de partições nem digitação livre de comandos. |
| `Linux/MainWindow.axaml.cs` | Aguarda consulta SMART após solicitar cancelamento no fechamento. |
| `Linux/SmokeTests/SmartChecks.cs`, `Linux/SmokeTests/Program.cs` | JSON simulado, bits de saída, ausência/erro, NVMe, temperatura negativa, rejeição de nome inválido e cancelamento prévio; modo `--smart-only` e conjunto padrão. |
| `Linux/README.md`, guia e registro | Escopo, dependência, estados e validações pendentes documentados. |

`DiskHealth.cs` Windows foi revisado e preservado. Uma leitura com temperatura/horas, mas sem avaliação SMART, não vira aprovação. Históricos de erros não são apresentados como confirmação de falha atual. Não há elevação automática, alteração de parâmetros ou disparo de autotestes; a consulta pode acordar discos em repouso. Números de série e saída bruta não são exibidos/persistidos. Exportação SMART, bridges/RAID específicos e benchmarks de disco permanecem pendentes.

**Validação:** verificações estáticas de XML, referências, eventos e whitespace. Testes C# não executados. Próxima validação: build, `--smart-only`, conjunto completo e cenários reais de acesso negado, dispositivo sem suporte, timeout e fechamento durante a consulta.

## 2026-10-22 — Montagens e espaço dos sistemas de arquivos

**Estado:** implementação escrita; build, testes C# e interface pendentes por ausência do SDK .NET.

| Arquivos relativos ao fork | Alterações e explicação |
| --- | --- |
| `Linux/Hardware/LinuxMountCollector.cs` | Parser de mountinfo, modelos de montagem e contadores de capacidade. Trata campos opcionais/escapes, distingue livre total de disponível ao usuário e consulta formatos locais reconhecidos com DriveInfo. Provedor de espaço injetável permite testes isolados. |
| `Linux/MainWindow.Storage.cs`, `Linux/MainWindow.axaml` | Integra montagens à coleta inicial/atualização manual e apresenta origem, tipo, modo de leitura, capacidade e avisos por montagem. |
| `Linux/SmokeTests/MountChecks.cs`, `Linux/SmokeTests/Program.cs` | Testes simulados de parsing, escapes sem dupla decodificação, contadores, falhas de acesso, montagens sobrepostas, remoção e cancelamento. Modo `--mounts-only`, integração a `--inventory-only` e conjunto padrão. |
| `Linux/README.md`, guia e registro | Comportamento, comandos e limites documentados. |

Capacidades compartilhadas não são somadas. Rede, FUSE, autofs e formatos não reconhecidos são listados sem consulta automática de espaço. Montagens sobrepostas não recebem medidas por caminho que poderiam pertencer a outro mount ID. Cancelamento é cooperativo entre consultas e não interrompe chamada nativa pendente; o inventário pode mudar durante a leitura. SMART, benchmarks e exportação de inventário permanecem pendentes. Base Windows preservada.

**Validação:** revisão estática de XML, referências, eventos e whitespace. SDK .NET ausente: testes C# não executados. Validar com `--mounts-only`, conjunto completo e comparação da apresentação com as montagens reais em ambiente Linux com .NET 8.

## 2026-10-22 — Inventário de dispositivos de armazenamento

**Estado:** implementação escrita; build, testes C# e validação gráfica pendentes por ausência do SDK .NET.

| Arquivos relativos ao fork | Alterações e explicação |
| --- | --- |
| `Linux/Hardware/LinuxStorageCollector.cs` | Modelos e coleta somente de leitura de dispositivos e partições via sysfs. Capacidade em setores de 512 bytes com proteção contra overflow; flags e modelo ausentes permanecem indisponíveis. |
| `Linux/MainWindow.Storage.cs` | Coleta inicial e atualização manual em segundo plano, sem sobreposição e separadas do monitoramento térmico. Cancelamento e espera ao fechar. |
| `Linux/MainWindow.axaml`, `Linux/MainWindow.axaml.cs` | Cartão de armazenamento na página de sistema, apresentação de dispositivos/partições e integração ao ciclo de vida da janela. |
| `Linux/SmokeTests/StorageInventoryChecks.cs`, `Linux/SmokeTests/Program.cs` | Testes com sysfs temporário: partições, unidade de capacidade, flags, ausência, zero, overflow, valores inválidos, remoção e cancelamento. Novo modo `--inventory-only`, incluído no conjunto padrão. |
| `Linux/README.md`, guia e registro | Escopo do inventário, limitações e instruções para validar. |

`DriveMap.cs` da base Windows foi revisado como referência e preservado. O destino usa dispositivos/partições Linux, sem WMI ou letras de unidade. Não soma capacidades sobrepostas nem interpreta rotação como identificação garantida de SSD/HDD. Montagens, espaço livre, SMART, benchmarks de disco e inclusão no relatório permanecem pendentes.

**Validação:** revisão estática de XML, eventos, referências e whitespace. SDK .NET ausente; nenhum teste C# ou interface foi executado. Validar com `--inventory-only`, conjunto completo e atualização após conectar/remover um dispositivo em sessão gráfica Linux.

## 2026-10-22 — Exportação do histórico em HTML e JSON

**Estado:** implementação escrita; build, testes C# e validação gráfica pendentes por ausência do SDK .NET.

| Arquivos relativos ao fork | Alterações e explicação |
| --- | --- |
| `Linux/Core/BenchmarkReportBuilder.cs` | Adapta a função de relatórios da base Windows para o histórico Linux. Captura até 200 registros, copia métricas/contexto, identifica pendências, gera HTML com texto escapado e JSON estruturado. Execuções incompletas não exportam pontuação parcial. |
| `Linux/MainWindow.Export.cs` | Diálogo de destino via StorageProvider, geração em segundo plano, escrita UTF-8, cancelamento e mensagens de sucesso/falha. |
| `Linux/MainWindow.axaml`, `Linux/MainWindow.axaml.cs`, `Linux/MainWindow.Persistence.cs` | Botões HTML/JSON e status; bloqueio de novos testes/ações de persistência durante exportação e espera da exportação ao fechar a janela. |
| `Linux/SmokeTests/ReportChecks.cs`, `Linux/SmokeTests/Program.cs` | Testes de escaping, snapshot independente, cultura pt-BR, estados, registros pendentes, deduplicação, retenção e histórico vazio; modo `--report-only` e inclusão no conjunto padrão. |
| `Linux/README.md`, guia e registro | Escopo, comportamento, comandos e pendências de validação documentados. |

O arquivo Windows `ReportBuilder.cs` foi lido como referência e preservado. Seu vínculo com `InfoNode` e as seções Windows não foram incorporados ao núcleo Linux. Esta etapa exporta benchmarks; relatório completo de hardware/sensores/discos permanece pendente. Exportar não marca registros pendentes como persistidos, nem cria um formato de importação. Falha na escrita pode deixar o arquivo escolhido incompleto, informada na interface.

**Validação:** verificações estáticas de XML, referências, ligação de eventos e whitespace. SDK .NET ausente; testes C# não executados. Para validar: executar `--report-only`, o conjunto completo, abrir os dois formatos exportados e conferir cancelamento, substituição de arquivo existente e fechamento durante exportação em sessão gráfica Linux.

## 2026-10-22 — Configurações térmicas e histórico persistente

**Estado:** implementação escrita; compilação, testes C# e execução gráfica pendentes por ausência do SDK .NET. As entradas anteriores descrevem o estado histórico de cada etapa.

| Arquivos relativos ao fork | O que foi alterado/adicionado e explicação |
| --- | --- |
| `Linux/Core/PersistenceModels.cs` | Modelos JSON versionados para preferências, medições e histórico; resolução de caminhos XDG com fallback para configuração e estado. |
| `Linux/Core/LocalPersistence.cs` | Leitura validada, gravação por arquivo temporário e substituição, lock entre escritores, backup de JSON inválido, proteção de versões desconhecidas, deduplicação por ID e retenção das 200 execuções mais recentes. |
| `Linux/MainWindow.Persistence.cs` | Restauração de preferências e histórico, fila de gravações em segundo plano, mensagens de falha e reenvio de resultados mantidos em memória. |
| `Linux/MainWindow.axaml`, `Linux/MainWindow.axaml.cs` | Botões para salvar preferências/repetir gravações, histórico visível e resultados estruturados por benchmark. Preferências são salvas antes da medição; o resultado é gravado após o teste. Fechamento aguarda gravações em andamento. |
| `Linux/MainWindow.Telemetry.cs` | Disponibiliza a identificação de CPU para o contexto dos registros. |
| `Linux/SmokeTests/PersistenceChecks.cs`, `Linux/SmokeTests/Program.cs` | Cenários com arquivos temporários para XDG, reabertura, cultura pt-BR, recuperação, versão desconhecida, permissões, lock, deduplicação, retenção e ausência de pontuação parcial. Modo `--storage-only` e inclusão no conjunto padrão. |
| `Linux/README.md`, guia e este registro | Documentados caminhos, momento da gravação, recuperação, limitações e comando de validação. |

Preferências alteradas são persistidas pelo botão ou antes do próximo teste, não automaticamente ao fechar. Histórico inclui contexto e métricas numéricas dos testes concluídos; interrupções, bloqueios, cancelamentos e erros não recebem resultados parciais. Se o disco recusar a escrita, a interface identifica as pendências; fechar com a falha ainda presente perde os registros não salvos. Credenciais DPAPI, perfis Windows e comparação entre plataformas permanecem fora desta etapa.

**Validação:** revisão estática de XML, referências locais, eventos da interface e whitespace. Os testes foram escritos, mas não executados; não há confirmação de build ou funcionamento gráfico neste ambiente. Próximo passo de validação: executar o conjunto de smoke tests e conferir restauração das preferências e do histórico após reabrir a janela em ambiente com SDK .NET 8.

## 2026-10-22 — Interrupção dos benchmarks por temperatura da CPU

**Estado:** integração escrita; build, testes C# e validação gráfica ainda pendentes. Os nomes e algoritmos da base Windows foram preservados.

### Arquivos e explicações

| Arquivos relativos ao fork | O que mudou e por quê |
| --- | --- |
| `Linux/Core/CpuThermalPolicy.cs` | Adicionados política térmica, leitura com timestamp monotônico, motivos de interrupção e monitor em thread dedicada. A política rejeita temperatura ausente/inválida, leitura com idade de cinco segundos ou mais e temperatura igual/superior ao limite. |
| `Linux/Core/BenchmarkRunner.cs` | Adicionado `RunThermalAsync`: valida antes de iniciar, verifica durante o trabalho e antes de publicar o resultado. Usa cancelamento vinculado, preservando a distinção entre motivo térmico e cancelamento manual. |
| `Linux/Hardware/TelemetryModels.cs`, `Linux/Hardware/LinuxSensorMonitor.cs` | Snapshot recebe timestamp monotônico do início da coleta; um atraso posterior na NVML não faz a temperatura parecer recém-lida. |
| `Linux/MainWindow.Telemetry.cs` | Publicação atômica da leitura térmica antes de aguardar a UI; invalidação imediata em falha/encerramento da coleta. |
| `Linux/MainWindow.axaml`, `Linux/MainWindow.axaml.cs` | Controle habilitado inicialmente, limite de 60–100 °C com valor inicial de 90 °C, estado da leitura e configuração bloqueada durante o teste. Registro da sessão diferencia não iniciado, interrompido, cancelado pelo usuário e cancelado ao fechar; resultados completos identificam o modo térmico usado. |
| `Linux/SmokeTests/ThermalChecks.cs`, `Linux/SmokeTests/Program.cs` | Testes simulados para pré-checagem, igualdade com limite, dados inválidos/antigos, interrupção durante o trabalho, descarte parcial, cancelamento manual, execução válida e liberação do monitor. Novo modo `--thermal-only`, também incluído no conjunto padrão do CI. |
| `Linux/SmokeTests/TelemetryChecks.cs` | Verificação da marcação temporal antes da consulta opcional de GPU. |
| `Linux/README.md`, guia e registro na pasta superior | Atualizados comportamento, comandos de validação e limitações reais do recurso. |

### Decisões

- O limite de 90 °C é uma configuração inicial, não uma identificação do limite do fabricante. O usuário pode ajustá-lo ou desligar explicitamente o monitoramento; essa escolha fica visível no painel e no resultado.
- O monitor verifica o último snapshot aproximadamente a cada 250 ms em thread dedicada, para não depender da UI nem do pool usado pelos benchmarks. Ele não faz novas consultas de hardware.
- O cancelamento é cooperativo. Não há promessa de parada instantânea nem de proteção de todos os componentes; o recurso considera os sensores de CPU reconhecidos. Os algoritmos originais de renderização e simulação permanecem inalterados.
- O registro é apenas da sessão; persistência ainda está pendente.

### Validação

Revisados XML, referências locais, ligação de eventos AXAML, limites configurados, whitespace e `git diff --check`. Os cenários de teste foram escritos sem produzir aquecimento real. O SDK .NET continua ausente (`command -v dotnet` sem resultado); não foram executados build, testes C# ou aplicação.

### Próximo passo

Executar o conjunto de verificações ou o modo `--thermal-only` em ambiente com .NET, validar os controles na janela e continuar a migração das páginas. Manter como pendentes a validação gráfica, a execução de NVML em hardware compatível e a comparação dos resultados com a base Windows.

## 2026-10-22 — Telemetria Linux e integração à interface

**Estado:** implementação inicial da Fase 2 escrita; build, testes C# e validação gráfica/NVML pendentes. As entradas anteriores descrevem o estado histórico de cada etapa.

### Arquivos e explicações

| Arquivos relativos ao fork | O que foi alterado/adicionado e por quê |
| --- | --- |
| `Linux/Hardware/ResetExplode.Hardware.csproj` | Projeto separado para acesso ao hardware, sem dependências WPF/Avalonia; pode ser exercitado pelos testes de console. |
| `Linux/Hardware/TelemetryModels.cs` | Modelos de sistema, sensores, RAM e snapshots; valores opcionais distinguem ausência de leitura de zero. Interface para backend opcional de GPU. |
| `Linux/Hardware/LinuxFiles.cs` | Leitura de arquivos e enumeração com tratamento de ausência/permissão; raiz substituível permite fixtures sem modificar `/proc` ou `/sys`. |
| `Linux/Hardware/LinuxSystemInfoCollector.cs` | CPU, distribuição, kernel, arquitetura, placa-mãe/BIOS e RAM via fontes Linux. Não coleta números de série. |
| `Linux/Hardware/LinuxSensorMonitor.cs` | CPU por deltas dos oito contadores principais de `/proc/stat`, RAM com `MemAvailable`, hwmon e cpufreq. Converte unidades, descarta amostras regressivas e sinaliza leituras inválidas, desativadas ou em falha. Redescobre dispositivos por coleta para evitar manter sensores removidos. |
| `Linux/Hardware/LinuxNvml.cs` | NVML opcional com carregamento de `.so.1`/`.so`, delegates Cdecl e liberação após o polling. Consulta temperatura, clocks, energia, uso e VRAM; erros de métricas aparecem como indisponibilidade. Não altera a NVML Windows nem oferece escrita/overclock. |
| `Linux/MainWindow.axaml`, `Linux/MainWindow.Telemetry.cs` | Página Sistema e sensores, navegação, filtro e resumo lateral CPU/RAM. Um único loop em segundo plano, com publicação de snapshots na thread da UI. |
| `Linux/MainWindow.axaml.cs` | Inicia coleta ao abrir e aguarda sua conclusão ao fechar, inclusive após cancelamento de benchmark, antes de liberar recursos e encerrar a janela. |
| `Linux/ResetExplode.csproj`, `Linux/SmokeTests/ResetExplode.SmokeTests.csproj` | Referência ao projeto Hardware; exclusão de suas fontes da compilação automática da UI. |
| `Linux/SmokeTests/TelemetryChecks.cs`, `Linux/SmokeTests/Program.cs` | Fixtures de procfs/sysfs para ausência, conversões, pt-BR, guest/iowait, RAM, DMI, hotplug, agregação de GPU e cancelamento. Modos `--telemetry-only` e `--live-telemetry`. O workflow Linux existente já executa o conjunto padrão, agora incluindo esses testes. |
| `Linux/README.md` | Instruções de diagnóstico, fontes oficiais e limites das leituras, incluindo calibração, containers e clocks reportados pelo driver. |
| Guia e registro na pasta superior | Estado atualizado sem declarar conclusão da validação das Fases 1/2. |

### Validação e evidências

- Inspeção somente de leitura do ambiente confirmou diretórios hwmon com chips como `coretemp`, `acpitz` e `nvme`. Isso confirma fontes disponíveis, não execução do coletor C#.
- Revisão estática de XML, referências de arquivos/projetos e correspondência entre handlers dos eventos AXAML e métodos da janela.
- Conferência de whitespace e `git diff --check`.
- Revisão das unidades/formato em documentação oficial do kernel e das funções NVML na documentação NVIDIA; links no README Linux.
- SDK .NET continua indisponível na sessão. Os novos testes foram escritos, mas **não executados**; o backend NVML e a UI continuam sem validação real.

### Pendências e limites

Executar build e testes conforme README; verificar a janela e os sensores em hardware real. O resumo térmico usa apenas chips de CPU reconhecidos e não inclui sensores ACPI genéricos. A RAM é a do sistema, não uma quota de container. Tensões não aplicam calibração específica de placa-mãe. Não há parada automática por temperatura. A telemetria fica ativa durante os testes de CPU e pode interferir nos resultados; comparabilidade de benchmarks ainda deve ser avaliada. Inventário completo de discos/módulos RAM, histórico persistente e demais páginas permanecem para etapas seguintes.

## 2026-10-22 — Fundação Avalonia no fork

**Estado:** implementação inicial escrita; build, smoke tests e validação gráfica pendentes.

### Arquivos alterados e adicionados

Os caminhos abaixo são relativos a `ResetExplodeForLinux/`, salvo os documentos de planejamento que continuam na pasta superior.

| Arquivos | Alteração e explicação |
| --- | --- |
| `Linux/ResetExplode.csproj` | Novo destino .NET 8 com Avalonia 11.3.22, mantendo assembly e produto ResetExplode; referencia núcleo independente e logo original. |
| `Linux/Program.cs`, `Linux/App.axaml`, `Linux/App.axaml.cs` | Novo ponto de entrada desktop Avalonia e inicialização do tema/janela. |
| `Linux/Theme.axaml` | Adaptação inicial da paleta monocromática, cartões, botões e tipografia do tema WPF. Controles de páginas futuras ainda não foram portados. |
| `Linux/MainWindow.axaml`, `Linux/MainWindow.axaml.cs` | Tela funcional em código para path tracing single/multithread, simulação de jogo, latência e banda de RAM; progresso, bitmap BGRA respeitando stride, cancelamento e resultados de sessão. A execução real ainda precisa de validação. |
| `Linux/Core/ResetExplode.Core.csproj` | Compila os arquivos originais `BenchRenderer.cs` e `GameSimBench.cs` por referência de fonte, evitando cópias e alterações nos algoritmos Windows. |
| `Linux/Core/MemoryBench.cs` | Extração dos métodos de RAM, excluindo I/O sem cache do Windows. Cancelamento lança exceção em vez de registrar medição incompleta. |
| `Linux/Core/BenchmarkRunner.cs` | Execução fora da UI, exclusão de testes simultâneos e descarte de resultados parciais após cancelamento. |
| `Linux/SmokeTests/ResetExplode.SmokeTests.csproj`, `Linux/SmokeTests/Program.cs` | Verificações executáveis sem pacotes de teste: pré-cancelamento, render real cancelado, concorrência, descarte parcial, liberação após erro e simulação curta. Não executam automaticamente banda de RAM ou render completo. |
| `ResetExplode.csproj` | Excluído `Linux/**` da inclusão automática do WPF, evitando classes/pontos de entrada duplicados. |
| `.github/workflows/linux.yml` | Workflow para compilar Avalonia e executar smoke tests em Ubuntu; sem publicação de release. O workflow Windows existente foi preservado. |
| `README.md`, `Linux/README.md` | Identificação do fork, comandos de execução, estrutura, origem do código e limitações desta etapa. |
| `../plano_porte_linux_reset_explode.md`, `../REGISTRO_DE_ALTERACOES.md` | Registrados o novo diretório do fork, o destino efetivamente adotado e o estado real da Fase 1. |

### Decisões de implementação

- O destino está dentro do fork, em `Linux/`, para acompanhar seu versionamento. O projeto Windows na raiz continua como base.
- Não foram renomeados arquivos existentes, namespace, produto ou assembly. `ResetExplodeLinux`, proposto inicialmente como nome do destino, foi substituído na orientação pelo caminho efetivamente adotado.
- A interface apresenta apenas os benchmarks implementados nesta etapa. Sensores, limites térmicos, histórico persistente, atalhos globais e instância única permanecem pendentes.
- A janela solicita cancelamento e aguarda o trabalho encerrar antes de fechar. Cancelamento é cooperativo, não interrupção forçada de threads.

### Validação realizada

- Parse XML de sete arquivos: três projetos novos, três arquivos AXAML e o projeto WPF original.
- Conferência da existência das referências locais de projeto, fontes compartilhadas e logo.
- Conferência estática da exclusão de `Linux/**` do WPF e da ausência de `FILE_FLAG_NO_BUFFERING` no núcleo de RAM.
- `git diff --check` sem erros nos arquivos rastreados; revisão de whitespace dos arquivos novos.
- Consulta às fontes oficiais de Avalonia/NuGet para a versão e APIs utilizadas; referências no README Linux.

### Limitações e próximos passos

`dotnet --info` falhou com `command not found`. A tentativa de acessar `https://dot.net/v1/dotnet-install.sh` falhou com `Could not resolve host`. Não foi possível instalar o SDK, restaurar pacotes, compilar ou executar os testes nesta sessão. A configuração de CI foi adicionada, mas não executada.

Validar primeiro os comandos do README Linux, a abertura da janela, o logo/tema, progresso, cancelamento, encerramento e resultados dos testes. A Fase 1 só poderá ser considerada validada após essas verificações. Em seguida, implementar a telemetria Linux da Fase 2.

## 2026-10-22 — Revisão do guia com base no código Windows

**Estado:** revisão documental concluída; implementação da migração ainda não iniciada.

### Arquivos alterados e adicionados

- **Alterado:** `plano_porte_linux_reset_explode.md`.
- **Adicionado:** `REGISTRO_DE_ALTERACOES.md`, para manter o histórico e as explicações das mudanças.

### O que mudou e por quê

| Alteração no guia | Explicação |
| --- | --- |
| Registrada a base Windows v0.16 e a ausência de projeto Linux na inspeção inicial | Distingue o código existente da arquitetura futura e define que o porte aproveitará a base atual. |
| Explicitada a criação de um destino Linux preservando o projeto Windows | Evita interpretar a mudança do framework como alteração direta do projeto original. |
| Substituídas garantias percentuais de portabilidade por classificações sujeitas a validação | A leitura estática não comprova compilação ou comportamento no Linux. A matriz e as fases foram ajustadas junto com as descrições dos arquivos. |
| Separados os testes de RAM e de carregamento em `MemoryBench.cs` | `MeasureLoading` e `ReadAssets` usam `FILE_FLAG_NO_BUFFERING` com `File.OpenHandle`; o arquivo completo não pode ser tratado como independente do Windows. |
| Corrigida a descrição de `DiskBench.cs` | O código usa `File.OpenHandle` e uma opção específica do Windows, sem chamada P/Invoke direta a `CreateFileW`. O guia passa a prever uma abstração de I/O compartilhável com o teste de carregamento. |
| Corrigidos os papéis de `HardwareErrors.cs` e `OcCheck.cs` | O primeiro é um modelo de resultado; o segundo coleta eventos e consulta a placa-mãe. A migração precisa atuar no coletor. |
| Localizada a dependência DPAPI em `AppSettings.cs` | A substituição do armazenamento de credenciais deve ocorrer na persistência, não apenas no cliente HowManyFPS. |
| Detalhado o acoplamento dos arquivos parciais de `MainWindow` | São partes da mesma classe com estado compartilhado. Migrar a interface exige adaptar comportamento e separar serviços, além de converter XAML. |
| Incluídos instância única, ativação da janela, temporizadores, atalhos e notificações no escopo | Esses comportamentos dependem de APIs ou mecanismos da interface e do Windows. |
| Ajustada a orientação de NVML | Alterar o nome da biblioteca não comprova funcionamento; carregamento, interop, capacidades e permissões precisam ser verificados. |
| Explicitada a preservação da metodologia dos benchmarks | Aquecimento, repetição, cancelamento e interferência dos sensores afetam a utilidade dos resultados após o porte. |
| Adicionado o acompanhamento por etapa | Cada implementação futura deverá registrar arquivos, motivos, validações e limitações neste histórico. |

### Validação

Revisão estática do guia contra o `.csproj` e os módulos citados, com conferência das alterações documentais. Nenhum código da aplicação foi alterado. Não foram executados build, aplicativo ou benchmarks; esta entrada não declara compatibilidade Linux validada.

### Próxima etapa planejada

Iniciar a Fase 1 do guia: preparar o projeto Avalonia de destino, aproveitar o tema e os algoritmos da base Windows e validar os primeiros benchmarks no Linux. As integrações propostas no guia ainda precisam de verificação técnica durante suas respectivas fases.

## Modelo para próximas entradas

### AAAA-MM-DD — Descrição da etapa

- **Estado:** planejada, em andamento ou concluída.
- **Arquivos alterados/adicionados:** caminhos relativos.
- **O que mudou:** comportamento ou estrutura alterada.
- **Explicação:** motivo e relação com a base Windows.
- **Validação:** verificações realizadas e seus resultados.
- **Pendências:** limitações e próximos passos concretos.
