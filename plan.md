# CLI Conversion Plan for 2ND BRAIN

## Current State Analysis

**Current Features:**

- ✅ CLI framework implemented with Spectre.Console.Cli
- ✅ Dependency injection architecture integrated
- ✅ Configuration via appsettings.json with CLI settings
- ✅ Version command implemented
- ✅ Sync command implemented with file filtering
  - Only syncs files modified since last run
  - Progress bars and clear output
  - Dry-run support
  - StoredLastRun tracking
- ✅ Query command implemented
  - One-shot queries with LLM answers
  - Search results display with tables
  - Configurable result limits
  - Sources display option
  - No-LLM mode for search-only results
- ✅ Status command implemented
  - Knowledge base information
  - Sync information (last sync time, document folder)
  - Configuration summary
  - Formatted tables using Spectre.Console
- ✅ RAG Chat command implemented (renamed from 'chat' to 'ragchat')
  - Interactive chat session with knowledge base (RAG)
  - Conversation history support (in-memory)
  - Configurable context window
  - Visual status indicators (history enabled/disabled)
  - Styled prompt with Spectre.Console
  - Model selection via --model option or appsettings.json
- ✅ LLM command implemented
  - Direct LLM chat without RAG context
  - Conversation history support (in-memory)
  - Configurable context window
  - Visual status indicators (history enabled/disabled)
  - Model selection via --model option or appsettings.json (CLI.LlmModel)
  - Styled prompt with Spectre.Console
- ✅ Scheduled background jobs (Quartz)
- ✅ Service architecture with lazy loading support
- ✅ Model configuration in appsettings.json

**Current Limitations:**

- ⚠️ Document management commands partially implemented (delete, stats)
  - list: ✅ Implemented with tree structure
  - delete: Not yet implemented
  - stats: Not yet implemented

**Current Implementation Status:**

- ✅ **Phase 1: Foundation** - COMPLETED
  - Spectre.Console packages installed (v0.49.0)
  - Command structure created (`Commands/` folder)
  - BaseSettings class for global options
  - Program.cs refactored to use CommandApp with DI
  - TypeRegistrar and TypeResolver implemented for DI integration
  - Help system working (English locale)
  - Version command implemented with ASCII art banner
  - Configuration integration (reads from AppSettings.CLI)
- ✅ **Phase 2: Core Commands** - COMPLETED
  - `sync` command - ✅ IMPLEMENTED
    - Filters files by last modified time (only syncs files added/modified since last run)
    - Supports --folder, --force, --dry-run options
    - Progress bar with Spectre.Console
    - Clear output messages (shows count or "up to date" message)
    - Updates StoredLastRun after successful sync
- ✅ **Phase 3: Additional Commands** - COMPLETED
  - `query` command - ✅ IMPLEMENTED
    - One-shot queries with LLM answer generation
    - Search results display with formatted tables
    - Supports --limit, --sources, --no-llm options
    - Uses Spectre.Console for beautiful output
  - `status` command - ✅ IMPLEMENTED
    - Knowledge base information (index name, accessibility)
    - Sync information (last sync time, document folder)
    - Configuration summary (RAG and CLI settings)
    - Formatted tables using Spectre.Console
  - `chat` command - ✅ IMPLEMENTED
    - Interactive chat session with knowledge base
    - Conversation history support (in-memory, configurable context)
    - Visual status indicators (history enabled/disabled in prompt)
    - Styled prompt using Spectre.Console markup
    - Supports --history and --context options
- ⏳ **Phase 4-6: Advanced Commands** - NOT STARTED

---

## Proposed CLI Structure

### Core Commands

```
2b [command] [options]

Commands:
  version       Show version information ✅ (IMPLEMENTED)
  sync          Sync documents from folder to knowledge base ✅ (IMPLEMENTED)
  query         Query the knowledge base (one-shot) ✅ (IMPLEMENTED)
  status        Check system status and health ✅ (IMPLEMENTED)
  ragchat       Start interactive chat session with RAG ✅ (IMPLEMENTED - renamed from 'chat')
  llm           Start interactive chat session with direct LLM ✅ (IMPLEMENTED)
  config        Manage configuration ✅ (IMPLEMENTED)
  list          List documents in knowledge base ✅ (IMPLEMENTED)
  tree          Display latest RAG sources and chunks as tree ✅ (IMPLEMENTED)
  delete        Delete documents from knowledge base ⏳ (PENDING)
  stats         Show knowledge base statistics ⏳ (PENDING)
  help          Show help information (built-in) ✅ (BUILT-IN)

Note: 'serve' command will be implemented in a future phase
```

### Detailed Command Specifications

#### 0. `version` Command ✅ IMPLEMENTED

