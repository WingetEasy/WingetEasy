<div align="center">
  <img src="src/WingetEasy.App/Assets/Icons/logo.png" alt="WingetEasy Logo" width="120" />

# WingetEasy

**Gerenciador visual para Winget em WinUI 3**

[![CI](https://img.shields.io/badge/CI-ainda%20não%20configurado-777)](#)
[![Version](https://img.shields.io/badge/version-0.1.0--dev-orange)](#)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue)](#)
[![Architecture](https://img.shields.io/badge/architecture-App%20%7C%20Core%20%7C%20Data-success)](#)
[![License](https://img.shields.io/badge/license-see%20LICENSE-lightgrey)](LICENSE)

[Contribuindo](CONTRIBUTING.md) • [Issues iniciais](Documents/2.%20InitialIssues/LABELS.md) • [Licença](LICENSE)

</div>

---

## Visão Geral

WingetEasy é um projeto .NET 8 para Windows com WinUI 3, organizado em três projetos principais:

- `WingetEasy.App` para a interface desktop.
- `WingetEasy.Core` para regras de negócio e contratos.
- `WingetEasy.Data` para persistência e acesso a dados.

O objetivo do projeto é evoluir para uma experiência visual em cima do Winget, mas o estado atual do repositório ainda é de base inicial: os projetos existem, a janela WinUI abre vazia e a camada de domínio ainda está em scaffold.

## Estado Atual

O que já existe no repositório hoje:

- Solução com os projetos `App`, `Core`, `Data` e o projeto de testes `WingetEasy.Core.Tests`.
- Configuração centralizada de compilação em `Directory.Build.props` e de versões de pacotes em `Directory.Package.props`.
- App WinUI 3 com `MicaBackdrop` na janela principal.
- Projeto de testes com xUnit, FluentAssertions, Moq e suporte a cobertura.
- Documento de contribuição com convenções de código e fluxo de PR.

O que ainda não está implementado no código atual:

- Serviços de Winget.
- Modelos de domínio.
- Persistência real com EF Core/SQLite.
- Scheduler, notificações e integração com system tray.
- Fluxo de instalação ou publicação MSIX finalizado.

## Estrutura Do Repositório

```text
WingetEasy/
├── src/
│   ├── WingetEasy.App/      UI WinUI 3
│   ├── WingetEasy.Core/     Regras de negócio e contratos
│   └── WingetEasy.Data/     Camada de dados
├── tests/
│   └── WingetEasy.Core.Tests/
├── CONTRIBUTING.md
└── README.MD
```

## Requisitos

Para compilar e executar a solução localmente:

- Windows 10 19041+ ou Windows 11.
- Visual Studio 2022 com os workloads:
    - Desenvolvimento de área de trabalho com .NET.
    - Desenvolvimento de SDK de Aplicativos do Windows.
- Git e .NET 8 SDK.

## Como Executar

1. Abra `WingetEasy.slnx` no Visual Studio.
2. Aguarde o restore dos pacotes NuGet.
3. Defina `WingetEasy.App` como projeto de inicialização.
4. Pressione F5.

Se tudo estiver correto, a janela principal do app abre com fundo Mica e sem conteúdo adicional por enquanto.

## Pacotes E Stack

As dependências já apontam para a direção do projeto:

- WinUI 3 e Windows App SDK na interface.
- CommunityToolkit para apoio a MVVM e recursos de UI.
- Serilog para logging.
- xUnit, Moq e FluentAssertions para testes.
- Entity Framework Core e SQLite previstos na documentação de issues.

## Documentação Do Projeto

- Veja [CONTRIBUTING.md](CONTRIBUTING.md) para configurar o ambiente e entender as convenções do projeto.
- Veja [pull_request_template.md](pull_request_template.md) para o formato esperado de PR.

## Status Do Roadmap

O backlog inicial está dividido por áreas:

- Setup da solution e dependências.
- Core: modelos, contratos e integração com Winget.
- Data: persistência, repositórios e migrations.
- UI: interface WinUI, tray, notificações e navegação.
- Distribuição: empacotamento, release e documentação.

As issues de maior clareza para começar estão marcadas como `good first issue` no documento de labels.

## Como Contribuir

1. Leia [CONTRIBUTING.md](CONTRIBUTING.md).
2. Escolha uma issue com escopo claro.
3. Siga as convenções de branch, commit e PR descritas na documentação.
4. Execute `dotnet test` antes de abrir o PR.

## Licença

Este projeto está licenciado sob a [LICENSE](LICENSE).
