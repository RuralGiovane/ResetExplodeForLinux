# 0012 — 26/11/2026 — Catálogo de referências GPU

## Pedido e entrega

Após ampliar a NVML, o usuário pediu o próximo passo. Foi implementado um catálogo local para tipo de memória e litografia, identificado por fabricante/dispositivo PCI, com rótulo de referência e URLs de proveniência. Não substitui leituras do driver nem exige rede durante a coleta.

Cobertura inicial deliberadamente limitada: GTX 1080 Ti (10de:1b06) e RTX 2080 Ti (10de:1e04 e 10de:1e07). A [lista oficial de IDs NVIDIA](https://download.nvidia.com/XFree86/Linux-x86_64/590.48.01/README/supportedchips.html) confirma os modelos; a tabela 1 do [artigo técnico NVIDIA](https://developer.nvidia.com/blog/nvidia-turing-architecture-in-depth/) informa tipo de memória e processo. Nome reportado conflitante bloqueia o preenchimento. Não se reutilizou o fallback para primeira variante do banco Windows.

## Limite encontrado na máquina atual

O DMI informa Acer Nitro AN515-55 e SKU sem identificação útil. O usuário não pôde conferir o sufixo completo agora. As fichas Acer encontradas descrevem variantes específicas; por isso a GTX 1650 permanece sem referência de memória/litografia. Não se inferiu o tipo de memória pelo clock, nem o processo pela família da arquitetura.

## Validação

- Builds Release SmokeTests e Avalonia: zero avisos/erros.
- Suíte completa net8.0 aprovada. Fixtures exercitam os três IDs, fonte explícita, funcionamento sem NVML, nomes conflitantes, fabricante errado, GTX 1650 não cadastrada e preservação das medições.
- Diagnóstico real somente leitura: Intel/i915 e GTX 1650 continuam identificadas. GTX 1650 preserva 896 núcleos, 128 bits e clocks máximos NVML, com memória/litografia indisponíveis. Nenhum stress iniciado.
- Os dois modelos cadastrados não foram validados em hardware real. Não houve novo aceite visual das linhas de referência; o build da interface não comprova esse aceite.

Registro da entrega do catálogo de referência. Continuam pendentes expansão do catálogo, cobertura AMD/Intel, validações GPU e exportações já registradas no roteiro.
