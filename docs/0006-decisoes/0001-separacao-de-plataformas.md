# 0001 — Separar plataformas e compartilhar algoritmos CPU

Status: implementação existente observada em 22/11/2026; registro retrospectivo, não nova escolha de framework.

## Contexto

A referência usa WPF, WMI, ETW e Direct3D, enquanto o destino precisa executar em Linux. Misturar os dois conjuntos de fontes nos mesmos itens automáticos criaria dependências e pontos de entrada incompatíveis.

## Escolha e evidência

Windows e Linux possuem projetos próprios. Avalonia apresenta a UI Linux; Core e Hardware separam algoritmos e coleta. BenchRenderer e GameSimBench são compilados diretamente de Shared pelos dois destinos.

Evidência: projetos [Windows](../../Windows/ResetExplode.csproj), [Linux](../../Linux/ResetExplode.csproj) e [Core](../../Linux/Core/ResetExplode.Core.csproj); [registro histórico](../REGISTRO_DE_ALTERACOES.md).

## Consequências

Os algoritmos CPU compartilhados não divergem por cópia, mas qualquer alteração afeta os dois destinos. Interfaces e backends precisam de integração própria. O porte não ganha comparabilidade de medições só por usar a mesma fonte.

## Alternativas e revisão

Copiar os algoritmos duplicaria manutenção. A separação não determina a futura biblioteca Vulkan nem exige converter tudo para MVVM. Essas seriam decisões novas, com evidência e critérios próprios.
