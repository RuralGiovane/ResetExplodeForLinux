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

Teste ativo de latência, jitter e perda ICMP implementado em 25/10/2026, com build e suíte aprovados; a validação desta etapa foi aprovada pelo usuário, conforme o [roteiro](docs/VALIDACAO_GRAFICA.md).

Exportação de rede HTML/JSON implementada, incluindo inventário e teste ICMP; validação gráfica aprovada pelo usuário em 25/10/2026, conforme o roteiro.

Download/upload HTTP limitado implementado (Cloudflare, 10 MB + 5 MB), com cancelamento e exportação no esquema 2. Validação desta etapa aprovada pelo usuário em 25/10/2026, conforme o roteiro.

Resolução manual de nomes pelo sistema implementada, com exportação no esquema 3. Build e suíte aprovados; validação desta etapa aprovada pelo usuário em 25/10/2026, conforme o roteiro.

Sistema e sensores reorganizado em sete sub-abas dedicadas, com navegação fixa e rolagem por seção. Organização aprovada pelo usuário; Visão geral ampliada com resumo do sistema inspirado no fastfetch. Build e suíte aprovados; resumo aprovado pelo usuário, conforme o roteiro.

Benchmarks Linux de disco/carregamento implementados com I/O direto e arquivos temporários exclusivos. Backend real reduzido validado em Btrfs; roteiro gráfico/cargas completas aprovado pelo usuário. Ajustes posteriores de cancelamento por cartão e limpeza de histórico descritos no [roteiro atualizado](docs/VALIDACAO_ESTABILIDADE_MEMORIA.md).

## Continuar o trabalho

Leia o [guia](docs/plano_porte_linux_reset_explode.md), o [registro](docs/REGISTRO_DE_ALTERACOES.md) e o [mapa de pastas](docs/ESTRUTURA.md). Os caminhos citados nas entradas históricas podem anteceder a reorganização; o mapa mostra os destinos atuais.

Licença [MIT](LICENSE). Histórico funcional e instruções da versão original estão no [README Windows](Windows/README.md).