```bash
2b version [options]
2b v [options]  # Alias

Description:
  Displays version information including:
  - ASCII art banner
  - Application version (from AppSettings.CLI.ApplicationVersion)
  - Author (from AppSettings.CLI.Author)
  - RAG system information (embedding model, LLM model)

Examples:
  2b version
  2b v
```

#### 1. `sync` Command ✅ IMPLEMENTED

```bash
2b sync [options]

Options:
  --folder <path>     Override document folder path
  --force             Force re-embedding of all documents (ignores last run time)
  --dry-run           Show what would be synced without doing it

Description:
  Syncs documents from the configured folder (or --folder override) to the knowledge base.
  By default, only syncs files that have been added or modified since the last sync.
  Uses StoredLastRun file to track last sync time.

Features:
  - Filters files by LastWriteTimeUtc (only files modified since last run)
  - Shows progress bar during sync (Spectre.Console)
  - Clear success/status messages
  - Updates StoredLastRun after successful sync
  - Skips progress bar when no files to sync

Examples:
  2b sync                                    # Sync files modified since last run
  2b sync --folder "C:\MyDocs"              # Sync from specific folder
  2b sync --force                           # Force sync all files
  2b sync --dry-run                         # Preview what would be synced
```

#### 2. `query` Command ✅ IMPLEMENTED

```bash
2b query <question> [options]

Options:
  --limit <n>         Number of results to return (default: 5)
  --sources           Include source document information
  --no-llm            Only return search results, don't generate answer

Description:
  Queries the knowledge base with a question and returns an LLM-generated answer.
  Can also return only search results without LLM processing.

Features:
  - One-shot queries (no interactive mode required)
  - LLM answer generation using configured model
  - Search results displayed in formatted tables
  - Source document information available
  - Beautiful output using Spectre.Console panels and tables
  - Animated spinners during search and answer generation (matching ragchat experience)
  - Stores results for display with `tree` command

Examples:
  2b query "What is the main topic?"
  2b query "Explain X" --limit 10
  2b query "Find Y" --no-llm --sources
```

#### 3. `ragchat` Command ✅ IMPLEMENTED (renamed from 'chat')

```bash
2b ragchat [options]

Options:
  --history           Enable conversation history to maintain context across messages
  --context <n>       Number of previous messages to include in context (default: 5)
  --model <name>      LLM model to use (overrides appsettings.json RAG.TextModel.Model)

Description:
  Starts an interactive chat session with the knowledge base. Each question is processed
  using RAG (Retrieval Augmented Generation) to provide context-aware answers.

Features:
  - Interactive chat loop (type 'exit' to quit)
  - Conversation history support (in-memory, lost when session ends)
  - Configurable context window for history
  - Visual status indicators showing history state
  - Styled prompt using Spectre.Console markup (shows model name)
  - Context-aware responses when history is enabled
  - Model selection via --model option or appsettings.json

Examples:
  2b ragchat                                  # Chat without history (uses RAG.TextModel.Model)
  2b ragchat --history                        # Chat with history (default: 5 messages)
  2b ragchat --history --context 10          # Chat with history (10 messages context)
  2b ragchat --model "llama3:8b"             # Use specific model
  2b ragchat --history --model "gemma3:4b"  # Chat with history and custom model
```

#### 4. `llm` Command ✅ IMPLEMENTED

```bash
2b llm [options]

Options:
  --history           Enable conversation history to maintain context across messages
  --context <n>       Number of previous messages to include in context (default: 5)
  --model <name>      LLM model to use (overrides appsettings.json CLI.LlmModel)

Description:
  Starts an interactive chat session with a direct LLM (without RAG context).
  This command bypasses the knowledge base and sends user input directly to the LLM.

Features:
  - Interactive chat loop (type 'exit' to quit)
  - Direct LLM interaction (no RAG/search)
  - Conversation history support (in-memory, lost when session ends)
  - Configurable context window for history
  - Visual status indicators showing history state
  - Styled prompt using Spectre.Console markup (shows model name)
  - Context-aware responses when history is enabled
  - Model selection via --model option or appsettings.json (CLI.LlmModel, default: gemma3:4b)

Examples:
  2b llm                                    # Direct LLM chat (uses CLI.LlmModel from config)
  2b llm --history                          # LLM chat with history (default: 5 messages)
  2b llm --history --context 10            # LLM chat with history (10 messages context)
  2b llm --model "llama3:8b"               # Use specific model
  2b llm --history --model "gemma3:4b"    # LLM chat with history and custom model
```

#### 5. `status` Command ✅ IMPLEMENTED

```bash
2b status [options]

Output:
  - Knowledge base information (index name, accessibility status)
  - Sync information (last sync time, sync state file, document folder)
  - Configuration summary (RAG settings, CLI settings)

Note: Output uses Spectre.Console tables for beautiful, formatted display

Examples:
  2b status              # Show full status information
```

