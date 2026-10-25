# RESET EXPLODE para Linux

Fork em migração de Windows/WPF para Linux/Avalonia, mantendo o nome do produto, namespaces e algoritmos compartilhados.

| Pasta | Conteúdo |
| --- | --- |
| [Linux/](Linux/README.md) | Aplicação Avalonia, serviços Linux e testes. |
| [Windows/](Windows/README.md) | Base Windows de referência, projeto WPF e publicação. |
| [Shared/](Shared/README.md) | Algoritmos compilados pelos dois destinos. |
| `Assets/` | Imagens compartilhadas pelas interfaces. |
| [docs/](docs/ESTRUTURA.md) | Guia de migração, registro de alterações e mapa do projeto. |
| `.github/workflows/` | Validação Linux e publicação Windows. |

## Executar e validar

Na raiz do repositório, com SDK/runtime .NET 8 e pacotes NuGet acessíveis:

```bash
bash Linux/validate.sh
dotnet run --project Linux/ResetExplode.csproj -c Release --no-build
```

Para validar apenas os projetos Core, Hardware e SmokeTests sem rede:

```bash
bash Linux/validate.sh --offline-net8
```

A suíte passa em .NET 8, incluindo relatórios, persistência, telemetria, armazenamento, alimentação, Maker e cancelamento de benchmarks. O modo offline não compila a interface Avalonia. Build Avalonia aprovado em 23/10/2026. Roteiro gráfico aprovado pelo usuário em 24/10/2026, incluindo exportações, cancelamento de benchmarks, pré-checagem térmica e fechamento durante execução. Usuário também aprovou GPU/NVML, SMART com autorização administrativa, instância única e inventário de rede com IPv4/IPv6, gateways e DNS. Maker com dispositivo conectado continua pendente por falta de hardware; veja o [estado detalhado](Linux/README.md).

No Windows, o projeto original agora está em `Windows/ResetExplode.csproj`; use `dotnet run --project Windows/ResetExplode.csproj` ou `Windows/publicar.bat` para publicação. A reorganização ajusta caminhos e recursos, sem modificar os algoritmos Windows. Build Windows não validado neste ambiente Linux.

## Continuar o trabalho

Leia o [guia](docs/plano_porte_linux_reset_explode.md), o [registro](docs/REGISTRO_DE_ALTERACOES.md) e o [mapa de pastas](docs/ESTRUTURA.md). Os caminhos citados nas entradas históricas podem anteceder a reorganização; o mapa mostra os destinos atuais.

Licença [MIT](LICENSE). Histórico funcional e instruções da versão original estão no [README Windows](Windows/README.md).
