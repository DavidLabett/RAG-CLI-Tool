# Environment Variables

This document describes the environment variables used by the RAG CLI Tool.

## CloudFlare Workers AI Configuration

When using online mode with CloudFlare Workers AI, you can set these environment variables instead of storing credentials in `appsettings.json`:

### Required Variables

- `AppSettings__RAG__CloudFlare__ApiToken` - Your CloudFlare API token
- `AppSettings__RAG__CloudFlare__AccountId` - Your CloudFlare account ID

### Optional Variables

- `AppSettings__RAG__CloudFlare__EmbeddingModel` - Embedding model (default: `@cf/baai/bge-base-en-v1.5`)
- `AppSettings__RAG__CloudFlare__GenerationModel` - Generation model (default: `@cf/meta/llama-3-8b-instruct`)
- `AppSettings__RAG__CloudFlare__ReRankingModel` - Re-ranking model (for future use)

### Setting Environment Variables

#### Windows PowerShell (User-level, persistent):
```powershell
[System.Environment]::SetEnvironmentVariable('AppSettings__RAG__CloudFlare__ApiToken', 'your-api-token-here', 'User')
[System.Environment]::SetEnvironmentVariable('AppSettings__RAG__CloudFlare__AccountId', 'your-account-id-here', 'User')
```

#### Windows PowerShell (Session-only):
```powershell
$env:AppSettings__RAG__CloudFlare__ApiToken = 'your-api-token-here'
$env:AppSettings__RAG__CloudFlare__AccountId = 'your-account-id-here'
```

#### Windows Command Prompt (User-level, persistent):
```cmd
setx AppSettings__RAG__CloudFlare__ApiToken "your-api-token-here"
setx AppSettings__RAG__CloudFlare__AccountId "your-account-id-here"
```

**Note:** After setting user-level environment variables, you may need to restart your terminal or IDE for them to take effect.

### Environment Variable Naming

.NET Configuration uses double underscores (`__`) to represent nested JSON structure:
- JSON path: `AppSettings.RAG.CloudFlare.ApiToken`
- Environment variable: `AppSettings__RAG__CloudFlare__ApiToken`

### Priority Order

Configuration values are loaded in this order (later values override earlier ones):
1. `appsettings.json`
2. Environment variables
3. Command-line arguments (if implemented)

This means environment variables will override values in `appsettings.json`.