#### 6. `config` Command ✅ IMPLEMENTED

```bash
2b config [options]

Description:
  Interactive configuration management with menu-driven interface.
  Allows editing RAG settings, CLI settings, and chunking settings.

Features:
  - Interactive menu with categories
  - View current configuration
  - Edit RAG settings (URLs, models, paths, etc.)
  - Edit CLI settings (application name, version, author, model)
  - Edit chunking settings (tokens, overlap, sentence requirements)
  - Saves changes to source appsettings.json file
  - Atomic file writes for safety

Examples:
  2b config                    # Interactive configuration menu
```

#### 7. `list` Command ✅ IMPLEMENTED

```bash
2b list [options]

Options:
  --folder <path>     Override document folder path

Description:
  Lists all documents from the configured folder and shows their sync status.
  Displays a hierarchical tree structure with statistics, file types, and documents.

Features:
  - Tree structure with hierarchical organization
  - Statistics node showing total documents, size, and sync status
  - File types breakdown with color-coded counts
  - Documents grouped by file type with individual file details
  - Each document shows file name, size, and sync status
  - Sync status determined by timestamp comparison (same logic as sync command)
  - Uses last sync time from SyncState to determine if files are synced
  - Fast and reliable - no knowledge base queries needed
  - Color-coded file types (PDF=red, TXT=blue, MD=green, DOCX=yellow)
  - Documents sorted by size within each file type group

Examples:
  2b list                    # List all documents from configured folder
  2b list --folder "C:\Docs" # List from specific folder
```

#### 8. `tree` Command ✅ IMPLEMENTED

```bash
2b tree [options]

Description:
  Displays the latest RAG sources and chunks from the most recent query or ragchat command
  as a hierarchical tree structure. Shows which documents and chunks were used to generate
  the last answer.

Features:
  - Tree structure showing sources and chunks
  - Documents as parent nodes (color-coded in yellow)
  - Chunks as child nodes with relevance scores
  - Color-coded chunks by relevance (green ≥0.7, yellow ≥0.5, red <0.5)
  - Chunk previews (first 80 characters)
  - Shows message if no results available (run query or ragchat first)

Examples:
  2b query "What is X?"      # Run a query first
  2b tree                     # Display the sources and chunks used
  2b ragchat                  # Or use ragchat
  2b tree                     # Display the latest sources and chunks
```

#### 9. `delete` Command

```bash
2b delete <document-id> [options]

Options:
  --force             Don't prompt for confirmation
  --all               Delete all documents (dangerous!)

Note: When confirmation is needed, will use Spectre.Console's prompt API for interactive confirmation

Examples:
  2b delete "The_Pragmatic_Programmer"
  2b delete --all --force
```

#### 10. `stats` Command

```bash
2b stats [options]

Options:
  --detailed          Include per-document statistics

Output:
  - Total documents
  - Total embeddings/chunks
  - Storage size
  - Average document size
  - Index health

Note: Output will use Spectre.Console tables for formatted statistics display

Examples:
  2b stats
  2b stats --detailed
```

#### 11. `help` Command

```bash
2b help [command]

Description:
  Shows help information for the CLI or a specific command.
  This is also available via the --help flag on any command.

Options:
  [command]          Show help for a specific command

Examples:
  2b help              # Show general help and list all commands
  2b help sync         # Show detailed help for sync command
  2b help query        # Show detailed help for query command
  2b sync --help       # Alternative: show help using --help flag
  2b --help            # Show general help (same as '2b help')
```

**Note:** Spectre.Console provides built-in help support. The `help` command provides an explicit way to access help, but `--help` and `-h` flags work on all commands automatically.

#### 12. `serve` Command (Future Phase)

```bash
2b serve [options]

Options:
  --no-scheduler      Don't start background scheduler
  --port <n>          If adding HTTP API later

Description:
  Runs as a background service with Quartz scheduler active.
  Similar to current behavior but can be controlled better.

Note: This command will be implemented in a future phase, not in initial implementation.

Examples:
  2b serve
  2b serve --no-scheduler
```

---

## Benefits of CLI Conversion

### 1. **Flexibility & Control**

- ✅ Run specific operations without starting full app
- ✅ One-shot queries for scripting/automation
- ✅ Better control over when sync happens
- ✅ Can be integrated into workflows and pipelines

### 2. **Scriptability & Automation**

- ✅ Can be called from batch files, PowerShell scripts
- ✅ Easy integration with task schedulers (Windows Task Scheduler, cron)
- ✅ Can be used in CI/CD pipelines
- ✅ Enables automation scenarios:
  ```powershell
  # Example: Auto-sync on file changes
  2b sync --folder $folder
  2b query "What changed?" > report.txt
  ```

