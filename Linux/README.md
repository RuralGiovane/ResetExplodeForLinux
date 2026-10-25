# RESET EXPLODE — base Avalonia para Linux

Fundação Avalonia e telemetria inicial da migração do projeto Windows v0.16. O repositório é o fork `ResetExplodeForLinux`; o produto, assembly e namespace continuam `ResetExplode`.

## Validação atual — 24/10/2026

Builds Avalonia/Core/Hardware/SmokeTests com dependências locais (`--no-restore`) aprovados em .NET 8, zero avisos/erros; suíte completa aprovada. Acesso ao NuGet e à sessão gráfica permanece limitado no ambiente do agente.

Usuário aprovou o roteiro gráfico básico, cancelamento/proteção térmica/fechamento de benchmarks, GPU/NVML, fluxo SMART administrativo completo e instância única. Maker conectado permanece pendente por falta de dispositivo. Build Windows e metodologia dos benchmarks completos continuam pendentes.

Inventário local de rede aprovado pelo usuário. Endereços IPv4/IPv6, gateways e DNS local adicionados; testes simulados aprovados. Coleta real de DNS e contadores funcionou, mas endereços/gateways ficaram indisponíveis no ambiente do agente (netlink bloqueado). Endereços/gateways reais e apresentação/atualização de todos os novos campos aprovados pelo usuário na sessão gráfica. Os estados descritos nas seções históricas abaixo podem anteceder essas confirmações.

Na raiz do fork, com SDK/runtime .NET 8 e acesso aos pacotes:

```bash
bash Linux/validate.sh
```

O comando compila Avalonia e o projeto de testes e executa o conjunto completo; também é usado no CI. Para validar somente Core, Hardware e SmokeTests sem acesso à rede, com SDK, referências e runtime .NET 8 instalados:

```bash
bash Linux/validate.sh --offline-net8
```

Esse modo compila os projetos originais com uma configuração NuGet temporária sem fontes e desabilita downloads automáticos de targeting packs. Não muda seus alvos nem substitui a validação Avalonia. Os artefatos ficam nos diretórios `bin`/`obj` habituais; a configuração temporária é removida ao terminar. Se referências obrigatórias estiverem ausentes, a compilação falha.

O modo anterior `bash Linux/validate.sh --offline-net10` continua disponível para SDK/runtime .NET 10 e Python 3. Ele junta os fontes em um assembly temporário e não valida as referências originais nem o alvo .NET 8.

A suíte exercita renderização curta com cancelamento e simulação; não executa automaticamente render completo, banda RAM de 2 GiB, SMART real ou aquecimento para testar limites térmicos.

Em ambientes com diretórios de usuário protegidos, os caches podem ser direcionados a uma pasta temporária via `DOTNET_CLI_HOME`, `NUGET_PACKAGES` e `XDG_RUNTIME_DIR`, sem modificar as configurações do usuário.

Roteiros aprovados: [validação gráfica](../docs/VALIDACAO_GRAFICA.md) e [validação de hardware](../docs/VALIDACAO_HARDWARE.md). São cópias versionadas das checklists mantidas no diretório de trabalho. O acesso à sessão gráfica e ao netlink segue restrito no ambiente do agente; as confirmações reais foram realizadas pelo usuário.

## Executar

Com o SDK .NET 8 instalado e acesso ao NuGet, a partir da raiz do fork:

```bash
dotnet restore Linux/ResetExplode.csproj
dotnet build Linux/ResetExplode.csproj -c Release --no-restore
dotnet run --project Linux/ResetExplode.csproj -c Release --no-build
```

