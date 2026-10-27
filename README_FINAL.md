<div align="center">
  <img src="doc/images/readme/Banner_portuguese.png" alt="WingetEasy Banner" width="100%" />

  # ⚡ WingetEasy

  [![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
  [![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6.svg?style=flat-square&logo=windows)](https://microsoft.com/windows)
  [![License](https://img.shields.io/badge/License-GPL%203.0-blue.svg?style=flat-square)](LICENSE)
  [![Memory](https://img.shields.io/badge/RAM-7%20a%2070%20MB-success.svg?style=flat-square)](#-por-que-wingeteasy)

  **O gerenciador visual definitivo para o WinGet nativo do Windows.**  
  Uma interface moderna em Fluent Design com transparência acrílica nativa do Windows 11, automação de verificações, proteção contra fechamento abrupto de programas abertos e consumo mínimo de memória.  
  Construído com .NET 10, WinUI 3 e arquitetura limpa para oferecer controle absoluto sem telemetria.

  [🚀 Primeiros Passos](#-início-rápido) • [📚 Documentação Técnica](#-índice-da-documentação-técnica) • [📐 Arquitetura](Documents/6.%20Explain/1%20-%20ESTRUTURA_DOCUMENTACAO.md) • [🤖 Regras para IA](.IArules)
</div>

---

## 💡 Por Que WingetEasy?

Enquanto outros gerenciadores de pacotes tentam abraçar múltiplos ecossistemas tornando-se pesados e invasivos, o **WingetEasy** prioriza discrição, leveza e respeito aos recursos da máquina:

- 🪶 **Ultraleve:** Apenas **7 MB a 15 MB** de RAM em repouso na bandeja (*System Tray*) e **~50 a 70 MB** aberto.
- ✨ **Transparência Acrílica Nativa:** Suporte completo a **Desktop Acrylic e Mica** no Windows 11 via `AcrylicBackdropHelper`, com contraste balanceado em modo Claro e Escuro.
- 🛡️ **Proteção de Aplicativos Ativos:** Detecta se navegadores, editores ou comunicadores estão abertos antes de atualizar, evitando fechamento forçado e perda de trabalho.
- 🔄 **Anti-Looping Inteligente:** Identifica e suprime pacotes problemáticos com versão `Unknown` ou `< 1.0` que insistem em aparecer repetidamente.
- 🔐 **Elevação UAC sob Demanda:** Executa como usuário comum e solicita permissões de Administrador em lote único apenas para pacotes que realmente exigem.
- 💾 **Snapshots & Automação:** Crie backups do estado da máquina e exporte scripts autônomos em PowerShell (`.ps1`) para pós-formatação.

---

## ⚡ Início Rápido

### Pré-requisitos
- **Sistema Operacional:** Windows 10 (Build 19041+) ou Windows 11.
- **Ambiente:** [Visual Studio 2022](https://visualstudio.microsoft.com/) (v17.8+) com as cargas:
  - *Desenvolvimento para desktop com .NET*
  - *Desenvolvimento do SDK de Aplicativo do Windows*
- **SDK:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) instalado.
- **Gerenciador Nativo:** Windows Package Manager (`winget.exe`) ativo no sistema.

### Compilação e Execução

```powershell
# 1. Clone o repositório
git clone https://github.com/WingetEasy/WingetEasy.git
cd WingetEasy

# 2. Restaure os pacotes e dependências
dotnet restore WingetEasy.slnx

# 3. Execute a suíte de testes unitários (118 testes)
dotnet test tests/WingetEasy.Core.Tests/WingetEasy.Core.Tests.csproj

# 4. Inicie o aplicativo em modo desktop
dotnet run --project src/WingetEasy.App/WingetEasy.App.csproj
```

> **💡 Dica para execução em segundo plano:**  
> Use `dotnet run --project src/WingetEasy.App/WingetEasy.App.csproj -- --minimized` para subir a aplicação diretamente na bandeja do sistema sem desenhar a janela principal.

---

## 📚 Índice da Documentação Técnica

Para desenvolvedores, contribuidores e arquitetos que desejam entender profundamente a engenharia do projeto, disponibilizamos um acervo de documentação de ponta a ponta na pasta [`Documents/6. Explain/`](Documents/6.%20Explain/):

| Documento | Descrição do Conteúdo |
|---|---|
| 📐 **[1 - Estrutura & Índice Arquitetural](Documents/6.%20Explain/1%20-%20ESTRUTURA_DOCUMENTACAO.md)** | Visão geral da Clean Architecture em 3 camadas, stack tecnológica, padrões e limites do sistema. |
| 🖥️ **[2 - Componentes do Frontend](Documents/6.%20Explain/2%20-%20DETALHAMENTO_COMPONENTES_FRONTEND.md)** | Detalhamento exaustivo de Views, ViewModels, Flyout, Tray, `AcrylicBackdropHelper`, Modais e controles customizados WinUI 3. |
| ⚙️ **[3 - Componentes do Backend](Documents/6.%20Explain/3%20-%20DETALHAMENTO_COMPONENTES_BACKEND.md)** | Especificação de serviços (`WingetService`, `UpdateService`), persistência EF Core SQLite WAL e repositórios. |
| 🔀 **[4 - O Trajeto dos Botões](Documents/6.%20Explain/4%20-%20MAPEAMENTO_INTERACOES_INTERFACE.md)** | Mapeamento passo a passo de cada ação da UI: gatilho, validação, rota/contrato, tabelas afetadas e retorno. |
| 📊 **[5 - Diagramas de Fluxo (Mermaid)](Documents/6.%20Explain/5%20-%20DIAGRAMAS_DE_FLUXO_MERMAID.md)** | Diagramas de sequência e fluxogramas visuais cobrindo atualização, UAC, cancelamento, snapshots e memória. |
| 🤖 **[6 - Diretrizes para IA (ai-context.md)](Documents/6.%20Explain/6%20-%20ai-context.md)** | Instruções e restrições de arquitetura para agentes e assistentes de Inteligência Artificial. |
| 🔄 **[7 - UML Comportamental & Fluxo](Documents/6.%20Explain/7%20-%20UML_COMPORTAMENTAL_E_FLUXO_SISTEMA.md)** | UML comportamental completo, ciclo de vida desde o boot no SO, rastreabilidade linha a linha de código e trimming de RAM. |
| 🤖 **[Diretrizes Canônicas para IA (.IArules)](.IArules)** | Conjunto de regras canônicas de engenharia para desenvolvimento orientado por IA. |

---

## 🏗️ Estrutura da Solução

```text
WingetEasy/
├── src/
│   ├── WingetEasy.App/             # WinUI 3, Windows App SDK, Fluent Design, Tray, Views, ViewModels, AcrylicBackdropHelper
│   ├── WingetEasy.Core/            # Regras de negócio, Winget CLI Parser, Task Scheduler, CLI Headless e Modelos
│   └── WingetEasy.Data/            # EF Core 10, SQLite WAL, Migrações e Repositórios de Dados com IDbContextFactory
├── tests/
│   └── WingetEasy.Core.Tests/      # Bateria de testes unitários com xUnit, Moq e FluentAssertions
├── Documents/
│   ├── 6. Explain/                 # Documentação técnica e arquitetural massiva de ponta a ponta (1 a 7)
│   ├── 7. Analise/                 # Relatórios de auditoria, memória e planos de execução históricos
│   └── .IArules                    # Diretrizes de IA espelhadas
├── .IArules                        # Diretrizes canônicas de engenharia para IA
└── WingetEasy.slnx                 # Solução C# moderna (.NET 10)
```

---

<div align="center">
  <sub>Construído com orgulho para o ecossistema Windows Open Source. Distribuído sob a licença GPL-3.0.</sub>
</div>