### 3. **Better User Experience**

- ✅ Clear command structure with help text
- ✅ Built-in help command and --help flags on all commands
- ✅ Rich console UI with tables, panels, and formatted output (with Spectre.Console)
- ✅ Better error messages and validation
- ✅ Beautiful progress indicators for long operations
- ✅ Human-readable output format with colors and styling

### 4. **Development & Debugging**

- ✅ Easier to test individual features
- ✅ Better separation of concerns
- ✅ Can add commands incrementally
- ✅ Easier to add new features as commands

### 5. **Production Readiness**

- ✅ Health checks via `status` command
- ✅ Better monitoring capabilities
- ✅ Can be containerized more easily
- ✅ Supports both interactive and non-interactive modes
- ✅ Service mode (`serve` command) planned for future phase

### 6. **Integration Opportunities**

- ✅ Can be called from other applications
- ✅ Can be wrapped in REST API later
- ✅ Can be used in webhooks
- ✅ Better for headless environments

### 7. **Configuration Management**

- ✅ CLI-based config management
- ✅ Environment variable support
- ✅ Multiple config file support
- ✅ Runtime config overrides

---

## Technical Implementation Plan

### Phase 1: Foundation ✅ COMPLETED

1. **✅ Add Spectre.Console NuGet package**

   ```xml
   <PackageReference Include="Spectre.Console" Version="0.49.0" />
   <PackageReference Include="Spectre.Console.Cli" Version="0.49.0" />
   ```

   - Packages installed and working

2. **✅ Create Command Structure**

   - ✅ `Commands/` folder created
   - ✅ BaseSettings class implemented (inherits from `CommandSettings`)
   - ✅ Base command handler pattern established (inherit from `Command<TSettings>`)
   - ✅ Dependency injection fully integrated via TypeRegistrar/TypeResolver
   - ✅ CommandApp routing configured in ServiceCollectionExtensions.AddCommandApp()

3. **✅ Refactor Program.cs**

   - ✅ Program.cs refactored to use CommandApp with DI
   - ✅ Configuration loading from appsettings.json
   - ✅ Service collection setup with AddRAGKnowledgeBase() and AddCommandApp()
   - ✅ Global options defined in BaseSettings (--config)
   - ✅ Help system working (English locale configured)
   - ✅ Error handling implemented
   - ⚠️ Chat logic still needs to be moved to `chat` command (Phase 3)

4. **✅ Version Command Implemented**
   - ✅ VersionCommand created with DI support
   - ✅ Reads configuration from AppSettings.CLI (version, author)
   - ✅ Reads RAG settings for display (embedding model, LLM model)
   - ✅ ASCII art banner preserved
   - ✅ Registered in DI container and CommandApp

### Phase 2: Core Commands ✅ COMPLETED

1. **✅ Implement `sync` command** (Priority 1) - COMPLETED
   - ✅ SyncCommand created with SyncSettings
   - ✅ Command options implemented (--folder, --force, --dry-run)
   - ✅ Integrated with DocumentEmbeddingService via DI
   - ✅ Progress reporting using Spectre.Console Progress API
   - ✅ Registered in ServiceCollectionExtensions.AddCommandApp()
   - ✅ File filtering by last modified time (only syncs files since last run)
   - ✅ StoredLastRun integration (reads/writes last sync time)
   - ✅ Clear output messages (shows count or "up to date" status)
   - ✅ Dry-run mode with table showing files that would be synced
   - ✅ Skips progress bar when no files to sync

**Note:** `query` and `status` commands will be implemented in subsequent phases.

### Phase 3: Additional Commands ✅ COMPLETED

1. **✅ Implement `query` command** (Priority 1) - COMPLETED

   - ✅ Extracted RagChatService query logic
   - ✅ Support one-shot queries
   - ✅ Human-readable output with Spectre.Console
   - ✅ Configurable options (--limit, --sources, --no-llm)
   - ✅ Formatted tables and panels for output
   - ✅ Animated spinners during search and answer generation (matching ragchat)
   - ✅ Stores results in RagResultService for tree command
   - ✅ Registered in DI and CommandApp

2. **✅ Implement `status` command** (Priority 2) - COMPLETED

   - ✅ Knowledge base information display
   - ✅ Sync information display
   - ✅ Configuration summary
   - ✅ Spectre.Console tables for formatted output
   - ✅ Registered in DI and CommandApp

