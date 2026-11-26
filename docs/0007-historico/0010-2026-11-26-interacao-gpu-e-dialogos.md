# 0010 — 26/11/2026 — Interação GPU e diálogos

## Entrega

Adicionado `UiChecks --gpu-interaction <diretório-vazio>` para validação guiada na sessão gráfica. O operador usa o teclado e os diálogos reais de exportação. O harness registra abertura do dropdown, navegação, seleção e retorno de foco; verifica cancelamento, liberação dos controles e conteúdo dos arquivos HTML/JSON. Configuração e histórico ficam isolados, com fixture identificada como sintética. Botões de benchmark/stress são bloqueados, sem carga real.

## Evidências

- Build Release de Avalonia/UiChecks aprovado sem avisos ou erros.
- Suíte completa SmokeTests net8.0 aprovada.
- Teclado/dropdown/retorno de foco aprovados pelo harness em 945×1029. O usuário confirmou que funcionou e ficou legível.
- Cancelamento do primeiro diálogo aprovado, com controles liberados e diretório de evidências vazio.
- Primeira tentativa de salvamento HTML cancelada pelo operador; o harness retornou código 1 e não registrou conclusão das exportações.
- O usuário confirmou que apareceram opções de salvar HTML/JSON, mas optou por não salvar. Abertura relatada dos diálogos não comprova gravação nem conteúdo dos arquivos. A segunda tentativa foi encerrada sem concluir essa validação.
- O bloqueio de início de testes observado pelo usuário é deliberado neste harness; não é alteração do aplicativo normal.

## Limites

O compositor não aplicou o tamanho solicitado de 820×620. O aceite registrado refere-se a 945×1029. Fixture de exportação não representa uma execução GPU real nem conclui o aceite de carga prolongada. Permanecem as pendências térmicas, AMD/Xe e camada Khronos do [roteiro GPU](../VALIDACAO_GPU.md).
