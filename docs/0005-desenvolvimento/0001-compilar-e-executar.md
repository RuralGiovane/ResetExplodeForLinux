# Compilar e executar

Execute os comandos na raiz do repositório. Dependências: SDK/runtime .NET compatível com net8.0, pacotes NuGet e, para UI, uma sessão gráfica com bibliotecas nativas do Avalonia.

## Fluxo normal

    bash Linux/validate.sh
    dotnet run --project Linux/ResetExplode.csproj -c Release --no-build

O script compila Avalonia, Core, Hardware e SmokeTests e executa a suíte. Não executa automaticamente benchmarks prolongados nem diagnósticos externos opt-in.

## Sem rede

    bash Linux/validate.sh --offline-net8

Valida os projetos reais Core/Hardware/SmokeTests sem compilar Avalonia. Exige targeting packs/runtime instalados. O modo offline-net10 é suplementar: agrega fontes em outro alvo e não comprova os projetos originais net8.0.

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