3. **✅ Implement `ragchat` command** (Priority 3) - COMPLETED

   - ✅ Moved chat loop logic from Program.cs to RagChatCommand
   - ✅ Conversation history support (in-memory)
   - ✅ Configurable context window (--context option)
   - ✅ Visual status indicators (startup message and prompt)
   - ✅ Styled prompt using Spectre.Console markup (shows model name)
   - ✅ Model selection via --model option or appsettings.json
   - ✅ ConversationMessage model for history tracking
   - ✅ Enhanced PromptTemplate to include conversation history
   - ✅ Renamed ChatService to RagChatService for clarity
   - ✅ Renamed ChatCommand to RagChatCommand and ChatSettings to RagChatSettings
   - ✅ Command renamed from 'chat' to 'ragchat' for clarity
   - ✅ Registered in DI and CommandApp

4. **✅ Implement `llm` command** (Priority 4) - COMPLETED
   - ✅ Direct LLM chat without RAG context
   - ✅ Conversation history support (in-memory)
   - ✅ Configurable context window (--context option)
   - ✅ Visual status indicators (startup message and prompt)
   - ✅ Styled prompt using Spectre.Console markup (shows model name)
   - ✅ Model selection via --model option or appsettings.json (CLI.LlmModel)
   - ✅ LlmChatService created for direct LLM interactions
   - ✅ OllamaService extended to support custom model parameter
   - ✅ Registered in DI and CommandApp

### Phase 4: Advanced Commands (IN PROGRESS)

1. **✅ Implement `config` command** - COMPLETED

   - ✅ Interactive configuration management
   - ✅ Menu-driven interface
   - ✅ Saves to source appsettings.json
   - ✅ Atomic file writes

2. **✅ Implement `list` command** - COMPLETED

   - ✅ List all documents from folder
   - ✅ Show sync status (synced/not synced)
   - ✅ Tree structure with hierarchical organization
   - ✅ Statistics node (total documents, size, sync counts)
   - ✅ File types breakdown with color-coded counts
   - ✅ Documents grouped by file type with individual details
   - ✅ Sync status checking using timestamp comparison (same logic as sync command)
   - ✅ Uses SyncState to get last sync time
   - ✅ Compares file LastWriteTimeUtc/CreationTimeUtc to last sync time
   - ✅ Much faster and more reliable than knowledge base status checks
   - ✅ Color-coded file types (PDF=red, TXT=blue, MD=green, DOCX=yellow)
   - ✅ Documents sorted by size within each file type group
   - ✅ Registered in DI and CommandApp

3. **✅ Implement `tree` command** - COMPLETED

   - ✅ Display latest RAG sources and chunks as tree structure
   - ✅ RagResultService to store latest search results
   - ✅ QueryCommand and RagChatService store results after each query
   - ✅ Tree visualization with documents and chunks
   - ✅ Color-coded chunks by relevance score
   - ✅ Chunk previews and relevance scores
   - ✅ Shows helpful message if no results available
   - ✅ Registered in DI and CommandApp

4. **⏳ Implement `delete` command** - PENDING

   - Document deletion from knowledge base
   - Safety confirmations
   - Batch deletion support

5. **⏳ Implement `stats` command** - PENDING

   - Knowledge base analytics
   - Performance metrics
   - Document statistics

6. **✅ Scoop Distribution Setup** - COMPLETED
   - ✅ Scoop manifest created (`scoop/2b.json`)
   - ✅ Build and release script (`scoop/build-release.ps1`)
   - ✅ Release documentation (`RELEASE.md`)
   - ✅ Local and GitHub bucket setup instructions
   - ✅ Installation via `scoop install 2b`

### Phase 5: Polish & Cleanup ✅ COMPLETED

1. **✅ Remove all icons/emojis** - COMPLETED

   - ✅ Removed checkmarks (✓) and crosses (✗) from command output
   - ✅ Replaced filled/empty circles (●, ○) with "H : on" and "H : off"
   - ✅ Replaced info (ℹ) with "info" and warning (⚠) with "warning"
   - ✅ Replaced all icons with text-based indicators
   - ✅ Commands updated: ListCommand, SyncCommand, ConfigCommand, StatusCommand, RagChatCommand, LlmCommand, DocumentEmbeddingService

2. **✅ Fix tree command** - COMPLETED

   - ✅ Root cause: Each CLI command runs in a separate process, so singleton service doesn't persist
   - ✅ Solution: Modified RagResultService to persist results to file (`rag-results.json`)
   - ✅ Updated TreeCommand to use file-based storage instead of in-memory singleton
   - ✅ Results now persist across separate command invocations
   - ✅ Improved error handling and null checks

3. **✅ Fix list command sync status** - COMPLETED
   - ✅ Root cause: Knowledge base status checks were unreliable and slow
   - ✅ Solution: Refactored to use same timestamp-based logic as sync command
   - ✅ Uses SyncState to get last sync time
   - ✅ Compares file timestamps (LastWriteTimeUtc/CreationTimeUtc) to last sync time
   - ✅ Much faster (no async knowledge base queries)
   - ✅ More reliable (consistent with sync command logic)
   - ✅ Status updates immediately after sync operations

