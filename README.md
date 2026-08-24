# Azure Private Endpoint Demo

Learning project built with React, ASP.NET Core controllers, and one .NET 10 isolated Azure Function.

This project was built to have start point for learning private endpoint in Azure Envinronment.

## Current architecture

Document indexing (Function App):

```text
PDF upload -> ASP.NET Core API -> private Blob Storage
Blob event -> Event Grid -> Azure Front Door -> AzureAISearchIndexerFunction
AzureAISearchIndexerFunction -> Document Intelligence -> Azure AI Search
```

UI Chatbot:

```text
React -> ASP.NET Core API -> Azure AI Search -> Azure OpenAI
```

The Function project deliberately contains exactly one Function entry point: `AzureAISearchIndexerFunction`. It has only an `EventGridTrigger`. There is no HTTP-triggered Function in this repository.

## Projects

```text
src/UI/                                      React + TypeScript UI
src/API/AzurePrivateEndpointDemo.API/        PDF endpoints and chatbot/RAG logic
src/API/AzureAISearchIndexer/                the Event Grid indexing Function
```

## AzureAISearchIndexerFunction

`AzureAISearchIndexerFunction.cs` receives both supported storage event types and delegates them to two private methods:

- `HandleBlobCreatedAsync`
- `HandleBlobDeletedAsync`

The Function itself coordinates the flow. Azure-specific operations are kept in three focused services:

| Service | Responsibility |
|---|---|
| `AzureBlobService` | Validate the configured container and download a PDF with `DefaultAzureCredential`. |
| `DocumentIntelligenceService` | Extract page-aware PDF text and split long pages into chunks. |
| `AzureSearchService` | Create or update the index, upload chunks, find a document, and delete its chunks. |

`ProcessedEventMemory` contains only the in-memory set of processed Event Grid IDs.

### Index check

For every supported event, `AzureSearchService.EnsureIndexExistsAsync` checks the configured Azure AI Search index. A missing index is created. An index missing any required fields is updated before the event is handled.

The index contains:

| Field | Purpose |
|---|---|
| `id` | Unique chunk key and Azure AI Search key field. |
| `documentId` | The unique Blob Storage blob name used as the document identifier. |
| `fileName` | Original uploaded PDF name. |
| `pageNumber` | Page number returned by Document Intelligence. |
| `content` | Searchable page or page-chunk text. |
| `blobName` | Private blob path/identifier, never a public Blob Storage URL. |

### BlobCreated flow

1. Verify that the event URL belongs to the configured container.
2. Check the unique Event Grid ID in `ProcessedEventMemory`.
3. Ignore the event when the ID was already processed.
4. Download the PDF from private Blob Storage with Managed Identity through `DefaultAzureCredential`.
5. Send the PDF content to Azure AI Document Intelligence.
6. Preserve page numbers and split long page text into chunks.
7. Remove old search chunks for the same document identifier.
8. Upload the current chunks to Azure AI Search.
9. Add the Event Grid ID to memory only after indexing completes successfully.

### BlobDeleted flow

1. Check the unique Event Grid ID in `ProcessedEventMemory`.
2. Ignore the event when the ID was already processed.
3. Use the unique Blob Storage blob name to find matching chunks.
4. Check Azure AI Search for chunks whose `fileName` has that value.
5. Delete all matching chunks when they exist.
6. Add the Event Grid ID to memory only after the delete operation completes successfully.

### In-memory idempotency

Processed event IDs are held in a singleton `ConcurrentDictionary` through `ProcessedEventMemory`, as required for this learning version. A successful create/delete operation is marked only at its end, so a failed Event Grid delivery can be retried.

This is process-local memory: IDs are lost after a Function App restart, and separate scaled-out instances do not share them. A production version that must remain idempotent across restarts and instances needs a persistent Azure-backed store, but that mechanism is intentionally not included here.

## ASP.NET Core API

Controllers contain only HTTP concerns. Storage and chatbot communication are implemented in injected services.

| Controller | Service | Responsibility |
|---|---|---|
| `DocumentsController` | `DocumentService` | Validate, upload, download, and delete PDFs. |
| `ChatbotController` | `ChatbotService` | Retrieve PDF chunks from Azure AI Search, call Azure OpenAI, and return the answer with source URLs. |

All request and response DTOs are separate immutable `sealed class` types with get-only properties. No DTO uses a `record`.

