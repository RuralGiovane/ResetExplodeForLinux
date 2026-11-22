# Visão geral do ResetExplodeForLinux

Atualizado em 22/11/2026. Retrato do projeto, incluindo o stress CPU/RAM registrado no commit 83f5d11.

O RESET EXPLODE é um aplicativo desktop de diagnóstico, monitoramento, benchmarks e ferramentas de hardware. Este repositório é o fork que porta a versão Windows para Linux. O nome do repositório é ResetExplodeForLinux; produto, namespaces e assembly continuam ResetExplode, versão declarada 0.16.0.

## Objetivo e público

Atender quem quer identificar componentes, acompanhar sensores, medir desempenho e investigar estabilidade no Linux. O objetivo informado pelo mantenedor é um porte 1:1 do Windows. Isso exige comparar comportamentos e resultados, além de reproduzir telas. A paridade ainda não foi atingida.

Não há backend HTTP, login, contas ou serviço de pagamentos na implementação atual. O app funciona localmente; alguns testes de rede acessam destinos externos explicitamente.

## Plataformas

| Pasta | Papel |
| --- | --- |
| [Windows](../../Windows/) | Referência .NET 8, WPF e APIs Windows |
| [Linux](../../Linux/) | Destino .NET 8, Avalonia 11.3.22 e coletores Linux |
| [Shared](../../Shared/) | Algoritmos CPU compilados pelos dois destinos |
| [Assets](../../Assets/) | Identidade visual compartilhada |

## Como ler o estado do projeto

- **Implementado:** existe comportamento no código; não implica validação em todo hardware.
- **Parcial:** parte da função Windows foi portada.
- **Pendente:** existe na referência ou no plano, mas não foi localizada implementação Linux correspondente.
- **Validado automaticamente:** há execução de testes/build registrada.
- **Aceite manual histórico:** confirmação anterior, limitada à versão/cenário registrado.
- **Validação manual pendente:** ainda precisa de interação ou equipamento real.

Consulte a [matriz de paridade](0002-paridade-e-roadmap.md) e as [evidências atuais](../0005-desenvolvimento/0002-validacao.md). Licença [MIT](../../LICENSE); dependências têm suas próprias licenças.