### Phase 6: Service Mode (Future)

1. **Implement `serve` command** (Deferred)
   - Background service mode
   - Scheduler integration
   - Graceful shutdown
   - Note: Code structure will support this, but implementation deferred

### Phase 7: Additional Polish

1. **Enhance Help System**

   - Comprehensive help text for each command
   - Examples in help output
   - Error message improvements
   - Rich formatted help output using Spectre.Console panels and tables
   - Note: Basic help is available via Spectre.Console.Cli in Phase 1

2. **Add Tab Completion**

   - Spectre.Console.Cli completion support
   - Shell integration (may require additional setup)

3. **Testing & Documentation**
   - Unit tests for commands
   - Integration tests
   - User documentation
   - Leverage Spectre.Console's IAnsiConsole for testable console output

---

## Architecture Changes

### Previous Architecture (Before CLI)

```
Program.cs
  └─> Initialize Services
      └─> Start Chat Loop
```

### Current Architecture ✅ IMPLEMENTED

```
Program.cs
  └─> Build Configuration (appsettings.json)
      └─> Build ServiceCollection with DI
          ├─> AddRAGKnowledgeBase(config)
          └─> AddCommandApp(config)
              └─> Resolve CommandApp from DI
                  └─> Run CommandApp with args

ServiceCollectionExtensions:
  └─> AddCommandApp()
      ├─> Register commands in DI (VersionCommand)
      └─> Configure CommandApp
          ├─> Set application name/version from AppSettings.CLI
          ├─> Register commands with TypeRegistrar (DI support)
          └─> TypeResolver handles DI resolution
              ├─> Commands resolved from DI container
              └─> CommandSettings resolved by Spectre.Console

Commands/:
  ├─> BaseSettings.cs (CommandSettings with global options)
  ├─> VersionCommand.cs ✅ (uses IOptions<AppSettings>)
  ├─> SyncSettings.cs ✅ (extends BaseSettings with sync-specific options)
  ├─> SyncCommand.cs ✅ (uses DocumentEmbeddingService, SyncState, IKernelMemory)
  ├─> QuerySettings.cs ✅ (extends BaseSettings with query-specific options)
  ├─> QueryCommand.cs ✅ (uses IKernelMemory, OllamaService, IOptions<AppSettings>)
  ├─> StatusSettings.cs ✅ (extends BaseSettings)
  ├─> StatusCommand.cs ✅ (uses IKernelMemory, SyncState, IOptions<AppSettings>)
  ├─> RagChatSettings.cs ✅ (extends BaseSettings with --history, --context, --model options)
  ├─> RagChatCommand.cs ✅ (uses IKernelMemory, RagChatService, IOptions<AppSettings>)
  ├─> LlmSettings.cs ✅ (extends BaseSettings with --history, --context, --model options)
  └─> LlmCommand.cs ✅ (uses LlmChatService, IOptions<AppSettings>)

Services/:
  ├─> RagChatService.cs ✅ (RAG-based chat with knowledge base)
  └─> LlmChatService.cs ✅ (direct LLM chat without RAG)

Models/:
  └─> ConversationMessage.cs ✅ (conversation history model)
```

### Target Architecture (Future)

```
Program.cs
  └─> Spectre.Console.Cli CommandApp
      ├─> VersionCommand ✅ (Phase 1 - COMPLETED)
      ├─> SyncCommand ✅ (Phase 2 - COMPLETED)
      ├─> QueryCommand ✅ (Phase 3 - COMPLETED)
      ├─> StatusCommand ✅ (Phase 3 - COMPLETED)
      ├─> RagChatCommand ✅ (Phase 3 - COMPLETED - renamed from ChatCommand)
      ├─> LlmCommand ✅ (Phase 3 - COMPLETED)
      ├─> ConfigCommand (Phase 4)
      ├─> ListCommand (Phase 4)
      ├─> DeleteCommand (Phase 4)
      ├─> StatsCommand (Phase 4)
      ├─> HelpCommand (Built-in via Spectre.Console.Cli)
      └─> ServeCommand (Phase 5 - Future)

Each Command:
  └─> Resolved from DI container
      └─> Services available via constructor injection
      └─> Execute Command Logic
      └─> Use Spectre.Console for rich output (tables, panels, progress)
```

### Service Initialization Strategy

- **Lazy Loading**: Only initialize services needed for specific command (CONFIRMED)
- **Shared Services**: Common services (logging, config) initialized once
- **Command-Specific**: Memory, chat service only when needed
- **Implementation**: Use Spectre.Console.Cli with dependency injection for lazy service resolution
- **UI Components**: Leverage Spectre.Console's IAnsiConsole for all output (tables, panels, progress bars, prompts)

---

## Example Usage Scenarios

