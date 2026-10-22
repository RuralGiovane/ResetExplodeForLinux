# Mapa do projeto

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
    Benchmarks/             coordenação, política térmica, RAM
    Persistence/            configurações e histórico XDG
    Reports/                exportação de benchmarks
  Hardware/
    Common/                 acesso a procfs/sysfs e arquivos
    Models/                 modelos de telemetria
    Interop/                integração NVML
    Telemetry/              sistema e sensores
    Storage/                blocos, montagens e SMART
    Power/                  bateria e fontes
    Maker/                  portas seriais, USB, grupos e aliases
    Reports/                exportações de hardware
  SmokeTests/
    Program.cs              seleção dos modos de teste/diagnóstico
    Core/                   persistência e política térmica
    Telemetry/, Storage/    testes dos coletores/processos
    Power/, Maker/          alimentação e seriais
    Reports/                snapshots, HTML e JSON
  validate.sh               validação normal/offline
  README.md                 instruções e limites
docs/                      documentação versionada
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

As compilações Avalonia e WPF não foram confirmadas nesta reorganização. Recursos, referências e XML foram revisados estaticamente; a validação gráfica depende da execução nos respectivos ambientes. `bin/`, `obj/` e pacotes gerados são ignorados no Git.
