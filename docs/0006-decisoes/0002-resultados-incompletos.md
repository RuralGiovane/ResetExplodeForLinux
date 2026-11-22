# 0002 — Preservar ausência, cancelamento e limites

Status: implementação existente observada em 22/11/2026.

## Contexto

Sensores e contadores dependem de hardware, drivers e permissões. Algoritmos podem retornar parcialmente ao cancelar. Mostrar zero ou nota parcial como conclusão produziria uma interpretação incorreta.

## Escolha e evidência

Modelos preservam valores anuláveis e avisos. BenchmarkRunner verifica cancelamento após retorno do trabalho. Persistência rejeita métricas/score em resultados incompletos. Relatórios distinguem datas e estados. EDAC/journal não atribuem causalidade automaticamente.

Fontes: [modelos](../../Linux/Hardware/Models/TelemetryModels.cs), [runner](../../Linux/Core/Benchmarks/BenchmarkRunner.cs), [persistência](../../Linux/Core/Persistence/LocalPersistence.cs), [comparação](../../Linux/Hardware/Stability/StabilitySession.cs).

## Consequências

A interface precisa explicar indisponibilidade e resultados inconclusivos. Os consumidores dos relatórios precisam ler estado antes de números. Não ter erros observados não certifica estabilidade.

## Alternativas e revisão

Substituir ausência por zero facilitaria apresentação, mas perderia significado. Publicar score parcial faria execuções canceladas parecerem comparáveis. Uma futura análise automática deve conservar essas distinções e registrar sua metodologia.
