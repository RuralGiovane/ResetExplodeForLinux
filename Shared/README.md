# Fontes compartilhadas

`Benchmarks/BenchRenderer.cs` e `Benchmarks/GameSimBench.cs` são os algoritmos originais de renderização e simulação de CPU. Os projetos Windows e Linux/Core compilam estes mesmos arquivos por referência de fonte, sem duplicação.

Mantenha esta pasta independente de WPF, Avalonia e APIs específicas de sistema. Alterações aqui afetam ambos os destinos e disparam a validação Linux no CI.
