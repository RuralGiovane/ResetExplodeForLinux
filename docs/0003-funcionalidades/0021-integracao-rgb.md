# Integração RGB

**Estado Linux em 22/11/2026:** Pendente no Linux.

## Finalidade

Portar o controle de iluminação e efeitos da referência.

## Comportamento atual

Windows integra iCUE, OpenRGB e Razer Chroma, incluindo efeitos ligados a sensores. Não existe implementação Linux correspondente no checkout.

## Código e referência Windows

- [docs/plano_porte_linux_reset_explode.md](../../docs/plano_porte_linux_reset_explode.md)

Referência Windows:

- [Windows/LightBackends.cs](../../Windows/LightBackends.cs)
- [Windows/CorsairSdk.cs](../../Windows/CorsairSdk.cs)
- [Windows/CorsairSdkInstaller.cs](../../Windows/CorsairSdkInstaller.cs)
- [Windows/MainWindow.Integ.cs](../../Windows/MainWindow.Integ.cs)

## Limites e diferenças

Disponibilidade Linux de cada ponte e dependência precisa ser verificada antes da escolha. Existir integração OpenRGB no Windows não significa que ela já está conectada à UI Linux.

## Validação e continuidade

Critérios futuros: descoberta, dispositivo/serviço indisponível, concorrência com software do fabricante, efeitos, brilho e devolução do controle ao fechar.

[Roteiro relacionado](../../docs/VALIDACAO_HARDWARE.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
