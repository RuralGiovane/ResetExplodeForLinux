# 0011 — 26/11/2026 — Especificações GPU via NVML

## Pedido e entrega

O usuário escolheu ampliar as especificações GPU. O inventário Linux agora consulta núcleos GPU, barramento de memória em bits e clocks máximos gráfico/de memória em MHz pela NVML, com associação por PCI. Os campos aparecem no inventário da interface e no diagnóstico `--gpu-inventory`.

As consultas usam símbolos opcionais, sem exigir uma nova versão do driver. Falhas são tratadas por campo; ausência, zero e sentinela uint.MaxValue não viram especificação válida. Os dados não são extrapolados para SMs, RT/Tensor, taxa efetiva, largura de banda ou desempenho teórico. Tipo de memória e litografia permanecem explicitamente indisponíveis: o banco Windows não documenta fontes por variante e não foi importado.

Referência: [consultas oficiais NVML](https://docs.nvidia.com/deploy/nvml-api/latest/api/group__nvmlDeviceQueries.html). Registro da entrega de ampliação NVML; o catálogo posterior está no registro 0012.

## Validação

- Builds Release de SmokeTests, Avalonia e UiChecks aprovados, sem avisos ou erros.
- Suíte completa net8.0 aprovada. Fixtures conferem unidades, isolamento NVIDIA/Intel/AMD, campos ausentes, valores inválidos e preservação das outras especificações.
- Diagnóstico real da GTX 1650: 896 núcleos, 128 bits, máximo gráfico de 1785 MHz e máximo de memória de 6001 MHz. Intel/i915 mantém os campos NVML indisponíveis. Esses valores não são medições de desempenho.
- Regressão gráfica aprovada nas 56 combinações de abas/tamanhos, com inventário expandido. Captura inicial da aba GPU em 820×620 revisada; não comprova inspeção visual de todas as linhas abaixo da dobra nem aceite manual de teclado no mínimo nativo.
- Dentro do sandbox a NVML não acessou a NVIDIA e o inventário permaneceu disponível via sysfs. A consulta fora dele retornou os campos acima. Não foi iniciado stress GPU.

## Pendências

Tipo de memória e litografia exigem fontes verificáveis e associação inequívoca à variante. Ampliação equivalente AMD/Intel e validação real AMD/Xe continuam pendentes, assim como exportação pelos diálogos, carga prolongada concluída e camada Khronos. O inventário não altera o esquema dos relatórios de benchmark.
