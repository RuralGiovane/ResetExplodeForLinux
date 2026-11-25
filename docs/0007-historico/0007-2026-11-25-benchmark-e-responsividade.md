# 0007 — 25/11/2026 — Benchmark por componente, prévia GPU e responsividade

## Entrega acumulada

Benchmark reúne Visão geral, CPU, Memória RAM, GPU, Disco e Configurações. CPU/RAM têm cards de stress separados; progresso e cancelamento permanecem acessíveis ao trocar de aba. Históricos filtram por componente e preservam o stress combinado antigo em CPU e RAM. EDAC e journal continuam em Sistema e sensores → Estabilidade.

GPU usa Vulkan offscreen com seleção explícita por PCI, sensor próprio obrigatório, prévia dos quadros reais e histórico/exportações. A sessão anterior registrou validação curta na GTX 1650, cancelamento e fechamento durante carga; não houve nova carga gráfica nesta retomada.

## Correção de resize

O gerenciador de janelas entregava 589 px úteis e o app insistia em MinHeight=620, provocando alternância. MainWindow aceita alocações positivas abaixo do mínimo e não volta a aumentá-lo ao crescer. Textos de título, rótulo e valores grandes quebram linha. Botões outline desabilitados têm contraste explícito no tema escuro.

A sessão interrompida já continha a correção, mas UiChecks não compilava por tentar chamar diretamente o construtor interno de WindowResizedEventArgs. O teste agora acessa esse construtor por reflexão, isolada do aplicativo, com erro explícito se a assinatura mudar. A compilação de UiChecks integra validate.sh; a execução gráfica continua opt-in.

## Verificações

- Build Release de Avalonia e UiChecks: zero avisos/erros.
- Suíte SmokeTests completa em net8.0 aprovada pelo modo offline; inclui GPU simulada, histórico por componente, térmica e exportações.
- UiChecks: 14 abas × quatro tamanhos de conteúdo (820×589, 820×620, 960×620, 1180×850), com estabilidade após acomodação e teste de negociação do mínimo.
- A primeira execução detectou mudança nativa isolada em Maker; repetição completa aprovada. A conferência final após o ajuste de contraste também foi executada.
- Shaders SPIR-V aprovados em spirv-val com alvo Vulkan 1.1.

O teste fixa o conteúdo, pois o compositor pode ignorar o tamanho solicitado da janela. Não comprova interação por teclado/dropdown em janela nativa mínima nem validação prolongada de GPU ou outros fabricantes. Essas pendências seguem em VALIDACAO_GPU.md.
