# WingetEasy — Roadmap

> **Estado atual:** Issues #1–#39 implementadas com sucesso (Milestones 1 a 5 concluídos). Milestone 6 (Issues #40–#45 de distribuição e release) em fase de preparação.  
> **Meta:** App totalmente funcional, ultraleve, publicado na Microsoft Store e disponível via `winget install`.

---

## Milestone 1 — Core sem UI ✅ (Concluído)

| # | Issue | Status |
|---|---|---|
| #1 | Configurar solution C# (App, Core, Data) | ✅ |
| #2 | Gerenciamento centralizado de NuGet | ✅ |
| #3 | GitHub Actions — CI básico | ✅ |
| #4 | Modelos de domínio (Records imutáveis) | ✅ |
| #5 | Interfaces dos serviços | ✅ |
| #6 | WingetService — CheckUpdatesAsync + parsing JSON | ✅ |
| #7 | WingetService — InstallUpdateAsync com progresso | ✅ |
| #8 | WingetService — GetInstalledPackages + IsWingetAvailable | ✅ |
| #9 | AppDbContext + EF Core + SQLite | ✅ |
| #10 | Entidades do banco + migration InitialSchema | ✅ |
| #11 | Repositórios (Settings, UpdateHistory, Package) | ✅ |
| #12 | AdminElevationHelper — elevação UAC | ✅ |
| #13 | SchedulerService — PeriodicTimer | ✅ |
| #14 | UpdateService — orquestração batch | ✅ |
| #15 | Serilog — logging estruturado | ✅ |
| #16 | Circuit Breaker para falhas consecutivas | ✅ |
| #17 | Testes unitários — WingetService | ✅ |
| #18 | Testes unitários — Repositórios | ✅ |

---

## Milestone 2 — UI Base WinUI 3 ✅ (Concluído)

| # | Issue | Status |
|---|---|---|
| #19 | Projeto WinUI 3 + DI Container (App.xaml.cs) | ✅ |
| #20 | TrayIcon — ícone e menu contextual | ✅ |
| #21 | MainWindow — NavigationView + Mica/Acrylic | ✅ |
| #22 | UpdatesViewModel — listagem e seleção de atualizações | ✅ |
| #23 | UpdateFlyout — layout, posicionamento, animações | ✅ |
| #24 | PackageItemControl — controle por linha de pacote | ✅ |
| #25 | SettingsViewModel + SettingsPage completa | ✅ |

---

## Milestone 3 — Polimento e Robustez ✅ (Concluído)

### Prioridade crítica — integração real e feedback

| # | Issue | Descrição | Status |
|---|---|---|---|
| **#22-fix** | Conectar UpdatesViewModel aos serviços reais | Remoção de mocks; injeção de `IWingetService` e `IUpdateService` com checagem assíncrona real | ✅ |
| **#26** | Feedback visual de progresso durante atualização | Estados visuais no item de pacote (Aguardando/Atualizando/Concluído/Falha), `ProgressRing`, resumo e diálogo de status | ✅ |
| **#27** | Toast Notifications nativas do Windows | `ToastNotificationService` nativo do Windows App SDK com botões de ação e abertura do app | ✅ |
| **#29** | Integração UAC com fluxo de atualização | Tratamento de elevação administrativa, fallback para lotes UAC e captura de resultados | ✅ |

### Novas páginas e experiência do usuário

| # | Issue | Descrição | Status |
|---|---|---|---|
| **#30** | HistoryViewModel + HistoryPage | Timeline com agrupamento por data, filtros temporais, busca e persistência no SQLite | ✅ |
| **#28** | Detecção e troca de tema em tempo real | Monitoramento via `UISettings.ColorValuesChanged`, propagação via `DispatcherQueue` (Sistema/Claro/Escuro) | ✅ |
| **#31** | OnboardingDialog — primeira execução | Diálogo inicial guiado em 4 etapas, configuração de frequência de verificação e persistência de conclusão | ✅ |

---

## Milestone 4 — Funcionalidades Avançadas ✅ (Concluído)

| # | Issue | Descrição | Status |
|---|---|---|---|
| **#32** | BackupService — criar snapshot | Exportação nativa `winget export`, enriquecimento com metadados e persistência no SQLite | ✅ |
| **#33** | BackupService — restaurar snapshot | Restauração granular por pacote com feedback de progresso e tratamento de falhas | ✅ |
| **#34** | BackupPage — UI completa | Interface visual com cartões de snapshots, criação rápida, restauração e exclusão | ✅ |
| **#35** | ProfilesService + ProfilesPage | Gestão de perfis personalizados de pacotes com CRUD e aplicação em lote | ✅ |
| **#39** | Filtros e busca avançada na UpdatesPage/Flyout | Chips por categoria (Apps, Runtimes, etc.), busca em tempo real e ordenação dinâmica | ✅ |
| **#36** | Modo headless — argumentos CLI | Argumentos de linha de comando (`--check`, `--update-all`, `--minimized`, etc.) com códigos de saída padronizados | ✅ |
| **#37** | Auto-update via Velopack | Verificação e aplicação silenciosa de updates do próprio app com fallback seguro | ✅ |
| **#38** | Task Scheduler nativo do Windows | Execução de checagens em segundo plano integradas ao Agendador de Tarefas do Windows (`Microsoft.Win32.TaskScheduler`) | ✅ |

