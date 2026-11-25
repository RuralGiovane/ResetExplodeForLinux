# Compilar e executar

Execute os comandos na raiz do repositório. Dependências: SDK/runtime .NET compatível com net8.0, pacotes NuGet e, para UI, uma sessão gráfica com bibliotecas nativas do Avalonia.

## Fluxo normal

    bash Linux/validate.sh
    dotnet run --project Linux/ResetExplode.csproj -c Release --no-build

O script compila Avalonia, Core, Hardware, SmokeTests e UiChecks e executa a suíte SmokeTests. Não executa automaticamente benchmarks prolongados nem diagnósticos externos opt-in.

## Sem rede

    bash Linux/validate.sh --offline-net8

Valida os projetos reais Core/Hardware/SmokeTests sem compilar Avalonia. Exige targeting packs/runtime instalados e Silk.NET.Vulkan 2.22.0/dependências no cache NuGet. O modo offline-net10 é suplementar: agrega fontes em outro alvo e não comprova os projetos originais net8.0.

## Ambiente usado em 22/11/2026

O script offline encontrou apphost 8.0.33 ausente. A compilação foi feita sem launcher nativo:

    DOTNET_CLI_HOME=/tmp/reset-dotnet-home dotnet build Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release --disable-build-servers -m:1 -p:UseAppHost=false -p:EnableTargetingPackDownload=false -p:RestoreSources= -p:NuGetAudit=false
    dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll
    DOTNET_CLI_HOME=/tmp/reset-dotnet-home dotnet build Linux/ResetExplode.csproj -c Release --no-restore --disable-build-servers -m:1 -p:UseAppHost=false
    dotnet Linux/bin/Release/net8.0/ResetExplode.dll

O build --no-restore da UI depende de assets/pacotes já restaurados. O override UseAppHost=false não foi gravado nos projetos. NuGetAudit=false foi usado nesse diagnóstico, não definido como política de CI.

## Windows e distribuição

No Windows: dotnet run --project Windows/ResetExplode.csproj; publicação local em Windows/publicar.bat. O build Windows não foi executado neste ambiente.

[CI Linux](../../.github/workflows/linux.yml) usa Ubuntu 24.04/.NET 8. [Publicação Windows](../../.github/workflows/publicar.yml) gera releases Windows. Não confundir esse workflow com distribuição Linux pronta.

## Shaders GPU

GLSL e SPIR-V são versionados; não precisam de compilador em runtime. Recompilação e verificações em [VALIDACAO_GPU](../VALIDACAO_GPU.md). O modo offline-net10 foi atualizado para incluir bindings/recursos, mas não executado neste ambiente sem SDK 10.

## Regressão gráfica de responsividade

Após a validação normal, em uma sessão gráfica:

```bash
dotnet Linux/UiChecks/bin/Release/net8.0/ResetExplode.UiChecks.dll /tmp/reset-ui-captures
```

O diretório de capturas é opcional. O teste abre uma janela, consulta telemetria e detecção de hardware, mas não inicia benchmarks ou stress. Usa configuração e histórico temporários, removidos ao fechar normalmente. Não redimensione a janela durante a execução: mudanças externas do compositor também são registradas como instabilidade.

Verifica as oito abas de Sistema e sensores e as seis de Benchmark em áreas de conteúdo de 820×589, 820×620, 960×620 e 1180×850. Limita explicitamente o conteúdo porque o gerenciador de janelas pode ignorar o tamanho solicitado; portanto, não comprova essas mesmas dimensões na janela nativa. Verifica estabilidade dos layouts e do tamanho nativo após acomodação e gera capturas das áreas de 820 px.

Também simula eventos de resize para garantir que alocações vazias não alterem o mínimo, alocações menores sejam aceitas e crescer não restaure o mínimo anterior. O construtor do evento é interno no Avalonia 11.3.22; somente o teste usa reflexão para acessá-lo. Uma mudança dessa API falha explicitamente. A execução gráfica é opt-in; sua compilação faz parte da validação padrão/CI.