### Scenario 1: Manual Document Sync

```bash
# Current: Must start app, wait for initialization, manually trigger sync

# CLI: Direct sync
2b sync                    # Sync documents immediately
2b sync --folder "C:\MyDocs"  # Sync specific folder
```

### Scenario 2: Scheduled Sync

```bash
# Current: Need to run full app or rely on Quartz scheduler

# CLI: Can be scheduled directly
# Windows Task Scheduler:
2b sync

# PowerShell scheduled task:
Register-ScheduledTask -Action (New-ScheduledTaskAction -Execute "2b.exe" -Argument "sync")
```

### Scenario 3: Quick Query (Future Phase)

```bash
# CLI: Direct query
2b query "What is X?"  # Instant result
```

### Scenario 4: Health Monitoring (Future Phase)

```bash
# Check system health
2b status --check-services

# If unhealthy, alert or restart
if [ $? -ne 0 ]; then
    # Alert logic
fi
```

### Scenario 5: Batch Operations

```bash
# Sync multiple folders
for folder in folder1 folder2 folder3; do
    2b sync --folder "$folder"
done
```

---

## Migration Path

### Backward Compatibility

- All functionality via explicit commands
- `2b ragchat` → RAG-based chat with knowledge base
- `2b llm` → Direct LLM chat without RAG
- All new functionality via commands

### Configuration

- Support both appsettings.json and CLI arguments
- CLI args override config file
- Environment variables override both

---

## Future Enhancements (Post-CLI)

1. **HTTP API Mode**

   - `2b api --port 8080`
   - REST endpoints for all commands
   - WebSocket for chat

2. **Plugin System**

   - Custom commands via plugins
   - Extensibility

3. **Export/Import**

   - `2b export`
   - `2b import <file>`

4. **Multi-Index Support**
   - `2b index create <name>`
   - `2b index switch <name>`

---

## Decision Points

### 1. CLI Library Choice

- **Spectre.Console** (CONFIRMED)
  - ✅ Rich console UI capabilities (tables, panels, progress bars, prompts)
  - ✅ Built-in CLI framework via Spectre.Console.Cli
  - ✅ Excellent documentation and examples
  - ✅ Active development and community
  - ✅ Beautiful, modern console output
  - ✅ Built-in help system
  - ✅ Supports dependency injection

### 2. Service Initialization

- **Lazy initialization per command** (CONFIRMED)
  - ✅ Faster startup
  - ✅ Lower memory usage
  - ✅ Only load services needed for specific command
  - Implementation: Use dependency injection with lazy resolution

### 3. Default Behavior

- **Option A**: `2b` → chat (backward compatible) - CONFIRMED
- Keeps existing workflow intact while adding new CLI capabilities

### 4. Output Format

- **Human-readable text only** (CONFIRMED)
  - No JSON output options
  - Clean, readable console output
  - Focus on user-friendly display

---

## Success Metrics

1. **Performance**

   - Query command: < 2s startup time
   - Sync command: Same performance as current
   - Status command: < 1s execution

2. **Usability**

   - All commands have help text
   - Clear error messages
   - Tab completion works

3. **Compatibility**
   - Existing workflows still work
   - Configuration migration smooth
   - No breaking changes to services

---

## Decisions Made

1. **Command Name**: `2b` instead of `secondbrain` - ✅ CONFIRMED & IMPLEMENTED

   - Shorter, easier to type
   - Matches "2ND BRAIN" branding
   - Configured in AppSettings.CLI.ApplicationName

2. **Output Format**: Human-readable text only - ✅ CONFIRMED & IMPLEMENTED

   - No JSON output options
   - Focus on clean console output
   - Spectre.Console formatting used in VersionCommand

3. **Service Initialization**: Lazy loading - ✅ CONFIRMED & IMPLEMENTED

   - Only initialize services needed per command
   - Faster startup times
   - DI architecture supports lazy resolution via TypeResolver

4. **Dependency Injection**: ✅ FULLY INTEGRATED

   - TypeRegistrar and TypeResolver implemented
   - Commands registered in DI container
   - Services available via constructor injection
   - CommandSettings handled by Spectre.Console (not DI)

5. **Configuration Integration**: ✅ IMPLEMENTED

   - CLI settings in AppSettings.CLI (ApplicationName, ApplicationVersion, Author)
   - Commands read from IOptions<AppSettings>
   - VersionCommand demonstrates pattern

6. **Phase 2 Implementation**: `sync` command - ✅ COMPLETED

   - Foundation complete (Phase 1)
   - Sync command fully implemented (Phase 2)
   - File filtering by last modified time working
   - Progress bars and clear output messages implemented
   - Other commands in subsequent phases (Phase 3+)

7. **Serve Command**: Deferred to future phase - CONFIRMED
   - Code structure will support it
   - Not implementing in initial phases