---

## Milestone 5 — Identidade Visual & Polimento Fluent ✅ (Concluído)

| Item | Descrição | Status |
|---|---|---|
| **Design Tokens** | Paleta semântica Fluent, corner radius padronizados e tipografia em `Styles/AppStyles.xaml` com `ThemeDictionaries` | ✅ |
| **Ícones e Assets** | Pacote completo de ícones (16 a 310px), variantes com badge de atualizações pendentes e splash screen | ✅ |
| **System Tray** | Ícone na bandeja dinâmico com tooltip descritivo do status ("Sistema atualizado" ou contagem de updates) | ✅ |
| **Acessibilidade & Atalhos** | Suporte a atalhos de teclado (F5, Ctrl+R, Ctrl+F) e atributos de `AutomationProperties` | ✅ |
| **Auto-Cura do Winget** | Recuperação automática de fontes corrompidas do Winget (`RepairSourcesAsync`) e cartão de manutenção nas configurações | ✅ |
| **Otimização de Memória** | Ciclos de vida curtos via `DbContextFactory`, desconexão de eventos, `EmptyWorkingSet` ao minimizar (7 a 70 MB) | ✅ |

---

## Milestone 6 — Distribuição 🚀 (Pendente / Em preparação)

| # | Issue | Descrição | Dependências |
|---|---|---|---|
| **#40** | Empacotamento MSIX + manifesto | Capacidades `internetClient` + `runFullTrust`, assets e manifesto final | #19, #25 |
| **#41** | Assinatura de código | Certificado digital para release, automação com `signtool` no pipeline | #40 |
| **#42** | GitHub Actions — workflow de release | Pipeline automatizado acionado por tag Git (`v*.*.*`) para build, teste, assinatura e publicação | #3, #40, #41 |
| **#43** | Manifesto YAML para `winget-pkgs` | Geração do manifesto via `wingetcreate` e submissão ao repositório oficial | #40, #41, #42 |
| **#44** | README profissional | Atualização contínua com badges, instruções de instalação e capturas de tela | #42 |
| **#45** | GitHub Pages — Documentação & Suporte | Políticas de privacidade e suporte em PT e EN | #44 |

---

## Otimizações Técnicas & Resoluções Arquiteturais

| Item | Descrição | Status |
|---|---|---|
| `TargetFramework` | Padronizado globalmente em `net10.0` no `Directory.Build.props` e projetos | ✅ |
| Migration `CheckLogs` | Tipo da coluna `DurationMs` corrigido para `long` no SQLite | ✅ |
| Contexto efêmero de banco | Adoção de `IDbContextFactory<AppDbContext>` evitando vazamento de memória e `ChangeTracker` acumulativo | ✅ |
| Redução da pegada de RAM | Imagens e ícones redimensionados, descarte de cache de navegação e minimização para a bandeja (~7MB–70MB) | ✅ |
| Regex Source Generators | Substituição de Regex em tempo de execução por geradores de código C# 14 / .NET 10 | ✅ |
| Persistência de coordenadas | Posição e dimensões da janela salvas e restauradas entre execuções | ✅ |
| Virtualização de UI | Listas otimizadas com virtualização XAML para rolagem suave mesmo com centenas de pacotes | ✅ |
| Tratamento de processos em uso | Detecção prévia de executáveis bloqueados e opção de fechar processo ou adiar pacote | ✅ |

---

## Métricas de conclusão

| Fase | Issues / Escopo | Status |
|---|---|---|
| Milestone 1 — Core | #1–#18 | ✅ 18/18 (100%) |
| Milestone 2 — UI Base | #19–#25 | ✅ 7/7 (100%) |
| Milestone 3 — Polimento | #22-fix, #26–#31 | ✅ 7/7 (100%) |
| Milestone 4 — Avançado | #32–#39 | ✅ 8/8 (100%) |
| Milestone 5 — Visual & Performance | Tokens, temas, acessibilidade e otimização de RAM | ✅ 1/1 (100%) |
| Milestone 6 — Distribuição | #40–#45 | ⏳ 0/6 (Em preparação) |
| **Total** | | **41/47** (~87%) |
