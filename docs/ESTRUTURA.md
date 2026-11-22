# Mapa do projeto

Entrada atual da documentação: [índice geral](README.md). Este mapa preserva também o contexto da reorganização anterior; o [guia de arquitetura](0002-arquitetura/0001-camadas-e-fontes.md) descreve as responsabilidades atuais.

Todos os caminhos abaixo são relativos à raiz Git `ResetExplodeForLinux/`. Os nomes da aplicação, namespaces e assemblies continuam iguais; pastas representam responsabilidades, sem exigir namespaces novos.

```text
Assets/                     imagens compartilhadas
Shared/Benchmarks/          renderizador e simulação CPU originais
Windows/                    projeto WPF e código original
  Drivers/                  pacotes seriais Windows
  publicar.bat              publicação local Windows
Linux/
  App.axaml, Program.cs     inicialização Avalonia
  ResetExplode.csproj       projeto da interface
  Views/                    MainWindow e seus arquivos parciais
  Styles/                   tema Avalonia
  Core/
    Benchmarks/             CPU/RAM/disco, stress, coordenação e política térmica
    Storage/                workspace temporário e backend de I/O direto
    Lifecycle/              instância única e pedidos de ativação
    Persistence/            configurações e histórico XDG
    Reports/                exportação de benchmarks
  Hardware/
    Network/                inventário, ICMP, DNS e throughput HTTP limitado
    Common/                 acesso a procfs/sysfs e arquivos
    Models/                 modelos de telemetria
    Interop/                integração NVML
    Telemetry/              sistema e sensores
    Storage/                blocos, montagens e SMART
    Power/                  bateria e fontes
    Maker/                  portas seriais, USB, grupos e aliases
    Reports/                exportações de hardware
    Stability/              EDAC, journal, classificação e comparação por sessão
  SmokeTests/
    Program.cs              seleção dos modos de teste/diagnóstico
    Core/                   persistência e política térmica
    Telemetry/, Storage/    testes dos coletores/processos
    Power/, Maker/          alimentação e seriais
    Reports/                snapshots, HTML e JSON
    Network/, Stability/    rede, EDAC/journal, deltas e stress CPU/RAM
  validate.sh               validação normal/offline
  README.md                 instruções e limites
docs/                       documentação versionada (entrada: README.md)
  0001-projeto/             identidade e paridade
  0002-arquitetura/         camadas e execução
  0003-funcionalidades/     fichas do porte
  0004-contratos/           modelos, arquivos e integrações
  0005-desenvolvimento/     build, validação e manutenção da documentação
  0006-decisoes/            contexto e consequências das escolhas
  0007-historico/           registros datados
.github/workflows/         CI Linux e publicação Windows
```

## Onde alterar cada função

- Coleta de dados: `Linux/Hardware/<área>/`. Modelos específicos pequenos permanecem junto ao respectivo coletor.
- Exibição, comandos e ciclo de vida: `Linux/Views/MainWindow.<área>.cs`; layout em `Views/MainWindow.axaml`.
- Exportação: builders em `Core/Reports/` ou `Hardware/Reports/`; diálogos e escrita em `Views/MainWindow.Export.cs`.
- Regressões: `Linux/SmokeTests/<área>/`. Fontes são incluídas recursivamente pelos projetos SDK.
- Algoritmos compartilhados: `Shared/Benchmarks/`, referenciados pelos projetos sem cópia.

Os arquivos `.csproj` Linux e o script de validação mantêm os caminhos públicos anteriores. `App.axaml` referencia `Styles/Theme.axaml`. Windows usa as imagens de `Assets/` por recursos vinculados, mantendo seus nomes lógicos. O workflow Windows usa o novo caminho do projeto e dos documentos de distribuição; os drivers permanecem recursos embutidos.

## Caminhos antigos

| Antes | Agora |
| --- | --- |
| Fontes/projeto WPF na raiz | `Windows/` |
| `BenchRenderer.cs`, `GameSimBench.cs` | `Shared/Benchmarks/` |
| `Drivers/` | `Windows/Drivers/` |
| `Linux/MainWindow*` | `Linux/Views/MainWindow*` |
| `Linux/Theme.axaml` | `Linux/Styles/Theme.axaml` |
| Fontes diretamente em Core/Hardware/SmokeTests | Subpastas por assunto, conforme árvore acima |
| Guia e registro fora do Git | `docs/plano_porte_linux_reset_explode.md`, `docs/REGISTRO_DE_ALTERACOES.md` |
| README Windows na raiz | `Windows/README.md`; a raiz contém a apresentação do fork |

As entradas anteriores do registro preservam os caminhos históricos. Os documentos fora do repositório foram substituídos por apontadores para as versões completas em `docs/`.

## Validação e limites

Execute `bash Linux/validate.sh --offline-net8` para Core/Hardware/SmokeTests. O modo suplementar `--offline-net10` seleciona recursivamente os fontes, excluindo `bin`/`obj`. Execute `bash Linux/validate.sh` para incluir Avalonia quando os pacotes estiverem acessíveis.

Na reorganização original, as compilações Avalonia e WPF não tinham sido confirmadas; recursos, referências e XML foram revisados estaticamente. Esse registro não descreve o estado atual: consulte as [evidências de validação](0005-desenvolvimento/0002-validacao.md). `bin/`, `obj/` e pacotes gerados são ignorados no Git.
