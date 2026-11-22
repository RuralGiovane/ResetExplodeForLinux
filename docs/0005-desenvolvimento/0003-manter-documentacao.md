# Como manter esta documentação

## Qual documento editar

| Mudança | Documento principal |
| --- | --- |
| Comportamento de uma função | Ficha em 0003-funcionalidades |
| Formato/semântica de dados | Contrato em 0004-contratos |
| Responsabilidade ou fluxo entre camadas | 0002-arquitetura |
| Escolha e trade-off duradouro | Registro em 0006-decisoes |
| Entrega, resultado e motivo contextual | Entrada em 0007-historico |
| Estado do porte | Matriz em 0001-projeto |
| Resultado manual | Roteiro de validação correspondente |

## Convenções

Numeração de quatro dígitos é local à pasta e serve para navegação, não implica ordem de implementação. Usar nomes descritivos em minúsculas com hífens. Não renumerar páginas existentes só para ordenar uma entrega nova.

Cada ficha informa estado, propósito, comportamento, código, limites e validação. Vincular fontes e documentos por links relativos. Marcar propostas como propostas; não transformar sugestão técnica em decisão aceita.

Antes de registrar conclusão: descrever o que foi executado, resultado e limites. Separar código implementado, build aprovado, teste automatizado e aceite do usuário. Referenciar commit somente quando ele existir; para trabalho ainda aberto, dizer checkout sem commit.

Os arquivos históricos na raiz de docs permanecem nos caminhos existentes. Não reescrever aceites passados nem copiar todos os logs para cada ficha. Novas páginas são a entrada temática; histórico preserva contexto datado.

## Modelo de funcionalidade

    # Nome da funcionalidade
    Estado e data:
    ## Finalidade
    ## Comportamento atual
    ## Código e referência Windows
    ## Limites e diferenças de plataforma
    ## Validação e próximos passos

## Modelo de decisão

    # Número — Escolha
    Status: proposta / implementada observada / aceita / substituída
    ## Contexto
    ## Escolha e evidência
    ## Consequências
    ## Alternativas e revisão

## Modelo de registro

    # AAAA-MM-DD — Entrega
    ## Pedido e contexto
    ## O que mudou
    ## Por que
    ## Validação
    ## Pendências

Não salvar tokens, credenciais, dumps pessoais ou logs brutos de hardware nesta pasta. Registrar o diagnóstico e a referência à evidência necessária.