## Questions to Consider

1. **How to handle long-running operations?**

   - Progress bars? (Use Spectre.Console's Progress API - integrates well with existing DocumentEmbeddingService)
   - Cancellation support (Ctrl+C)? (Spectre.Console.Cli supports cancellation tokens)

2. **Error handling:**
   - Exit codes for scripting?
   - Structured error output?

---

## Recommendation

**Proceed with CLI conversion** - The benefits significantly outweigh the effort:

- Better user experience
- More flexible usage
- Easier to maintain and extend
- Production-ready architecture
- Enables future enhancements

**Implementation Status:**

- ✅ **Phase 1: Foundation** - COMPLETED

  - Spectre.Console library integrated (Spectre.Console.Cli for CLI framework)
  - Dependency injection fully implemented
  - Lazy service initialization architecture in place
  - `2b` command name configured in AppSettings
  - Human-readable output with rich formatting via Spectre.Console
  - Version command demonstrates the pattern
  - Help system working (English locale)

- ✅ **Phase 2: Sync Command** - COMPLETED

  - ✅ `sync` command fully implemented and working
  - ✅ Leverages Spectre.Console's progress bars for sync operations
  - ✅ Uses DocumentEmbeddingService via DI
  - ✅ File filtering by last modified time (only syncs new/modified files)
  - ✅ StoredLastRun integration for tracking sync state
  - ✅ Clear output messages and dry-run support

- ✅ **Phase 3: Additional Commands** - COMPLETED

  - ✅ `query` command implemented for one-shot queries
  - ✅ `status` command implemented for system status
  - ✅ `ragchat` command implemented with conversation history support and model selection (renamed from 'chat')
  - ✅ `llm` command implemented for direct LLM chat without RAG
  - ✅ Model configuration added to appsettings.json (CLI.LlmModel)
  - ✅ ChatService renamed to RagChatService for clarity
  - ✅ ChatCommand renamed to RagChatCommand and ChatSettings to RagChatSettings
  - ✅ OllamaService extended to support custom model parameter
  - ✅ Consistent visual styling applied across all commands (matching StatusCommand style)
  - ✅ HTTP request logging suppressed for cleaner output

- ✅ **Phase 4: Advanced Commands** - COMPLETED

  - ✅ `config` command implemented with interactive menu
  - ✅ `list` command implemented with tree structure (statistics, file types, documents)
  - ✅ `tree` command implemented to display latest RAG sources and chunks
  - ✅ `RagResultService` created to store latest search results
  - ✅ `QueryCommand` enhanced with spinner/status indicators matching ragchat
  - ✅ Scoop manifest created for easy installation
  - ✅ Release process documented (RELEASE.md)
  - ⏳ `delete` command pending
  - ⏳ `stats` command pending
  - ⏳ `serve` command deferred to future phase

- ✅ **Phase 5: Polish & Cleanup** - COMPLETED
  - ✅ Removed all icons/emojis from command output
  - ✅ Fixed tree command (file-based persistence for cross-process results)
  - ✅ Fixed list command sync status (timestamp-based logic matching sync command)
  - ✅ Removed --quiet and --verbose options from all commands (simplified codebase, consistent output)

**Key Achievements:**

- ✅ Full DI integration with Spectre.Console.Cli
- ✅ Configuration-driven CLI settings
- ✅ Type-safe command structure
- ✅ Version command implemented
- ✅ Sync command fully implemented with file filtering
- ✅ Query command implemented with LLM integration
- ✅ Status command implemented with system information
- ✅ RAG Chat command implemented with conversation history and model selection (renamed to 'ragchat')
- ✅ LLM command implemented for direct LLM chat without RAG
- ✅ Conversation history feature (in-memory, configurable context)
- ✅ Visual status indicators for chat history
- ✅ Model configuration in appsettings.json (CLI.LlmModel)
- ✅ Model selection via --model option for both ragchat and llm commands
- ✅ Service architecture: RagChatService (RAG) and LlmChatService (direct LLM)
- ✅ OllamaService extended to support custom model parameter
- ✅ Progress bars and rich console output
- ✅ Formatted tables and panels using Spectre.Console
- ✅ Styled prompts and status indicators (showing model names)
- ✅ Animated spinners in QueryCommand matching ragchat experience
- ✅ Tree command for visualizing RAG sources and chunks
- ✅ RagResultService for storing latest search results
- ✅ Scoop manifest and release process setup
- ✅ Installation via package manager (Scoop)
- ✅ Cleanup phase completed (removed emojis, fixed tree command, fixed list command)
- ✅ Removed --quiet and --verbose options (simplified codebase, ~50 lines of conditional code removed)
- ⏳ Ready for remaining commands (delete, stats)