### API endpoints

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/documents` | Upload one PDF using multipart field `file`. |
| `GET` | `/api/documents/{blobName}` | Stream one private PDF through the API. |
| `DELETE` | `/api/documents/{blobName}` | Delete one private PDF. |
| `POST` | `/api/chatbot` | Answer a question using Azure AI Search and Azure OpenAI. |

There is no file-listing endpoint. The UI never receives a direct Azure Blob Storage URL.

`ChatbotService` searches the configured Azure AI Search index for relevant chunks, builds a page-aware context, and calls the configured Azure OpenAI chat deployment through `Azure.AI.OpenAI`. It returns `fileName`, `pageNumber`, `documentId`, and an API download URL for each distinct source page. The Function project remains restricted to the single Event Grid Function and contains no HTTP trigger.

## Dependency registration

Both .NET projects use .NET 10 `IHostApplicationBuilder` extension methods instead of keeping all configuration in `Program.cs`.

API registration is split across:

- `ApplicationServiceExtensions`
- `AzureStorageExtensions`
- `ChatbotExtensions`
- `CorsExtensions`
- `WebApplicationExtensions`

Function registration is in `FunctionServiceExtensions`. The two `Program.cs` files only create the builder, call the appropriate extensions, build, and run.

## Authentication and Managed Identity roles

Azure SDK clients receive an injected `DefaultAzureCredential`. The code does not use storage account keys, Azure service connection strings, client secrets, search keys, or Document Intelligence keys.

Function App managed identity:

| Azure service | Required role/data access |
|---|---|
| Blob Storage | `Storage Blob Data Reader` on the PDF storage scope. |
| Azure AI Search | `Search Service Contributor` to create/update the index and `Search Index Data Contributor` to query/upload/delete documents. |
| Document Intelligence | `Cognitive Services User`. |

ASP.NET Core API managed identity:

| Azure service | Required role/data access |
|---|---|
| Blob Storage | `Storage Blob Data Contributor` for upload, download, and delete operations. |
| Azure AI Search | `Search Index Data Reader` for chatbot context retrieval. |
| Azure OpenAI | `Cognitive Services OpenAI User` for chat completions. |

The Azure Functions host storage configuration is separate from the application clients. It can use identity-based settings such as `AzureWebJobsStorage__accountName` according to the selected Azure Functions hosting plan.

## Configuration

Function App section: `Azure`.

| Setting | Example/default |
|---|---|
| `BlobServiceUri` | `https://<account>.blob.core.windows.net` |
| `DocumentContainerName` | `chatbot-documents` |
| `SearchEndpoint` | `https://<service>.search.windows.net` |
| `SearchIndexName` | `documents` |
| `DocumentIntelligenceEndpoint` | `https://<resource>.cognitiveservices.azure.com` |
| `DocumentIntelligenceModelId` | `prebuilt-layout` |
| `MaxChunkCharacters` | `4000` |
| `ChunkOverlapCharacters` | `300` |

Azure Function App settings use double underscores, for example `Azure__DocumentContainerName`. For local development, copy `local.settings.example.json` to `local.settings.json` and replace endpoint placeholders.

API sections:

| Setting | Purpose |
|---|---|
| `AzureStorage:BlobServiceUri` | Private Blob service URI. |
| `AzureStorage:DocumentContainerName` | PDF container name. |
| `AzureStorage:MaxUploadBytes` | Maximum accepted PDF size. |
| `AzureAI:SearchEndpoint` | Azure AI Search endpoint. |
| `AzureAI:SearchIndexName` | Index created by the Function App. |
| `AzureAI:OpenAIEndpoint` | Azure OpenAI endpoint. |
| `AzureAI:OpenAIChatDeployment` | Azure OpenAI chat deployment name. |
| `AzureAI:SearchResultCount` | Number of PDF chunks added to the prompt context. |
| `Cors:AllowedOrigins` | Allowed React development origins. |

React UI configuration:

| Setting | Purpose |
|---|---|
| `VITE_API_BASE_URL` | Absolute URL of the separately hosted API, including `/api`, for example `http://localhost:5107/api`. |

Copy `src/UI/.env.example` to `src/UI/.env.local` for local development. The UI calls the API directly with Axios; Vite does not proxy `/api` requests.

During local development, `DefaultAzureCredential` can use the developer identity created by `az login` or the IDE.