A aplicação precisa de sessão gráfica e das bibliotecas nativas exigidas pelo Avalonia. A base usa Avalonia 11.3.22; conferir o ambiente X11/XWayland conforme a distribuição. A compatibilidade gráfica ainda precisa de execução real neste porte. Referências: [pacote Avalonia](https://www.nuget.org/packages/Avalonia/11.3.22) e [documentação Linux](https://docs.avaloniaui.net/docs/platform-specific-guides/linux).

## Implementado nesta etapa

- Inicialização Avalonia e janela com o nome original do produto.
- Tema escuro com paleta, cartões de 16 px, botões de 10 px e tipografia baseados em `../Windows/Theme.xaml`; uso do logo original como recurso.
- Renderização de CPU em uma ou todas as threads, com visualização progressiva em `WriteableBitmap`.
- Simulação de jogo com as 30 mil entidades originais, aquecimento de 2 s e medição de 10 s.
- Testes separados de latência e banda de RAM. O teste de banda aloca cerca de 2 GiB; latência usa cerca de 272 MiB. Não há detecção de memória livre nesta fase.
- Execução fora da thread da interface, um teste por vez; cancelamento descarta resultados parciais. Fechar a janela durante um teste solicita cancelamento e aguarda sua conclusão.
- Persistência das configurações térmicas e das últimas 200 execuções, com métricas estruturadas, contexto e estados de conclusão separados.
- Página Sistema e sensores, com identificação de CPU, distribuição, kernel, placa-mãe/BIOS e RAM, atualização sequencial em segundo plano e busca de sensores.
- Carga da CPU calculada por diferenças de `/proc/stat`; memória usada calculada a partir de `MemTotal` e `MemAvailable`, sem tratar cache recuperável como RAM indisponível.
- Temperatura, ventoinhas, tensão, corrente, potência e frequência via `/sys/class/hwmon`; frequência reportada por política de CPU via `cpufreq`.
- Backend NVIDIA opcional e somente de leitura: temperatura, clocks, consumo, limite de potência, uso da GPU e VRAM via NVML. Cada métrica não suportada fica indisponível com seu erro; ausência de NVML não impede o restante da coleta.
- Interrupção dos benchmarks por temperatura da CPU, com limite configurável, verificação de leitura recente e motivo registrado sem pontuação parcial.

O path tracer mantém a cena de 960 × 540, 1024 amostras/pixel e a pontuação original. A duração é determinada pela conclusão da imagem, não por um temporizador. O cancelamento é cooperativo e pode aguardar a unidade de trabalho atual.

## Organização e reaproveitamento

| Arquivo/projeto | Responsabilidade |
| --- | --- |
| `ResetExplode.csproj`, `Program.cs`, `App.axaml*` | Aplicação Avalonia, sem WPF, WMI, DPAPI ou Direct3D. |
| `Styles/Theme.axaml`, `Views/MainWindow.axaml*` | Tema inicial, controles, progresso, renderização e ciclo de vida da janela. |
| `Core/ResetExplode.Core.csproj` | Compila diretamente `../../Shared/Benchmarks/BenchRenderer.cs` e `../../Shared/Benchmarks/GameSimBench.cs` da base Windows, sem cópias. |
| `Core/Benchmarks/MemoryBench.cs` | Extrai somente os algoritmos de RAM do arquivo Windows; exclui carregamento de disco e reforça cancelamento. |
| `Core/Benchmarks/BenchmarkRunner.cs` | Impede concorrência, executa trabalho em segundo plano e descarta retorno parcial após cancelamento. |
| `Core/Benchmarks/CpuThermalPolicy.cs` | Pré-checagem e monitoramento térmico independente da UI, com relógio monotônico e interrupção por limite ou perda de leitura. |
| `SmokeTests/` | Verificações de execução e cancelamento, sem interface e sem pacotes externos. |
| `Hardware/` | Modelos independentes de UI, acesso somente de leitura a arquivos Linux, coletores de sistema/sensores e NVML opcional. |
| `Core/Persistence/PersistenceModels.cs`, `Core/Persistence/LocalPersistence.cs` | Modelos versionados, caminhos XDG, gravação com substituição atômica, recuperação e retenção do histórico. |
| `Views/MainWindow.Persistence.cs` | Restauração das preferências, fila de gravações e apresentação do histórico e das pendências. |
| `Views/MainWindow.Telemetry.cs` | Navegação, atualização das leituras na UI, filtro e encerramento da coleta. |

O `.csproj` Windows na raiz exclui `Linux/**` dos itens automáticos para não misturar os dois pontos de entrada ou classes de UI. Seus algoritmos e telas permanecem como base do porte. Os demais controles do tema serão migrados conforme suas páginas; esta etapa não converte todo o tema WPF.

## Verificações

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release
```

O executável retorna erro se alguma verificação falhar. Exercita cancelamento prévio (inclusive RAM sem alocar buffers), cancelamento de render real com uma thread, rejeição de testes simultâneos, descarte de resultado parcial e uma simulação curta completa. Não roda uma renderização completa nem o teste de banda de 2 GiB automaticamente.

Os testes de telemetria criam uma árvore temporária de arquivos equivalente a `/proc` e `/sys`, sem alterar o sistema. Verificam ausência de dados, cultura pt-BR, unidades, carga da CPU sem duplicar contadores de guest, RAM disponível, campos DMI ausentes, falha/desativação de sensores, remoção de dispositivo e cancelamento. Para executar apenas esses testes:

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release -- --telemetry-only
```

Para executar os testes de telemetria e depois uma coleta real no terminal, incluindo NVML se disponível:

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release -- --live-telemetry
```

O modo real é um diagnóstico; ausência de sensores não é uma falha de teste. Os testes com arquivos simulados não validam o interop NVIDIA.

Os testes térmicos usam leituras simuladas, sem aquecer o hardware: verificam igualdade com o limite, dados inválidos, amostras antigas, bloqueio antes de iniciar, interrupção durante a execução, descarte de resultado parcial, distinção de cancelamento manual e encerramento do monitor. Fazem parte do comando padrão; para executá-los separadamente:

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release -- --thermal-only
```

O workflow `.github/workflows/linux.yml` compila a UI e executa essas verificações no Ubuntu. Ele não publica releases e não substitui a validação visual.

**Estado de validação nesta entrega:** XML e referências locais revisados estaticamente. Build, smoke tests e execução gráfica pendentes: o ambiente de desenvolvimento não possui `dotnet` e não resolveu o host do instalador. O workflow foi escrito, mas não foi executado nesta sessão.

Ao executar em um ambiente preparado, verificar também: abertura e carregamento do logo/tema; navegação entre sistema e benchmarks; atualização e busca de sensores; progresso do path tracer; cancelamento e início de outro teste; fechamento durante coleta/teste; latência/banda com RAM disponível. Validar a lógica de interrupção com os testes simulados, sem tentar superaquecer o computador.

## Interrupção por temperatura da CPU

O monitoramento começa habilitado, com limite de 90 °C, ajustável de 60 a 100 °C. Esse é um valor inicial configurável, não uma detecção do limite térmico do fabricante. A configuração vale para os benchmarks de renderização, simulação e RAM e fica bloqueada durante a execução. É restaurada na abertura e salva pelo botão SALVAR CONFIGURAÇÃO ou antes de iniciar um teste.

Com a opção habilitada, o benchmark não inicia sem uma leitura válida, com idade inferior a cinco segundos e abaixo do limite. Durante o teste, atingir o limite, perder o sensor ou completar cinco segundos sem atualização solicita cancelamento. A idade é calculada com relógio monotônico a partir do início da coleta, antes das consultas de GPU; atraso na NVML ou na UI não renova artificialmente uma leitura antiga.

Uma thread dedicada verifica o último snapshot aproximadamente a cada 250 ms, sem consultar hardware nem depender do pool usado pelos benchmarks. A leitura é publicada antes de enviar a atualização ao dispatcher. Não há garantia de interrupção instantânea: sensores, escalonamento do sistema e pontos cooperativos de cancelamento dos algoritmos determinam a latência.

É possível desativar explicitamente o monitoramento, por exemplo quando a CPU não tem sensor reconhecido. O painel e os resultados informam esse modo. Com monitoramento habilitado, o registro diferencia teste não iniciado, interrupção térmica, cancelamento manual e fechamento da janela; nenhuma medição incompleta recebe pontuação. O recurso cobre a CPU reconhecida pelo coletor, não a temperatura dos módulos de RAM ou uma proteção geral de hardware.

## Configurações e histórico local

Os arquivos seguem os caminhos [XDG](https://specifications.freedesktop.org/basedir/latest/). Variáveis vazias ou relativas usam os padrões abaixo:

| Conteúdo | Caminho padrão | Variável de base |
| --- | --- | --- |
| Opção e limite térmico | `~/.config/ResetExplode/settings.json` | `XDG_CONFIG_HOME` |
| Últimas 200 execuções | `~/.local/state/ResetExplode/benchmarks.json` | `XDG_STATE_HOME` |

As preferências são salvas pelo botão SALVAR CONFIGURAÇÃO ou antes de iniciar um teste. Alterar um controle e fechar sem salvar não persiste essa alteração. O histórico é gravado ao terminar cada execução e restaurado na abertura. O fechamento aguarda gravações em andamento.

Cada registro inclui identificador, data, tipo de teste, estado, resumo, modo térmico e contexto de CPU, sistema, arquitetura, runtime e parâmetros disponíveis. Resultados concluídos incluem métricas numéricas e pontuação quando aplicável; testes bloqueados, interrompidos, cancelados ou com erro não recebem pontuação nem métricas parciais. O contexto não significa que a comparação com Windows já foi validada.

A gravação usa arquivo temporário no mesmo diretório, flush e substituição do destino. Um arquivo `.lock` coordena as gravações entre instâncias; o histórico é relido antes da inclusão e registros repetidos são deduplicados pelo identificador. Diretórios novos usam permissões 0700 e arquivos novos 0600 no Linux, sem alterar permissões dos diretórios existentes.

JSON inválido é preservado em uma cópia `.invalid-*` antes de uma nova gravação. Versões desconhecidas e arquivos acima de 2 MiB são preservados sem sobrescrita. Falhas de gravação aparecem na interface: resultados pendentes ficam marcados como NÃO SALVO e podem ser reenviados por SALVAR PENDÊNCIAS / ATUALIZAR. Se a falha persistir até fechar, esses resultados permanecem apenas na memória e serão perdidos.

Esta etapa armazena somente configurações térmicas e histórico. Não importa automaticamente configurações Windows nem armazena chaves de API; a migração de credenciais protegidas por DPAPI permanece pendente.

Os testes de persistência usam diretórios temporários, sem acessar a configuração real do usuário. Cobrem caminhos XDG, reabertura, números em cultura pt-BR, recuperação, versões desconhecidas, bloqueio de escrita, deduplicação, retenção e rejeição de resultados parciais. Integram o comando padrão e podem ser executados isoladamente:

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release -- --storage-only
```

Esses testes passaram nos projetos originais em .NET 8; validação gráfica permanece pendente.

## Limitações da telemetria

- Dados ausentes, inacessíveis, inválidos ou sensores com falha/desativados são apresentados como indisponíveis; zero continua sendo um valor válido, por exemplo para uma ventoinha parada.
- O resumo térmico usa a maior temperatura válida dos chips de CPU reconhecidos (`coretemp`, `k10temp`, `k8temp`, `zenpower`). Outros chips permanecem listados sem adivinhar sua associação à CPU. Sistemas ARM e drivers adicionais podem exigir novos mapeamentos.
- Tensões hwmon não aplicam calibrações de `/etc/sensors.conf`; canais de termistor que exigem conversão específica não são convertidos em temperatura. Não inferimos valores de linhas de alimentação da placa-mãe.
- A RAM exibida descreve o sistema via procfs, não um limite de memória de container. DMI pode não existir em determinadas plataformas; módulos individuais de RAM ainda não são inventariados nesta fase. O inventário de blocos está descrito abaixo.
- `cpufreq` reporta a frequência exposta pelo driver por política; não é uma medição independente do clock efetivo.
- NVML carrega `libnvidia-ml.so.1` ou `libnvidia-ml.so`, inicializa no primeiro uso e libera a biblioteca após encerrar a coleta. Alterações de instalação/driver durante a sessão podem exigir reabrir a aplicação. O arquivo `Nvml.cs` original permanece intacto; o backend Linux implementa apenas consultas e ciclo de vida próprios, sem operações de overclock.
- A coleta roda em um único loop, com intervalo de um segundo após cada atualização; o intervalo real inclui o tempo de resposta dos drivers. Telemetria permanece ativa durante os benchmarks de CPU e pode influenciar a medição. Comparabilidade entre plataformas ainda não foi validada.

Referências dos formatos e APIs: [hwmon](https://docs.kernel.org/hwmon/sysfs-interface.html), [procfs](https://www.kernel.org/doc/html/v6.14/filesystems/proc.html) e [consultas NVML](https://docs.nvidia.com/deploy/nvml-api/latest/api/group__nvmlDeviceQueries.html).

## Exportar histórico

Na página de benchmarks, use EXPORTAR HTML ou EXPORTAR JSON e escolha o destino no diálogo de salvar. O HTML é um arquivo independente, sem scripts ou recursos externos; o JSON mantém métricas numéricas, contexto, identificadores e estados das execuções. Ambos incluem até 200 registros disponíveis na sessão, com data da exportação e versão da aplicação. Registros pendentes são identificados como não salvos localmente; exportá-los não elimina a pendência do histórico.

A exportação captura os dados antes de abrir o diálogo. Ela bloqueia novos benchmarks até terminar, aguarda gravações locais em andamento e é aguardada no fechamento. Cancelar o diálogo não grava conteúdo. Falhas são exibidas na interface; uma falha durante a escrita pode deixar o destino incompleto. O relatório exportado usa um formato próprio e não é um backup importável de `benchmarks.json`.

Esta etapa adapta a função HTML/JSON de `../Windows/ReportBuilder.cs` aos modelos Linux sem modificar o arquivo Windows. O relatório cobre o histórico de benchmarks migrados; inventário completo, sensores e saúde dos discos do relatório Windows continuam pendentes. O contexto exportado inclui CPU, sistema, arquitetura e runtime. A seleção de destino usa o [StorageProvider do Avalonia 11](https://v11.docs.avaloniaui.net/docs/concepts/services/storage-provider/).

Testes de snapshot, escaping HTML, números em pt-BR, estados, deduplicação, limite e histórico vazio integram o conjunto padrão. Para executá-los isoladamente:

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release -- --report-only
```

Os testes de geração passaram em .NET 8; diálogos de salvar permanecem pendentes.

## Inventário de armazenamento

A página Sistema e sensores contém um cartão de armazenamento, carregado ao abrir a janela e atualizado pelo botão ATUALIZAR DISPOSITIVOS. `Hardware/Storage/LinuxStorageCollector.cs` lê `/sys/block` e suas partições em segundo plano, separado do loop térmico. Atualizações não se sobrepõem; o fechamento cancela e aguarda a coleta em andamento.

São apresentados nome do dispositivo, relação entre partição e dispositivo pai, modelo quando disponível, capacidade e flags de rotação, removibilidade e somente leitura. O inventário inclui dispositivos virtuais expostos pelo kernel (como loop e device-mapper); não os classifica automaticamente como discos físicos. A flag de rotação não é usada para afirmar que um dispositivo é SSD. Dados ausentes ou inválidos aparecem como indisponíveis; capacidade zero é preservada.

A capacidade usa `size × 512`, independentemente do tamanho de bloco lógico. Referência: [ABI de dispositivos de bloco do kernel Linux](https://github.com/torvalds/linux/blob/master/Documentation/ABI/stable/sysfs-block). Capacidades de discos, partições e dispositivos empilhados podem se sobrepor e não são somadas. Não representam espaço livre de sistemas de arquivos.

Esta é a primeira parte do porte de armazenamento, tomando `../Windows/DriveMap.cs` como referência de funcionalidade e preservando sua implementação Windows. O inventário por si só não faz inferência de saúde, benchmark, acesso aos conteúdos de `/dev` ou coleta de números de série. Há uma exportação própria para armazenamento, descrita abaixo.

Os testes usam uma árvore sysfs temporária e cobrem partições, unidades, flags, capacidade zero, ausência, dados inválidos, overflow, remoção e cancelamento. Fazem parte do comando padrão e também podem ser executados isoladamente:

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release -- --inventory-only
```

Compilação e testes C# passaram em .NET 8; apresentação gráfica permanece pendente.

## Montagens e espaço disponível

O cartão de armazenamento também lê `/proc/self/mountinfo`, mostrando ponto de montagem, origem, tipo, raiz, número major:minor e modo de leitura. A coleta considera o namespace do processo, que pode diferir do host em containers. Campos opcionais e escapes de espaço, tabulação, quebra de linha e barra invertida são tratados pelo parser.

Para formatos locais reconhecidos, `DriveInfo` consulta capacidade, espaço livre total e disponível ao usuário. Usado é calculado como total menos livre total; cotas e reservas podem tornar o disponível menor. Leituras inconsistentes, falhas de acesso e dados ausentes são apresentados sem inventar valores. Pontos de montagem sobrepostos são listados sem consulta de espaço; bind mounts e subvolumes podem compartilhar capacidade e não são somados. Não se infere associação física apenas pelo nome da origem.

Rede, FUSE, autofs e formatos fora da lista reconhecida são listados sem consulta automática de espaço. Consultas locais rodam fora da UI e do monitor térmico; o cancelamento é verificado entre consultas, mas não interrompe uma chamada nativa já em andamento. A montagem pode mudar durante a coleta e os contadores são lidos em chamadas distintas: o inventário é uma observação, não uma fotografia atômica nem uma garantia de espaço gravável. O modo somente leitura é mostrado separadamente da capacidade.

A atualização usa o mesmo botão do inventário. Os testes simulam a tabela de montagens e o provedor de espaço, sem depender de discos reais:

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release -- --mounts-only
```

Também fazem parte de `--inventory-only` e do conjunto padrão. Referências: [formato mountinfo do kernel](https://www.kernel.org/doc/html/v5.8/filesystems/proc.html) e [espaço disponível em DriveInfo](https://learn.microsoft.com/en-us/dotnet/api/system.io.driveinfo.availablefreespace?view=net-8.0). Testes e compilação dos projetos sem UI passaram em .NET 8; execução gráfica permanece pendente.

## Consulta SMART manual

No cartão de armazenamento, selecione um dispositivo inteiro e use CONSULTAR SMART. Antes de consultar, um aviso explica a permissão administrativa para o dispositivo selecionado, com Cancelar e Prosseguir. Cancelar ou fechar o aviso não executa a consulta. Prosseguir solicita autorização via polkit; a senha é tratada pelo sistema. Apenas a consulta é elevada, mantendo a interface no usuário normal. Executa `pkexec --disable-internal-agent /usr/bin/timeout --kill-after=2s 15s <smartctl> --json=c -a /dev/<nome>` sem shell e com argumentos separados. Exige smartmontools em um diretório padrão do sistema, `/usr/bin/pkexec`, `/usr/bin/timeout` e agente gráfico polkit ativo. Não inicia autotestes nem altera configurações; pode ativar um disco em repouso. Dispositivos virtuais ou adaptadores sem suporte podem retornar dados indisponíveis.

A leitura mostra temperatura, horas ligadas, ciclos e métricas NVMe quando presentes. Distingue aprovação da avaliação SMART, alerta reportado, registros/contadores de atenção, leitura incompleta e ausência de avaliação. Bits de falha de acesso/comando não são tratados como aprovação; bits de histórico de erros são exibidos separadamente de falha atual. A aprovação não garante ausência de defeitos. Não aplica os limiares genéricos de temperatura/desgaste do Windows a dispositivos Linux.

O processo tem prazo de 15 segundos, drena stdout/stderr simultaneamente e limita cada saída a 1 MiB de caracteres retidos. No prazo ou cancelamento, solicita encerramento da árvore do processo e aguarda a saída do processo principal antes de retornar. O fechamento cancela a consulta e aguarda sua tarefa; uma operação de kernel não interrompível pode atrasar o encerramento do processo. O JSON bruto, mensagens brutas e números de série não são exibidos nem persistidos. O resultado identifica o dispositivo consultado e o horário; atualize/consulte novamente após mudanças de hardware.

A integração está em `Hardware/Storage/LinuxSmartReader.cs` e `Views/MainWindow.Smart.cs`; a base `../Windows/DiskHealth.cs` foi preservada. Atributos ATA específicos de fabricante, seleção de protocolos para bridges/RAID e benchmarks de disco continuam pendentes. Referência dos bits: [código oficial do smartmontools](https://github.com/smartmontools/smartmontools/blob/main/src/smartctl.h).

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release -- --smart-only
```

Os testes incluem JSON simulado e execução de um programa temporário, sem consultar discos; também integram o conjunto padrão. No Linux, exigem `/bin/sh`, `head` e `sleep`. Verificam argumentos separados, caminho do executável com espaço, códigos de saída, JSON inválido, executável ausente, omissão de stderr, drenagem simultânea das pipes, limite de saída, timeout e cancelamento em andamento. O PID do processo simulado deve estar encerrado quando a leitura retorna; encerramento de descendentes e I/O de kernel não interrompível não são cobertos. Nesta etapa foi confirmada a presença de smartctl 7.5 por `smartctl --version`, e os testes simulados passaram também em .NET 8. Timeout/cancelamento passaram com processo simulado real. Consulta com smartctl/disco real e interação pela UI ainda precisam ser validadas.

### Diagnóstico com smartctl instalado

Após compilar os testes, execute explicitamente uma consulta ao dispositivo escolhido:

```bash
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --live-smart nvme0n1
```

Informe somente o nome, sem `/dev/`; o comando aceita exatamente uma opção e um dispositivo. Usa o mesmo parser da interface, com prazo de 15 segundos, sem elevação, autotestes ou alteração de parâmetros. A opção de terminal mantém a consulta sem privilégios; somente o fluxo confirmado da interface usa polkit. Pode acordar um disco em repouso. Ctrl+C solicita cancelamento. Mostra o estado, métricas e avisos tratados; não imprime JSON bruto nem stderr do smartctl. Esse modo não faz parte da suíte automática.

Código de saída 0 significa que um relatório foi produzido, inclusive quando informa indisponibilidade; não significa disco saudável. Argumentos inválidos retornam 2 e cancelamento por Ctrl+C retorna 130. A falha de abertura é descrita pelo relatório sem presumir se a causa foi permissão, dispositivo ausente ou suporte.

Validado nesta continuação com smartctl 7.5 e .NET 8: `nvme0n1` consta em `/sys/block`, mas `/dev/nvme0n1` não está disponível no ambiente. A execução real retornou LEITURA INCOMPLETA, sem aprovação ou métricas inventadas. Acesso ao disco e leitura efetiva de saúde permanecem pendentes. Uso sem dispositivo, com opções adicionais e com `..` foi rejeitado com saída 2. A suíte completa passou, incluindo rejeição de nomes `.` e `..`.

## Exportar armazenamento

O cartão de armazenamento oferece EXPORTAR ARMAZENAMENTO HTML e JSON. O arquivo inclui dispositivos/partições, montagens, capacidade e espaço, avisos e a última consulta SMART disponível. Esse relatório é separado do histórico de benchmarks; não dispara coleta ou consulta SMART durante a exportação.

O snapshot usa cópias das coleções e identifica as datas do inventário e do SMART separadamente, além da data de exportação. Uma atualização em andamento pode ainda não estar incluída; aguarde sua conclusão para exportar os novos dados. Uma falha de atualização limpa o inventário exportável, e iniciar outra consulta SMART limpa o resultado SMART anterior. Sem coleta disponível, o relatório informa a ausência. A última consulta SMART identifica seu dispositivo e pode ser anterior ao inventário; não é automaticamente associada a um dispositivo reconectado com o mesmo nome.

HTML é independente, sem scripts ou recursos externos, com texto escapado. JSON mantém capacidades e contadores em bytes numéricos; métricas SMART preservam o texto apresentado na UI. Os dados incluem caminhos de montagem, origens e modelos. Não há campos de número de série nem saída bruta do smartctl no modelo exportado; caminhos e origens podem conter identificadores fornecidos pelo próprio sistema. Falha de escrita pode deixar o destino incompleto e é informada na interface.

Implementação: `Hardware/Reports/StorageReportBuilder.cs` e integração ao fluxo existente `Views/MainWindow.Export.cs`. Testes de cópia independente, escaping, datas, ausência e precisão numérica integram `--report-only` e o conjunto padrão. Testes passaram em .NET 8; build Avalonia e diálogo gráfico permanecem pendentes.

## Bateria e alimentação

A página de sistema contém um cartão BATERIA E ALIMENTAÇÃO, coletado ao abrir a janela, automaticamente cerca de cinco segundos após cada coleta automática terminar e pelo botão ATUALIZAR ALIMENTAÇÃO. `Hardware/Power/LinuxPowerCollector.cs` lê `/sys/class/power_supply` somente para consulta. `Views/MainWindow.Power.cs` executa a coleta fora da UI e do loop térmico, sem sobrepor atualizações; o fechamento solicita cancelamento e aguarda a tarefa.

Cada fonte/bateria é apresentada separadamente, com tipo e escopo quando disponíveis. Para baterias: presença, estado, percentual reportado, modelo, fabricante, tecnologia, ciclos, energia (Wh), carga elétrica (Ah), tensão, corrente, potência e temperatura. Para outras fontes: online, tensão e corrente reportadas. `online` descreve aquela fonte; não é usado para afirmar como todo o computador está sendo alimentado. Baterias de periféricos são identificadas pelo escopo do kernel, quando presente.

As unidades micro são convertidas para Wh/Ah/V/A/W; temperatura usa décimos de °C. Não converte Ah para Wh, não estima autonomia/desgaste e não atribui um diagnóstico de saúde. Zero válido é preservado, inclusive carga/ciclos; percentual fora de 0–100 e dados inválidos/ausentes ficam indisponíveis. Bateria explicitamente ausente não mostra métricas remanescentes. Uma máquina sem entradas em power_supply não é classificada como defeituosa.

A base `../Windows/BatteryInfo.cs` foi revisada e preservada; regras WMI de desgaste e planos de energia não foram copiadas. Não altera limites de carga nem políticas de energia. Os dados de alimentação podem ser exportados conforme descrito abaixo. Referência: [classe power_supply do kernel](https://www.kernel.org/doc/html/latest/power/power_supply_class.html).

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release -- --power-only
```

O loop automático e o botão manual compartilham a tarefa de leitura em andamento, evitando consultas sobrepostas. Uma falha limpa a apresentação e permite nova tentativa no ciclo seguinte. Ao fechar, o atraso é cancelado e a aplicação aguarda o loop e a coleta ativa antes de descartar seus recursos. O intervalo real inclui o tempo da leitura e do dispatcher; não é uma garantia de atualização a cada cinco segundos. A leitura continua enquanto a janela estiver aberta, inclusive em outras páginas ou durante benchmarks.

Os testes usam sysfs temporário, incluem cultura pt-BR, escalas, sinais, zero, ausência, escopo, dados inválidos, mudança de carga/estado, snapshots independentes, remoção, reaparecimento e cancelamento. Integram o conjunto padrão. Os testes passaram em .NET 8; build Avalonia e UI permanecem pendentes.

## Exportar bateria e alimentação

Use EXPORTAR ALIMENTAÇÃO HTML ou JSON no cartão correspondente. A exportação captura a última coleta concluída, sem disparar nova leitura, e copia também as listas internas de métricas. Atualizações automáticas posteriores não alteram o arquivo em geração. Durante uma atualização em andamento, o relatório usa a coleta anterior, com sua data original.

O relatório separa data de coleta e data de exportação. Distingue ausência de coleta, coleta sem fontes/baterias e falha de leitura. Uma falha limpa o snapshot exportável; uma coleta posterior bem-sucedida limpa a mensagem de erro. Inclui nomes, tipos, escopos, modelos, valores e avisos já disponíveis na interface, sem números de série coletados por este módulo.

HTML é independente e escapa os textos recebidos. JSON tem `reportType: linux-power` e versão de esquema 1; os valores das métricas são textos com unidades e formatação da coleta, não uma série temporal numérica. Não calcula autonomia, desgaste ou diagnóstico. A exportação compartilha o diálogo, o bloqueio de exportações simultâneas e a espera no fechamento usados pelos outros relatórios. Falhas de escrita podem deixar o destino incompleto e são exibidas na interface.

Implementação em `Hardware/Reports/PowerReportBuilder.cs`, integrada a `Views/MainWindow.Export.cs`. Testes de cópia profunda, escaping, unidades/zero, datas, coleta vazia/ausente e erro estão incluídos em `--report-only` e no conjunto padrão. Os testes passaram em .NET 8; compilação Avalonia e diálogo gráfico permanecem pendentes.

## Maker: inventário de portas seriais

O cartão MAKER · PORTAS SERIAIS na página Sistema e sensores lista `ttyUSB` e `ttyACM`, com coleta inicial e botão ATUALIZAR PORTAS. Coletas não se sobrepõem e rodam fora da UI; o fechamento cancela e aguarda a tarefa. Atualize após conectar ou remover dispositivos.

O coletor cruza nomes observados em `/sys/class/tty` e `/dev`, sem abrir as portas. Mostra separadamente a presença nas duas listagens e o número major:minor quando válido. Nomes com prefixo serial sem sufixo numérico são descartados. Ausência na listagem pode significar indisponibilidade do diretório; não é diagnóstico de defeito.

A associação ativa a `dialout` e `uucp` usa o GID efetivo e os grupos suplementares em `/proc/self/status`, conforme a [documentação do kernel](https://docs.kernel.org/filesystems/proc.html). Os nomes são resolvidos somente pelo arquivo local `/etc/group`; não consulta NSS/LDAP. Uma entrada de usuário nesse arquivo não implica que o processo atual já recebeu o grupo. Dados ausentes, ambíguos ou inválidos são apresentados como indisponíveis.

A associação a grupos não comprova permissão efetiva: ACLs, regras udev, isolamento, porta ocupada e comunicação não são testados. Não altera grupos, permissões, drivers ou ModemManager. A referência Windows `UsbCleaner.cs` foi preservada. Ferramentas de configuração ficam para etapas posteriores. A exportação Maker está descrita abaixo. A identificação USB e os aliases by-id estão descritos abaixo.

Após compilar:

```bash
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --serial-only
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --live-serial
```

Os testes simulados integram a suíte padrão e cobrem fontes distintas, deduplicação, filtros de nomes, grupos efetivo/suplementares, dados ausentes/inválidos, remoção, snapshots independentes e cancelamento. Build e suíte completa passaram em .NET 8. O diagnóstico real não observou portas neste ambiente; encontrou `uucp` GID 984 sem associação ativa, e `dialout` indisponível. Não valida hardware serial conectado. O cartão passou por verificações estáticas de XML/referências; build e ciclo gráfico Avalonia permanecem pendentes.

### Identificação USB e driver das portas

O cartão Maker e `--live-serial` mostram fabricante e produto reportados pelo dispositivo USB pai, VID/PID hexadecimais e o driver associado à porta/interface quando disponíveis. A coleta resolve o link de classe para `/sys/devices` e percorre os pais, com limite de 32 níveis e cancelamento entre leituras. Segue as [regras de acesso ao sysfs do kernel](https://www.kernel.org/doc/html/latest/admin-guide/sysfs-rules.html); o modelo mantém separados o caminho do dispositivo USB e o nó de origem do driver.

A busca termina no primeiro dispositivo com `DEVTYPE=usb_device` ou atributos VID/PID, inclusive quando incompletos. Campos ausentes desse dispositivo não são preenchidos com os de um hub acima dele. O driver é obtido do link mais próximo encontrado antes do dispositivo USB, sem usar o driver genérico desse dispositivo como se fosse o da porta. Não há tabela que infira modelo ou fabricante apenas pelo VID, nem leitura do atributo `serial`. Os textos são declarações do dispositivo, não comprovação de autenticidade ou funcionamento.

Links inacessíveis/quebrados, destinos fora da raiz de leitura e valores inválidos resultam em identificação indisponível. A coleta não é atômica: remoção/reconexão durante a leitura pode produzir dados parciais; atualize novamente após mudanças. A identificação foi testada com hierarquias temporárias USB serial e CDC ACM, drivers distintos, hubs, atributos ausentes/inválidos, links relativos/circulares/quebrados e remoção. Suíte completa .NET 8 aprovada sem avisos/erros; hardware conectado e execução gráfica ainda pendentes.

### Caminhos persistentes by-id

O cartão Maker e `--live-serial` apresentam os aliases encontrados em `/dev/serial/by-id` para cada porta. A coleta resolve links relativos, absolutos e encadeados e associa somente destinos existentes que correspondam exatamente a `/dev/ttyUSB<n>` ou `/dev/ttyACM<n>` já observados na listagem de `/dev`. Vários aliases podem identificar a mesma porta; são apresentados em ordem estável e preservados em uma coleção somente de leitura no snapshot.

Links quebrados, circulares, inacessíveis, arquivos que não sejam links e destinos fora das portas observadas não são associados; as entradas enumeradas sem associação geram um aviso com a contagem. Um diretório by-id ausente/inacessível resulta em nenhuma associação, sem inferir defeito. A coleta não abre dispositivos nem cria ou altera links. Mudanças posteriores exigem atualização do inventário; o snapshot não garante que um caminho ainda aponte para o mesmo dispositivo.

Os nomes dos aliases são mostrados como fornecidos pelo sistema e podem conter identificadores ou números de série, embora o coletor não leia o atributo USB `serial`. A exportação manual Maker inclui esses dados; não há persistência automática do inventário.

Testes integrados a `--serial-only` e à suíte padrão validam múltiplos aliases, ordenação, nomes com espaços, links relativos/absolutos/encadeados, links inválidos, destinos fora da raiz de teste, alteração de destino, remoção de dispositivo/diretório e preservação do snapshot anterior. Suíte completa aprovada em .NET 8, sem avisos/erros. XML/binding verificados estaticamente; coleta real executada sem portas observadas. Hardware conectado e execução gráfica permanecem pendentes.

## Exportar inventário Maker

Use EXPORTAR MAKER HTML ou JSON no cartão de portas seriais. O relatório captura a última coleta concluída, incluindo portas, presença em sysfs e `/dev`, identificação USB e origem do driver, aliases by-id, grupos ativos e avisos. Não executa nova coleta nem abre portas. Uma atualização em andamento ainda pode não aparecer no snapshot; os horários de coleta e exportação são separados.

O snapshot copia as coleções, inclusive aliases. Falha de coleta limpa o inventário exportável e mantém o motivo; coleta posterior bem-sucedida limpa o erro. Relatórios distinguem ausência de coleta, inventário vazio e falha. O JSON usa `reportType: linux-serial` e esquema 1; mantém valores booleanos/nulos para associação a grupos e identificação ausente. HTML independente escapa os textos e não contém scripts ou recursos externos.

Aliases podem conter identificadores e números de série. Essa informação aparece junto aos botões e no HTML. O relatório descreve o inventário observado e não certifica acesso, autenticidade ou comunicação. Exportação usa o mesmo diálogo, bloqueio de exportações/benchmarks simultâneos e espera no fechamento dos demais relatórios. Erro de escrita é informado e pode deixar um arquivo incompleto.

Implementação em `Hardware/Reports/SerialReportBuilder.cs` e `Views/MainWindow.Export.cs`. Testes de cópia profunda, escaping, aliases, identidade, grupos, datas, ausência, inventário vazio e falha integram `--report-only` e a suíte completa; todos passaram em .NET 8. Diálogo e apresentação gráfica permanecem pendentes.

## Organização atual

O [mapa de pastas](../docs/ESTRUTURA.md) explica a separação entre `Windows/`, `Shared/`, `Linux/` e `docs/`. Na aplicação Linux, as telas estão em `Views/`, o tema em `Styles/` e os fontes de Core, Hardware e SmokeTests em subpastas por assunto. Os caminhos dos projetos e do comando `validate.sh` permanecem iguais. Guia e registro completos agora são versionados em `docs/`.

## Próximas etapas

Builds, suíte automatizada, fluxo térmico, GPU/NVML e cenários gráficos das entregas atuais aprovados. Distribuição portátil e instalação por usuário ficam por último, após os itens obrigatórios do porte, conforme orientação do usuário. Ainda pendentes: Maker com dispositivo conectado, metodologia dos benchmarks completos, build Windows, perfis/credenciais, disco sem cache, GPU/Vulkan, overlay, atalhos globais e demais ferramentas Windows. Rede tem inventário, ICMP, transferência HTTP limitada, resolução de nomes e exportação; bufferbloat/capacidade máxima seguem fora da implementação.

### Autorização da consulta SMART

A interface aguarda até dois minutos pelo fluxo de autorização/consulta; após autorização, timeout limita a consulta a 15 segundos, com tentativa de encerramento forçado após mais dois segundos. Ao fechar o app, o cancelamento solicita encerramento; processos já elevados podem depender desse limite para terminar. I/O não interrompível pelo kernel não tem garantia de encerramento imediato. Recusa/cancelamento da autorização e ausência do agente polkit são apresentados sem interpretar esses resultados como saúde do disco. O aviso do sistema pode identificar `timeout` como programa autorizado, pois ele limita o processo smartctl elevado.

Referência: [pkexec / polkit](https://www.freedesktop.org/software/polkit/docs/latest/pkexec.1.html).

## Instância única

Uma abertura adicional com o mesmo usuário e diretório de estado solicita ativação da janela existente e encerra antes de iniciar Avalonia ou coletores. A janela minimizada é restaurada; diálogos próprios em andamento recebem o pedido de foco. Durante o encerramento, não há reativação. O foco efetivo depende do gerenciador de janelas/compositor; a solicitação não garante que o desktop permita trazer a janela à frente.

`Core/Lifecycle/SingleInstance.cs` mantém um arquivo bloqueado em uma subpasta privada do estado XDG. Pedidos vazios de ativação, identificados por GUID, são verificados a cada 200 ms e confirmados ao serem encaminhados à UI. A segunda instância espera até três segundos pela confirmação; se não houver resposta, informa a limitação e não abre outra janela. Nenhum comando, argumento ou dado de hardware é transmitido. Arquivos abandonados de pedidos/confirmações expiram após um minuto. O arquivo de lock permanece, mas o bloqueio é liberado ao fechar o processo, inclusive após encerramento abrupto. Alterar o diretório XDG de estado isola a instância.

```bash
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --instance-only
```

Testes entre processos cobrem exclusão, notificações repetidas, identidades independentes, fechamento normal e recuperação após encerramento abrupto. Ativação, restauração, interação com aviso SMART/benchmark e reabertura aprovadas pelo usuário na sessão gráfica. Referência de UI: [Window.Activate](https://api-docs.avaloniaui.net/docs/M_Avalonia_Controls_WindowBase_Activate).

## Rede: inventário local

O cartão REDE · INTERFACES na página Sistema e sensores mostra cada interface observada em `/sys/class/net`, incluindo loopback e interfaces virtuais. Coleta inicial e botão ATUALIZAR REDE rodam fora da UI, sem sobreposição; o fechamento solicita cancelamento e aguarda a coleta.

Apresenta estado operacional, carrier, MTU, velocidade reportada do link, duplex e contadores de bytes/pacotes/erros/descartes RX/TX. Contadores preservam inteiros sem sinal de 64 bits e zeros válidos; dados ausentes/inválidos e sentinelas de velocidade ficam indisponíveis. A coleta é sequencial, não atômica, e a remoção de uma interface durante a leitura pode produzir campos indisponíveis. Cada atualização cria um snapshot independente.

O inventário não confirma acesso à internet nem mede throughput. Contadores são acumulados e podem reiniciar; não se somam interfaces virtuais e físicas como tráfego total do computador. Nenhuma consulta externa, configuração de rede, elevação ou teste ativo é executado por esta etapa. Teste ICMP, transferência HTTP de download/upload e exportação estão descritos abaixo. Endereços/gateways/DNS estão descritos abaixo. A base Windows `NetworkTest.cs` foi revisada e preservada.

```bash
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --network-only
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --live-network
```

Implementação: `Hardware/Network/LinuxNetworkCollector.cs` e `Views/MainWindow.Network.cs`. Testes simulados integram a suíte completa. Referências: [ABI de interfaces do kernel](https://github.com/torvalds/linux/blob/master/Documentation/ABI/testing/sysfs-class-net) e [contadores de rede](https://docs.kernel.org/networking/statistics.html).

### Endereços, gateways e DNS local

O mesmo botão ATUALIZAR REDE atualiza endereços IPv4/IPv6 unicast, prefixos e gateways reportados por `NetworkInterface.GetAllNetworkInterfaces()`/`GetIPProperties()`. A associação usa o nome exato da interface observada em sysfs; associação ausente ou ambígua fica indisponível. Escopo IPv6 (por exemplo `%7`) é preservado. Lista vazia (Nenhum informado) é distinta de falha da API (Indisponível). Não seleciona uma conexão principal nem promete representar todas as rotas/tabelas ou gateways de cada família suportados pelo sistema.

DNS aparece separado das interfaces: são as entradas nameserver explícitas de `/etc/resolv.conf`, com suporte a IPv4/IPv6, comentários, deduplicação preservando ordem e aviso para entradas inválidas. Arquivo vazio é distinto de arquivo inacessível. Não inventa servidores padrão e não consulta os servidores. Endereços locais podem representar um resolvedor intermediário; configuração específica de VPN, DNS por domínio e resolutores próprios de aplicativos não são inferidos.

Falha na enumeração de endereços preserva o inventário sysfs e DNS. Cada atualização cria novos dados; o fechamento continua cancelando e aguardando a tarefa. A coleta é sequencial e pode refletir mudanças de configuração durante a leitura. Testes de `NetworkConfigurationChecks` cobrem prefixos, IPv6 com escopo, associação de interfaces, falhas parciais, alterações e snapshots, parsing DNS e indisponibilidade; integram --network-only e a suíte completa.

Referências: [propriedades IP das interfaces (.NET)](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ipinterfaceproperties) e [resolv.conf](https://man7.org/linux/man-pages/man5/resolv.conf.5.html).


## Rede: latência, jitter e perda ICMP

No cartão de rede, informe um IPv4 ou IPv6 unicast e clique em TESTAR LATÊNCIA. Domínios e URLs não são aceitos nesta etapa; nenhum destino externo é escolhido automaticamente. IPv6 com escopo numérico é aceito. São 10 tentativas sequenciais com timeout de 2 segundos por tentativa e pausa de 500 ms após cada uma, exceto a última. A duração nominal máxima é aproximadamente 25 segundos, sujeita ao agendamento do sistema.

Latência mínima/média/máxima usa somente respostas `Success`, preservando zero válido. A API fornece RTT em milissegundos inteiros: zero pode representar menos de 1 ms. Jitter é a média das diferenças absolutas entre RTTs de tentativas consecutivas bem-sucedidas; uma tentativa sem resposta interrompe o par. Sem pares válidos, jitter fica indisponível. Perda ICMP é a porcentagem de tentativas sem resposta de sucesso, com contagem dos estados retornados. Bloqueio de ICMP ou ausência de resposta não prova falha da internet, e estes números não medem throughput nem jitter RTP.

Falhas locais de permissão/execução interrompem o teste e exibem erro sem publicar perda ou resultado parcial. Cancelar e fechar a janela solicitam cancelamento e aguardam a tarefa. Benchmarks e exportações ficam bloqueados durante o teste e vice-versa. Atualização do inventário permanece independente. Não há elevação, configuração de rede ou persistência automática. Exportação manual descrita abaixo.

```bash
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --network-only
# Opt-in: envia pacotes ao endereço informado; Ctrl+C cancela.
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --live-latency 127.0.0.1
```

Implementação: `Hardware/Network/NetworkLatencyTest.cs`; usa [Ping.SendPingAsync do .NET](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ping.sendpingasync?view=net-8.0). Testes simulados sem tráfego real integram a suíte padrão. Build Avalonia/SmokeTests e suíte completa aprovados em 25/10/2026; usuário confirmou “tudo validado” para o roteiro desta etapa, incluindo execução, cancelamento, bloqueio de ações concorrentes e fechamento. Valores individuais das medições não foram informados.

Diagnóstico real no ambiente do agente: `--live-latency 127.0.0.1` concluiu com 10 estados `TimedOut`, RTT/jitter indisponíveis. A causa não foi determinada; este resultado valida a conclusão sem respostas, mas não valida recepção ICMP real nem conectividade da máquina do usuário.


## Exportar relatório de rede

EXPORTAR REDE HTML/JSON captura o último inventário disponível e o resultado concluído da última tentativa de teste ICMP. Não inicia coleta nem envia pacotes. Nova tentativa limpa o resultado anterior; cancelamento ou erro mantém somente o estado, sem métricas parciais. Atualização do inventário em andamento pode não aparecer no relatório; falha de coleta limpa o inventário exportável e registra o motivo. Coleta posterior bem-sucedida limpa esse erro.

O relatório contém interfaces, métricas como exibidas na UI (textos com unidades/formatação local), IPs, gateways, DNS, avisos, destino e tentativas ICMP. JSON usa `reportType: linux-network`, esquema 3 (inclui transferência HTTP e resolução de nomes), métricas ICMP numéricas, estados das tentativas por nome e valores indisponíveis nulos. RTT de tentativa sem sucesso não é uma medição utilizável, mesmo que a API tenha retornado zero; consulte sempre seu estado. Horários de coleta, conclusão do teste e exportação são separados. HTML independente escapa todos os dados e não carrega recursos externos. As coleções são copiadas antes de gerar o conteúdo.

Os botões informam a presença de endereços de rede no arquivo. Exportação usa o diálogo existente, bloqueia benchmarks e testes ICMP simultâneos e aguarda conclusão no fechamento. Cancelar o diálogo preserva os dados da sessão. Erro de escrita pode deixar arquivo incompleto. Os relatórios incluem o último resultado concluído de transferência HTTP, quando disponível, sem histórico de testes.

Implementação em `Hardware/Reports/NetworkReportBuilder.cs`. Testes de snapshot, escaping, identidade, datas, métricas, perda total, ausência, inventário vazio e falha integram `--report-only` e a suíte completa. Builds Avalonia/SmokeTests sem avisos/erros e suíte completa aprovados em 25/10/2026. Usuário confirmou “tudo validado” para a exportação de rede em 25/10/2026: arquivos HTML/JSON, estados sem resultado, cancelamento do diálogo, substituição, bloqueios e fechamento aprovados conforme o roteiro versionado.


## Download / upload HTTP limitado

Em Sistema e sensores → Rede, TESTAR DOWNLOAD / UPLOAD acessa `https://speed.cloudflare.com`: GET `__down?bytes=10000000`, seguido de POST `__up` com 5.000.000 bytes sintéticos gerados em memória. Os endpoints são os [documentados pelo projeto Cloudflare Speedtest](https://github.com/cloudflare/speedtest/blob/main/README.md). A tela identifica o destino e o consumo antes do clique; Cloudflare recebe o IP da conexão, conforme seu [aviso de privacidade](https://speed.cloudflare.com/). Nenhuma transferência ocorre ao abrir a aplicação ou exportar um relatório.

Uma requisição por etapa, sem aquecimento, paralelismo ou repetição automática pela aplicação. Download lê até 10.000.000 bytes do corpo e exige esse total; upload exige escrita completa do corpo e resposta HTTP de sucesso. A resposta do upload é lida até 4 KiB. Cabeçalhos de resposta são limitados a 16 KiB pelo cliente. Redirecionamentos, cookies e descompressão automática ficam desativados; respostas comprimidas ou comprimentos declarados inesperados são rejeitados. Solicita ausência de cache. O payload de 15 MB não inclui overhead, retransmissões ou respostas; não representa limite exato de bytes na interface de rede.

Cada etapa tem limite de 20 segundos abrangendo envio, cabeçalhos e leitura do corpo; o tempo nominal total é até 40 segundos, sujeito ao agendamento/encerramento do sistema. Cancelamento cobre envio e leitura e o fechamento aguarda a tarefa. Erro HTTP, resposta incompleta, timeout ou cancelamento descarta ambas as taxas, inclusive download já concluído. Nova execução limpa apenas o resultado anterior de transferência; ICMP é independente. Benchmarks, ICMP e exportações ficam bloqueados durante a transferência e vice-versa.

Mbit/s = bytes do corpo × 8 / segundos / 1.000.000. Cronômetro monotônico por etapa inclui conexão/DNS/TLS quando necessários, espera do servidor e leitura da resposta. Não desconta latência nem estima capacidade máxima. Transferências pequenas e sequenciais podem subestimar links rápidos; a medição não reproduz a metodologia do site Cloudflare, não mede bufferbloat e não diagnostica o provedor.

Relatórios `linux-network` agora usam esquema 2, com campos `throughput`, `throughputStatus` e `throughputMethodology`. Preservam os campos anteriores de inventário e ICMP; a transferência concluída contém servidor, datas inicial/final, bytes, duração e taxas numéricas. Ausência, erro e cancelamento mantêm `throughput: null` e estado explícito. Sem persistência automática.

```bash
# Simulado, sem tráfego externo:
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --network-only
# Opcional: envia e recebe dados reais com a Cloudflare; Ctrl+C cancela.
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --live-throughput
```

Implementação: `Hardware/Network/NetworkThroughputTest.cs`, `Views/MainWindow.Throughput.cs` e `Hardware/Reports/NetworkReportBuilder.cs`. Testes HTTP simulados validam destino, método, volume, ordem, cálculo, erros HTTP, redirecionamento, corpo truncado, compressão, resposta antecipada de upload, timeout, cancelamento durante download/upload e liberação de streams. Usuário confirmou “tudo validado” para o roteiro de transferência real e interação gráfica em 25/10/2026, incluindo resultados, exportação, cancelamento, bloqueios e fechamento. Valores individuais das medições não foram informados; nenhum teste de volume externo foi executado pelo agente.

Validação de 25/10/2026: builds Avalonia/SmokeTests sem avisos/erros e suíte completa aprovados; `git diff --check` aprovado.


## Resolução de nomes pelo sistema

Em Sistema e sensores → Rede, informe um domínio/host e clique em RESOLVER NOME. Campo vazio, IP, URL, porta, caminho e rótulos inválidos são rejeitados antes da consulta. Nomes internacionais são normalizados com IDN para ASCII; ponto final explícito é preservado. Nomes simples, como `localhost`, seguem a resolução local configurada. Não há domínio padrão nem consulta automática.

Uma chamada a [Dns.GetHostAddressesAsync](https://learn.microsoft.com/en-us/dotnet/api/system.net.dns.gethostaddressesasync?view=net-8.0) retorna os endereços observados, IPv4/IPv6 com escopo quando presente, deduplicados em ordem de retorno. Cronômetro monotônico mede a duração da chamada. Cache, hosts, NSS, busca por sufixos e VPN podem influenciar o resultado. Não identifica o servidor que respondeu, não testa individualmente os DNS de resolv.conf, não comprova conectividade com os IPs e não altera configuração.

O token solicita cancelamento após 5 segundos; cancelamento manual/fechamento usa o mesmo fluxo. A tarefa é aguardada antes de liberar a execução/fechar; o encerramento efetivo depende do suporte da API e do resolvedor do sistema, podendo ultrapassar o prazo. Resultado que chega após cancelamento é descartado. Resposta vazia é distinta de exceção, cancelamento ou ausência de consulta. Nova tentativa limpa somente o resultado anterior de resolução. ICMP, throughput, benchmarks e exportações são mutuamente exclusivos com esta consulta.

Relatórios `linux-network` passam ao esquema 3: adicionam `dnsResolution`, `dnsResolutionStatus` e `dnsMethodology`, mantendo os campos anteriores. Exportam nome normalizado, endereços copiados, início/conclusão e tempo em milissegundos. Falha/cancelamento mantém resultado nulo e estado explícito; pode ser preservado resultado anterior de ICMP ou transferência, com suas próprias datas. HTML escapa os textos. O nome consultado também passa a integrar o relatório.

```bash
# Testes simulados sem consultas externas:
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --network-only
# Consulta real opt-in pelo resolvedor local:
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --live-dns localhost
```

Builds Avalonia/SmokeTests sem avisos/erros e suíte completa aprovados em 25/10/2026. Testes cobrem validação/IDN, IPv4/IPv6/escopo, deduplicação/snapshot, duração, ausência, falha, timeout, cancelamento e relatório. Usuário confirmou a validação do roteiro desta etapa em 25/10/2026, incluindo consulta externa, entradas inválidas, exportação e controles gráficos. Cenários condicionais seguem a disponibilidade indicada; domínios e valores individuais não foram informados.

Consulta real `--live-dns localhost` aprovada no ambiente do agente: retornou `::1` e `127.0.0.1`, em 14,29 ms observados. Isso valida integração com o resolvedor local, sem comprovar DNS externo. CLI recompilado sem avisos/erros após inclusão do diagnóstico. `git diff --check` aprovado.


## Navegação de Sistema e sensores

Sistema e sensores permanece como seção principal no menu lateral. O conteúdo agora se divide em sete sub-abas: Visão geral, Computador, Sensores, Rede, Armazenamento, Alimentação e Maker. Cada cartão possui sua própria área de rolagem; título, status de telemetria e navegação ficam fora dela. Cabeçalhos das sub-abas podem quebrar em linhas conforme a largura da janela.

Visão geral é a seleção inicial. Trocar de seção não recria os controles nem inicia consultas ativas; seleções, campos e resultados continuam nos mesmos objetos durante a sessão. Ao voltar de Benchmark, a sub-aba selecionada permanece. O resultado ICMP e sua explicação agora aparecem junto aos controles de latência, antes do bloco de download/upload. Os serviços e regras de exclusão/cancelamento não foram modificados.

Build Avalonia aprovado, sem avisos/erros; verificação estrutural confirmou sete cartões em sete sub-abas e preservação de todos os controles nomeados/eventos. A execução gráfica no ambiente do agente falhou com `XOpenDisplay failed`; navegação, dimensionamento e interação aguardam o roteiro gráfico do usuário. Não foram criados testes que apenas repetem a estrutura visual.


### Resumo da Visão geral

O cartão inicial apresenta um resumo inspirado no fastfetch, com logo, nome da máquina, distribuição, kernel, arquitetura do processo, CPU, threads disponíveis, placa-mãe, BIOS e memória total. Usa os dados existentes de `LinuxSystemInfoCollector`; não executa fastfetch nem instala dependências. GPU usa a identificação/fonte reportada pelos sensores atuais, sem inferir modelo a partir de driver genérico. O espaço de `/` vem da última coleta de montagens, sem somar volumes e sem confundir essa capacidade com a de todos os discos.

CPU, temperatura e memória permanecem ao vivo. Campos ausentes têm estado explícito; erro de telemetria limpa identificação GPU e erro de armazenamento limpa capacidade do resumo. A seleção das sub-abas continua preservada. Build Avalonia/SmokeTests sem avisos/erros e suíte completa aprovados; resumo aprovado pelo usuário, que solicitou o commit da entrega.

## Metodologia e validação CPU/RAM

Revisão de 25/10/2026: banda de RAM corrigida para processar todo o buffer com qualquer quantidade de threads e verificar cancelamento por blocos. Novos históricos/relatórios registram metodologia e parâmetros; banda usa `linux-ram-bandwidth-v2`. Testes pequenos disponíveis com `--memory-only`. Usuário aprovou o funcionamento desta etapa. Valores das repetições e comparação Windows/Linux continuam pendentes no [roteiro de benchmarks](../docs/VALIDACAO_BENCHMARKS.md).

## Benchmarks de disco e carregamento

Na página **Benchmark**, informe uma pasta absoluta existente e use **TESTAR DISCO** ou **TESTAR CARREGAMENTO**. Arquivos temporários exclusivos, Linux x64 com I/O direto verificado, sem acesso a discos brutos. Disco usa 512 MiB mais escrita aleatória por 4 s; carregamento usa 512 MiB + 1000 assets de 64 KiB. Reserve 1,1 GiB livres. Cancelamento, histórico e exportação seguem o fluxo existente. Em Btrfs, apenas arquivos temporários usam NoCoW/NoCompress; resultados não representam o modo CoW padrão.

Testes simulados com `--disk-only`; diagnóstico real reduzido opt-in com `--live-disk-smoke <pasta>`. Backend real aprovado no Btrfs local; tmpfs recusado. Validação gráfica e cargas completas pendentes. Consulte [metodologia, limites e roteiro](../docs/VALIDACAO_DISCO.md).

## Limpeza de memória e estabilidade

O cartão RAM oferece **LIBERAR MEMÓRIA DOS TESTES** após a execução: solicita coleta/compactação do .NET, mostra memória gerenciada antes/depois e preserva histórico. Não equivale a limpar toda a RAM do sistema. A ação fica bloqueada durante testes/exportações.

**Sistema e sensores → Estabilidade** consulta manualmente contadores EDAC de erros corrigidos/não corrigidos de memória. Ausência de suporte é indisponibilidade, não aprovação. Etapa inicial: logs do kernel/GPU e outros diagnósticos ainda pendentes. Testes dedicados: `--memory-cleanup-only`, `--stability-only`; leitura real: `--live-edac`. Validação gráfica será feita junto com disco, conforme o [roteiro conjunto](../docs/VALIDACAO_ESTABILIDADE_MEMORIA.md).

## Ajustes após o aceite conjunto

Usuário aprovou disco/carregamento e limpeza/EDAC. Após o feedback, progresso/CANCELAR acompanham o cartão do benchmark ativo. O cartão da sessão tem limpeza própria (somente texto); o histórico salvo tem limpeza com confirmação, incluindo pendências, para começar novas exportações sem registros antigos. Arquivos já exportados e configurações são preservados. Aviso da RAM explica recuperação em etapas e oferece “Mais detalhes”. Revisão dos demais cartões ficou para depois; novos controles ainda aguardam conferência visual no [roteiro atualizado](../docs/VALIDACAO_ESTABILIDADE_MEMORIA.md).
