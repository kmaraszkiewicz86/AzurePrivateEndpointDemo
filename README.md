# Azure Private Endpoint Demo

This demo app supports an article about setting up basic Azure Private Endpoints for a Web App, Blob Storage, Document Intelligence, and Azure AI Search to complete an end-to-end private endpoint scenario.

## UI — `src/UI/src`

- `App` displays the page layout, upload section, and indexed-file section.
- `UploadSection` lets users upload PDFs and download or delete files uploaded during the current session.
  - `handleUpload` uploads the selected PDF and displays its status.
  - `handleDelete` deletes a PDF and removes it from the session list.
- `IndexedDocumentsSection` loads indexed files, refreshes the list on demand, and displays expandable page content with download links.
- `documentService` handles document requests through Axios.
  - `uploadPdf` sends a PDF to `POST /api/Documents` and resolves its download URL.
  - `deletePdf` sends a request to `DELETE /api/Documents/{blobName}`.
- `indexedDocumentService` retrieves the indexed-file list.
  - `getIndexedDocuments` calls `GET /api/IndexedDocuments` and resolves each file's API download URL.
- `apiClient` configures Axios using `VITE_API_BASE_URL`, which includes the API host and `/api`.
  - `getApiErrorMessage` reads the API error message or returns a fallback.
  - `createApiUrl` converts an API download path into an absolute URL.
- `UploadedDocument`, `IndexedDocument`, and `IndexedDocumentPage` describe the upload response, indexed files, and page chunks.

## API — `src/API/AzurePrivateEndpointDemo.API`

### Controllers

Both controllers use `[ApiController]` and `[Route("api/[controller]")]`.

- `DocumentsController` exposes PDF operations through `DocumentService`.
  - `UploadAsync` handles `POST /api/Documents` with multipart field `file` and returns upload details or a validation error.
  - `DownloadAsync` handles `GET /api/Documents/{blobName}` and returns the PDF stream or HTTP 404.
  - `DeleteAsync` handles `DELETE /api/Documents/{blobName}` and returns HTTP 204 or 404.
- `IndexedDocumentsController` exposes content stored in Azure AI Search.
  - `GetDocumentsAsync` handles `GET /api/IndexedDocuments` and returns files with their extracted page chunks.

### Services

- `DocumentService` manages PDFs in Blob Storage.
  - `UploadAsync` validates the PDF, uploads it under its file name, and returns an API download URL.
  - `DownloadAsync` reads the blob's metadata and content, returning null when the file is missing.
  - `DeleteAsync` deletes the blob and reports whether it existed.
  - `ValidateFile` checks the file size and PDF extension.
  - `ValidatePdfSignatureAsync` checks that the content starts with the PDF signature.
  - `ReadOriginalFileName` decodes the original name from blob metadata or falls back to the blob name.
  - `CreateDownloadUrl` builds an API download path with the escaped blob name.
- `IndexedDocumentService` reads indexed content without calling Azure OpenAI.
  - `GetDocumentsAsync` retrieves search results in batches and groups them by blob and page, preserving individual text chunks.

### Registration and configuration

- `ApplicationServiceExtensions.AddApplicationServices` registers controllers, problem details, Azure services, and CORS.
- `AzureStorageExtensions.AddAzureStorage` registers `DefaultAzureCredential`, the blob container client, and `DocumentService`.
- `IndexedDocumentExtensions.AddIndexedDocumentServices` registers the search client and `IndexedDocumentService`.
- `CorsExtensions.AddApplicationCors` allows the configured UI origins.
- `WebApplicationExtensions.UseApplicationPipeline` configures exception handling, HTTPS outside development, CORS, and controller routes.
- `AzureStorageOptions` contains the blob endpoint, container name, and upload size limit.
- `AzureAiOptions` contains the Azure AI Search endpoint and index name.

### Data classes

- `DocumentUploadResponse` contains the uploaded file's name, blob name, and API download URL.
- `DownloadedDocument` contains the PDF stream, content type, and file name.
- `IndexedDocumentDto` contains an indexed file's identity, name, download URL, and pages.
- `IndexedDocumentPageDto` contains a page number and its separate text chunks because the index does not store chunk positions.
- `SearchDocumentChunk` maps indexed fields to the API's search results.
- `ErrorResponseDto` contains a validation error message.

## Function — `src/API/AzureAISearchIndexer`

### Event handler

`AzureAISearchIndexerFunction` is the single Function entry point and uses only an `EventGridTrigger`.

- `RunAsync` checks the index and event's container, then dispatches supported events to the create or delete handler.
- `HandleBlobCreatedAsync` skips completed events, downloads the PDF, extracts and indexes its text, and marks the event as processed after success.
- `HandleBlobDeletedAsync` skips completed events, deletes matching search entries, and marks the event as processed after success.
- `StorageBlobEventData` holds the blob URL received in the event.

### Services

- `AzureBlobService` reads PDFs from the configured container.
  - `TryGetBlobName` checks the event URL's container and extracts the blob name.
  - `DownloadPdfAsync` downloads the PDF bytes and reads the original file name.
  - `ReadOriginalFileName` decodes the original name from metadata or falls back to the path's file name.
- `DocumentIntelligenceService` extracts page-aware PDF text.
  - `ExtractPageChunksAsync` analyzes the PDF and splits each page's text into chunks.
  - `SplitText` divides text into overlapping chunks using the configured size limits.
- `AzureSearchService` manages the index and its documents.
  - `EnsureIndexExistsAsync` creates the index or updates its definition when required fields are missing.
  - `IndexDocumentAsync` removes existing chunks for the file and uploads its current chunks.
  - `DeleteDocumentAsync` delegates deletion using the blob name as the file name.
  - `DeleteByFileNameAsync` finds matching chunk keys and deletes them in batches.
  - `CreateIndexDefinition` defines the chunk ID, document ID, file name, page number, content, and blob name fields.
- `ProcessedEventMemory` stores completed event IDs only within the current process, so they are lost on restart and are not shared across instances.
  - `WasProcessed` checks whether an event ID is already recorded.
  - `MarkAsProcessed` records a successfully completed event ID.

### Registration and data classes

- `FunctionServiceExtensions.AddIndexingServices` registers configuration validation, `DefaultAzureCredential`, indexing services, and the processed-event memory.
- `AzureServicesOptions` contains Azure endpoints, container and index names, the Document Intelligence model, and chunk settings.
- `BlobDocument` contains the downloaded PDF bytes and file name.
- `DocumentPageChunk` contains extracted text with its page and chunk numbers.
- `SearchDocumentChunk` represents the fields uploaded to Azure AI Search.
