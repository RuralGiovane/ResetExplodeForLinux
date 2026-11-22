# Documentação do ResetExplodeForLinux

Comece por este índice para entender o aplicativo e continuar o porte Windows → Linux. Organização criada em 22/11/2026; descreve o estado atual, incluindo stress CPU/RAM registrado no commit 83f5d11.

## Caminhos de leitura

- **Conhecer o projeto:** [visão geral](0001-projeto/0001-visao-geral.md) → [paridade e roadmap](0001-projeto/0002-paridade-e-roadmap.md).
- **Implementar uma função:** [arquitetura](0002-arquitetura/README.md) → [ficha da funcionalidade](0003-funcionalidades/README.md) → [contratos](0004-contratos/README.md) → [validação](0005-desenvolvimento/0002-validacao.md).
- **Entender o motivo de uma escolha:** [decisões](0006-decisoes/README.md) e [histórico](0007-historico/README.md).
- **Executar ou contribuir:** [compilar/executar](0005-desenvolvimento/0001-compilar-e-executar.md) e [manter os documentos](0005-desenvolvimento/0003-manter-documentacao.md).

## Seções

| Pasta | Conteúdo |
| --- | --- |
| [0001-projeto/](0001-projeto/README.md) | Identidade, escopo e estado de paridade do porte. |
| [0002-arquitetura/](0002-arquitetura/README.md) | Responsabilidades dos projetos e ciclo de execução. |
| [0003-funcionalidades/](0003-funcionalidades/README.md) | Fichas das funções existentes e pendentes. O estado de cada ficha descreve Linux, com referência Windows separada. |
| [0004-contratos/](0004-contratos/README.md) | Modelos, formatos persistidos/exportados e integrações locais. O projeto não possui uma API HTTP própria. |
| [0005-desenvolvimento/](0005-desenvolvimento/README.md) | Execução, testes, evidências e manutenção da documentação. |
| [0006-decisoes/](0006-decisoes/README.md) | Registros do contexto, escolha e consequências. Decisões retrospectivas são identificadas; propostas não são tratadas como aceitas. |
| [0007-historico/](0007-historico/README.md) | Registros datados do pedido, mudanças, motivos e verificações. Não são logs brutos de execução. |

A numeração é local à pasta, para navegação. Não é promessa de ordem de implementação. Funções pendentes também têm documentação para registrar referência, limites e critérios futuros.

## Estado e evidência

As fichas distinguem implementado, parcial, pendente e validação manual. Código presente não significa paridade 1:1. Build aprovado não significa hardware validado. Aceites de outubro não aprovam automaticamente novas mudanças.

Os contratos reais são C#, arquivos JSON/HTML e chamadas ao sistema; não há backend HTTP, login ou pagamentos neste app.

## Documentação anterior preservada

| Documento | Uso |
| --- | --- |
| [ESTRUTURA.md](ESTRUTURA.md) | Mapa físico e correspondência de caminhos históricos |
| [Plano mestre](plano_porte_linux_reset_explode.md) | Plano e entradas de evolução; ler datas e estado atual |
| [Registro de alterações](REGISTRO_DE_ALTERACOES.md) | Histórico acumulado desde outubro |
| [Validação gráfica](VALIDACAO_GRAFICA.md) | Cenários de interface e aceites datados |
| [Hardware](VALIDACAO_HARDWARE.md) | Diagnósticos reais e limitações |
| [Benchmarks CPU/RAM](VALIDACAO_BENCHMARKS.md) | Metodologia e repetibilidade |
| [Disco](VALIDACAO_DISCO.md) | I/O direto e cargas de armazenamento |
| [Estabilidade/memória](VALIDACAO_ESTABILIDADE_MEMORIA.md) | EDAC, journal, sessões, stress e limpeza |

Os arquivos anteriores não foram movidos para evitar quebra de links e perda do contexto original. Se uma pendência antiga já foi resolvida, atualizar a ficha atual e registrar a evolução; não apagar a evidência histórica. Mudanças futuras devem atualizar a ficha/contrato afetado e adicionar contexto conforme o guia de manutenção.
